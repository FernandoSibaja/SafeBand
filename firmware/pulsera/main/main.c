/*
 * SafeBand · Pulsera (Seeed XIAO ESP32-C3)
 *
 * Anuncia por BLE su identificador (ej. "SB-0001") y su estado dentro del
 * "manufacturer data" del anuncio. Los nodos escanean este anuncio.
 *
 * Pines (ver el documento de arquitectura):
 *   - GPIO5 (D3)  botón SOS a GND. También sirve el botón BOOT del XIAO (GPIO9).
 *                 Mantener 3 s con el LED apagado -> SOS (vibra largo, LED azul, se anuncia 2 min).
 *                 Mantener 3 s con el LED azul    -> cancelar (vibra 2 veces, LED verde, se anuncia 10 s).
 *                 Con el LED apagado SIEMPRE es un SOS nuevo: si la escuela lo desactivó desde la página,
 *                 la pulsera no se entera, y así un segundo apretón nunca se toma como "cancelar" a ciegas.
 *   - GPIO4 (D2)  reed switch simple a GND (o jumper a GND) -> pulsera PUESTA.
 *                 Sin módulo ni comparador: usa el pull-up interno (consumo ~0 con el broche abierto).
 *                 El nivel que significa "puesta" se elige en menuconfig.
 *   - GPIO3 (D1)  ADC: VBAT / 2 por divisor de 2 x 100 kOhm (opcional, menuconfig).
 *   - GPIO7 (D5)  LED WS2812: azul = SOS, verde = SOS cancelado, rojo parpadeando = quitada,
 *                 naranja parpadeando lento = batería baja, apagado = normal.
 *   - GPIO6 (D4)  motor de vibración por transistor 2N2222: un pulso largo al activar el SOS, dos cortos al cancelarlo.
 *
 * Formato del manufacturer data (4 bytes):
 *   [0-1] 0xFFFF  ID de compañía reservado para pruebas
 *   [2]   0x01    versión del formato
 *   [3]   estado  bit0 = puesta, bit1 = SOS, bit2 = batería baja, bit3 = SOS cancelado
 */

#include <stdbool.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "esp_adc/adc_oneshot.h"
#include "esp_adc/adc_cali.h"
#include "esp_adc/adc_cali_scheme.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "led_strip.h"
#include "nimble/nimble_port.h"
#include "nimble/nimble_port_freertos.h"
#include "host/ble_hs.h"
#include "host/util/util.h"

static const char *TAG = "pulsera";

// ---------- Pines (XIAO ESP32-C3) ----------
#define PIN_BOTON_SOS   GPIO_NUM_5    // D3: botón SOS a GND
#define PIN_BOTON_BOOT  GPIO_NUM_9    // botón BOOT del XIAO (para probar sin botón externo)
#define PIN_REED        GPIO_NUM_4    // D2: reed switch a GND (imán en el broche) o jumper
#define PIN_BATERIA     GPIO_NUM_3    // D1: ADC, VBAT / 2
#define PIN_MOTOR       GPIO_NUM_6    // D4: base del 2N2222 (por 1 kOhm)
#define PIN_LED         GPIO_NUM_7    // D5: datos del WS2812

// ---------- Tiempos ----------
#define TICK_MS              50
#define SOS_PULSACION_MS     3000     // cuánto hay que mantener el botón (evita presiones accidentales y por juego)
#define SOS_DURACION_MS      120000   // cuánto se anuncia el SOS (en el sistema sigue activo hasta que lo desactiven)
#define CANCELADO_MS         10000    // cuánto se anuncia "SOS cancelado" (para que algún nodo lo oiga)
#define VIBRACION_SOS_MS     600      // pulso largo al activar el SOS
#define VIBRACION_CORTA_MS   150      // dos pulsos cortos al cancelar
#define BATERIA_CADA_MS      5000     // cada cuánto se mide la batería

// Intervalo de anuncio en unidades de 0.625 ms
#define INTERVALO_NORMAL     800      // 500 ms
#define INTERVALO_SOS        160      // 100 ms (modo rápido para que el SOS llegue antes)

// ---------- Bits de estado ----------
#define BIT_PUESTA       (1 << 0)
#define BIT_SOS          (1 << 1)
#define BIT_BATERIA_BAJA (1 << 2)
#define BIT_SOS_CANCELADO (1 << 3)

// Brillo del LED (0-255): bajo, porque va pegado a la muñeca y gasta batería
#define BRILLO 24

static uint8_t tipo_direccion;
static volatile bool ble_listo = false;
static uint8_t estado_anunciado = 0xFF;   // 0xFF = todavía no se anuncia nada

static led_strip_handle_t led;

// ============================================================
// BLE: construir y (re)iniciar el anuncio
// ============================================================
static void anunciar(uint8_t estado)
{
    if (!ble_listo) return;

    // Si ya se estaba anunciando, se detiene para cambiar datos e intervalo
    ble_gap_adv_stop();

    const char *nombre = CONFIG_SAFEBAND_PULSERA_ID;
    uint8_t datos_fabricante[4] = { 0xFF, 0xFF, 0x01, estado };

    struct ble_hs_adv_fields campos = {0};
    campos.flags = BLE_HS_ADV_F_DISC_GEN | BLE_HS_ADV_F_BREDR_UNSUP;
    campos.name = (uint8_t *)nombre;
    campos.name_len = strlen(nombre);
    campos.name_is_complete = 1;
    campos.mfg_data = datos_fabricante;
    campos.mfg_data_len = sizeof(datos_fabricante);

    int rc = ble_gap_adv_set_fields(&campos);
    if (rc != 0) {
        ESP_LOGE(TAG, "Error al preparar el anuncio: %d", rc);
        return;
    }

    struct ble_gap_adv_params params = {0};
    params.conn_mode = BLE_GAP_CONN_MODE_NON;   // solo anuncia, no acepta conexiones
    params.disc_mode = BLE_GAP_DISC_MODE_GEN;
    params.itvl_min = params.itvl_max = (estado & (BIT_SOS | BIT_SOS_CANCELADO)) ? INTERVALO_SOS : INTERVALO_NORMAL;

    rc = ble_gap_adv_start(tipo_direccion, NULL, BLE_HS_FOREVER, &params, NULL, NULL);
    if (rc != 0) {
        ESP_LOGE(TAG, "Error al iniciar el anuncio: %d", rc);
        return;
    }

    estado_anunciado = estado;
    ESP_LOGI(TAG, "Anunciando %s | puesta=%d sos=%d bateria_baja=%d",
             nombre,
             (estado & BIT_PUESTA) != 0,
             (estado & BIT_SOS) != 0,
             (estado & BIT_BATERIA_BAJA) != 0);
}

// NimBLE llama esto cuando el Bluetooth está listo para usarse
static void al_sincronizar(void)
{
    ble_hs_util_ensure_addr(0);
    ble_hs_id_infer_auto(0, &tipo_direccion);
    ble_listo = true;
    ESP_LOGI(TAG, "Bluetooth listo");
}

static void al_reiniciar(int motivo)
{
    ble_listo = false;
    estado_anunciado = 0xFF;
    ESP_LOGW(TAG, "Bluetooth reiniciado, motivo=%d", motivo);
}

// Tarea donde corre el stack NimBLE
static void tarea_ble(void *param)
{
    nimble_port_run();
    nimble_port_freertos_deinit();
}

// ============================================================
// Batería: ADC en GPIO3 a través del divisor (VBAT / 2)
// ============================================================
#if CONFIG_SAFEBAND_MEDIR_BATERIA
static adc_oneshot_unit_handle_t adc;
static adc_cali_handle_t calibracion;
static adc_channel_t canal_bateria;

static void configurar_bateria(void)
{
    adc_unit_t unidad;
    ESP_ERROR_CHECK(adc_oneshot_io_to_channel(PIN_BATERIA, &unidad, &canal_bateria));

    adc_oneshot_unit_init_cfg_t cfg_unidad = { .unit_id = unidad };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&cfg_unidad, &adc));

    // Atenuación de 12 dB: mide hasta ~2.5 V; con el divisor, VBAT de hasta ~5 V
    adc_oneshot_chan_cfg_t cfg_canal = { .atten = ADC_ATTEN_DB_12, .bitwidth = ADC_BITWIDTH_DEFAULT };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc, canal_bateria, &cfg_canal));

    // Calibración de fábrica del chip: convierte la lectura cruda a milivolts
    adc_cali_curve_fitting_config_t cfg_cali = {
        .unit_id = unidad, .chan = canal_bateria, .atten = ADC_ATTEN_DB_12, .bitwidth = ADC_BITWIDTH_DEFAULT,
    };
    ESP_ERROR_CHECK(adc_cali_create_scheme_curve_fitting(&cfg_cali, &calibracion));
}

// Voltaje de la batería en mV (promedio de 8 lecturas), o -1 si falla
static int leer_bateria_mv(void)
{
    int suma = 0;
    for (int i = 0; i < 8; i++) {
        int crudo, mv;
        if (adc_oneshot_read(adc, canal_bateria, &crudo) != ESP_OK) return -1;
        if (adc_cali_raw_to_voltage(calibracion, crudo, &mv) != ESP_OK) return -1;
        suma += mv;
    }
    return (suma / 8) * 2;   // x2 por el divisor de dos resistencias iguales
}
#endif

// ============================================================
// Entradas, LED y motor
// ============================================================
static void configurar_pines(void)
{
    gpio_config_t entradas = {
        .pin_bit_mask = (1ULL << PIN_BOTON_SOS) | (1ULL << PIN_BOTON_BOOT) | (1ULL << PIN_REED),
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,   // sin conectar = 1; a GND = 0
    };
    gpio_config(&entradas);

    gpio_config_t motor = {
        .pin_bit_mask = 1ULL << PIN_MOTOR,
        .mode = GPIO_MODE_OUTPUT,
    };
    gpio_config(&motor);
    gpio_set_level(PIN_MOTOR, 0);

    // WS2812: un solo LED controlado por el periférico RMT
    led_strip_config_t cfg_led = {
        .strip_gpio_num = PIN_LED,
        .max_leds = 1,
        .led_model = LED_MODEL_WS2812,
    };
    led_strip_rmt_config_t cfg_rmt = {
        .clk_src = RMT_CLK_SRC_DEFAULT,
        .resolution_hz = 10 * 1000 * 1000,   // 10 MHz
    };
    ESP_ERROR_CHECK(led_strip_new_rmt_device(&cfg_led, &cfg_rmt, &led));
    led_strip_clear(led);
}

// Pone el color del LED solo si cambió (r, g, b ya con brillo aplicado)
static void color_led(uint8_t r, uint8_t g, uint8_t b)
{
    static uint32_t anterior = 0xFFFFFFFF;
    uint32_t actual = ((uint32_t)r << 16) | ((uint32_t)g << 8) | b;
    if (actual == anterior) return;
    anterior = actual;
    if (actual == 0) {
        led_strip_clear(led);
    } else {
        led_strip_set_pixel(led, 0, r, g, b);
        led_strip_refresh(led);
    }
}

// Bucle principal: lee entradas, calcula el estado y actualiza el anuncio si cambió
static void bucle_principal(void)
{
    uint32_t ms_presionado = 0;
    uint32_t ms_sos_restante = 0;
    uint32_t ms_cancelado_restante = 0;
    uint32_t ms_vibracion = 0;      // tiempo que falta del patrón de vibración
    uint32_t patron_vibracion = 0;  // 0 = pulso continuo; si no, dos pulsos cortos
    uint32_t contador = 0;
    bool bateria_baja = false;

    while (true) {
        // --- Botón SOS (externo o BOOT): hay que mantenerlo 3 s ---
        // LED azul (SOS anunciándose) → cancelar. LED apagado → SOS nuevo, siempre.
        bool presionado = gpio_get_level(PIN_BOTON_SOS) == 0 || gpio_get_level(PIN_BOTON_BOOT) == 0;
        if (presionado) {
            ms_presionado += TICK_MS;
            if (ms_presionado == SOS_PULSACION_MS) {
                if (ms_sos_restante > 0) {
                    ms_sos_restante = 0;
                    ms_cancelado_restante = CANCELADO_MS;
                    ms_vibracion = 3 * VIBRACION_CORTA_MS;   // vibra - pausa - vibra
                    patron_vibracion = 1;
                    ESP_LOGW(TAG, "SOS cancelado desde la pulsera");
                } else {
                    ms_sos_restante = SOS_DURACION_MS;
                    ms_cancelado_restante = 0;
                    ms_vibracion = VIBRACION_SOS_MS;
                    patron_vibracion = 0;
                    ESP_LOGW(TAG, "¡SOS activado!");
                }
            }
        } else {
            ms_presionado = 0;
        }
        if (ms_sos_restante > 0) ms_sos_restante -= TICK_MS;
        if (ms_cancelado_restante > 0) ms_cancelado_restante -= TICK_MS;

        // --- Motor: pulso largo (SOS) o dos cortos (cancelado) ---
        bool vibrar = ms_vibracion > 0;
        if (vibrar && patron_vibracion == 1)   // apagado en el tercio de en medio = la pausa
            vibrar = ms_vibracion > 2 * VIBRACION_CORTA_MS || ms_vibracion <= VIBRACION_CORTA_MS;
        gpio_set_level(PIN_MOTOR, vibrar);
        if (ms_vibracion > 0) ms_vibracion -= TICK_MS;

        // --- Batería (cada 5 s), con 100 mV de histéresis para no parpadear en el límite ---
#if CONFIG_SAFEBAND_MEDIR_BATERIA
        if (contador % (BATERIA_CADA_MS / TICK_MS) == 0) {
            int mv = leer_bateria_mv();
            if (mv > 0) {
                if (!bateria_baja && mv < CONFIG_SAFEBAND_BATERIA_BAJA_MV) bateria_baja = true;
                else if (bateria_baja && mv > CONFIG_SAFEBAND_BATERIA_BAJA_MV + 100) bateria_baja = false;
                ESP_LOGI(TAG, "Batería: %d mV%s", mv, bateria_baja ? " (BAJA)" : "");
            }
        }
#endif

        // --- Armar el byte de estado ---
        uint8_t estado = 0;
        if (gpio_get_level(PIN_REED) == CONFIG_SAFEBAND_REED_NIVEL_PUESTA) estado |= BIT_PUESTA;
        if (ms_sos_restante > 0)       estado |= BIT_SOS;
        if (ms_cancelado_restante > 0) estado |= BIT_SOS_CANCELADO;
        if (bateria_baja)              estado |= BIT_BATERIA_BAJA;

        // --- Solo se reinicia el anuncio si algo cambió ---
        if (ble_listo && estado != estado_anunciado) {
            anunciar(estado);
        }

        // --- LED: prioridad SOS > cancelado > quitada > batería baja > apagado ---
        contador++;
        bool parpadeo = (contador / 10) % 2;    // cambia cada 500 ms
        bool parpadeo_lento = (contador / 20) % 2;   // cambia cada 1 s
        if (estado & BIT_SOS)                 color_led(0, 0, BRILLO);                        // azul fijo
        else if (ms_cancelado_restante > CANCELADO_MS - 2000) color_led(0, BRILLO, 0);        // verde 2 s
        else if (!(estado & BIT_PUESTA))      color_led(parpadeo ? BRILLO : 0, 0, 0);         // rojo parpadeando
        else if (estado & BIT_BATERIA_BAJA)   color_led(parpadeo_lento ? BRILLO : 0, parpadeo_lento ? BRILLO / 3 : 0, 0); // naranja
        else                                  color_led(0, 0, 0);                             // apagado

        vTaskDelay(pdMS_TO_TICKS(TICK_MS));
    }
}

// ============================================================
void app_main(void)
{
    // NVS: memoria que usa el Bluetooth para guardar su configuración
    esp_err_t err = nvs_flash_init();
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        err = nvs_flash_init();
    }
    ESP_ERROR_CHECK(err);

    configurar_pines();
#if CONFIG_SAFEBAND_MEDIR_BATERIA
    configurar_bateria();
#endif

    // Iniciar NimBLE
    ESP_ERROR_CHECK(nimble_port_init());
    ble_hs_cfg.sync_cb = al_sincronizar;
    ble_hs_cfg.reset_cb = al_reiniciar;
    nimble_port_freertos_init(tarea_ble);

    ESP_LOGI(TAG, "Pulsera %s iniciada (XIAO ESP32-C3)", CONFIG_SAFEBAND_PULSERA_ID);
    bucle_principal();
}
