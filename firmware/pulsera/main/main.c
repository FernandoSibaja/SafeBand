/*
 * SafeBand · Pulsera simulada (ESP32 clásico)
 *
 * Anuncia por BLE su identificador (ej. "SB-0001") y su estado dentro del
 * "manufacturer data" del anuncio. Los nodos escanean este anuncio.
 *
 * Simulación con la placa:
 *   - Botón BOOT (GPIO 0) presionado 2 s  -> SOS activo por 30 s
 *   - Jumper GPIO 25 a GND                -> pulsera PUESTA (sin jumper = quitada)
 *   - Jumper GPIO 26 a GND                -> BATERÍA BAJA simulada
 *   - LED integrado (GPIO 2): fijo = SOS, parpadeo = quitada, apagado = normal
 *
 * Formato del manufacturer data (4 bytes):
 *   [0-1] 0xFFFF  ID de compañía reservado para pruebas
 *   [2]   0x01    versión del formato
 *   [3]   estado  bit0 = puesta, bit1 = SOS, bit2 = batería baja
 */

#include <stdbool.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "nimble/nimble_port.h"
#include "nimble/nimble_port_freertos.h"
#include "host/ble_hs.h"
#include "host/util/util.h"

static const char *TAG = "pulsera";

// ---------- Pines ----------
#define PIN_BOTON_SOS   GPIO_NUM_0    // botón BOOT de la placa
#define PIN_PUESTA      GPIO_NUM_25   // jumper a GND = puesta (simula el reed switch)
#define PIN_BATERIA     GPIO_NUM_26   // jumper a GND = batería baja (simulada)
#define PIN_LED         GPIO_NUM_2    // LED azul integrado

// ---------- Tiempos ----------
#define TICK_MS              50
#define SOS_PULSACION_MS     2000     // cuánto hay que mantener el botón
#define SOS_DURACION_MS      30000    // cuánto dura el SOS activo

// Intervalo de anuncio en unidades de 0.625 ms
#define INTERVALO_NORMAL     800      // 500 ms
#define INTERVALO_SOS        160      // 100 ms (modo rápido para que el SOS llegue antes)

// ---------- Bits de estado ----------
#define BIT_PUESTA       (1 << 0)
#define BIT_SOS          (1 << 1)
#define BIT_BATERIA_BAJA (1 << 2)

static uint8_t tipo_direccion;
static volatile bool ble_listo = false;
static uint8_t estado_anunciado = 0xFF;   // 0xFF = todavía no se anuncia nada

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
    params.itvl_min = params.itvl_max = (estado & BIT_SOS) ? INTERVALO_SOS : INTERVALO_NORMAL;

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
// Entradas (botón y jumpers) y LED
// ============================================================
static void configurar_pines(void)
{
    gpio_config_t entradas = {
        .pin_bit_mask = (1ULL << PIN_BOTON_SOS) | (1ULL << PIN_PUESTA) | (1ULL << PIN_BATERIA),
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,   // sin conectar = 1; con jumper a GND = 0
    };
    gpio_config(&entradas);

    gpio_config_t salida = {
        .pin_bit_mask = 1ULL << PIN_LED,
        .mode = GPIO_MODE_OUTPUT,
    };
    gpio_config(&salida);
}

// Bucle principal: lee entradas, calcula el estado y actualiza el anuncio si cambió
static void bucle_principal(void)
{
    uint32_t ms_presionado = 0;
    uint32_t ms_sos_restante = 0;
    uint32_t contador = 0;

    while (true) {
        // --- Botón SOS: hay que mantenerlo presionado (evita falsos positivos) ---
        if (gpio_get_level(PIN_BOTON_SOS) == 0) {
            ms_presionado += TICK_MS;
            if (ms_presionado == SOS_PULSACION_MS) {
                ms_sos_restante = SOS_DURACION_MS;
                ESP_LOGW(TAG, "¡SOS activado!");
            }
        } else {
            ms_presionado = 0;
        }
        if (ms_sos_restante > 0) ms_sos_restante -= TICK_MS;

        // --- Armar el byte de estado ---
        uint8_t estado = 0;
        if (gpio_get_level(PIN_PUESTA) == 0)  estado |= BIT_PUESTA;
        if (ms_sos_restante > 0)              estado |= BIT_SOS;
        if (gpio_get_level(PIN_BATERIA) == 0) estado |= BIT_BATERIA_BAJA;

        // --- Solo se reinicia el anuncio si algo cambió ---
        if (ble_listo && estado != estado_anunciado) {
            anunciar(estado);
        }

        // --- LED: fijo = SOS, parpadeo = quitada, apagado = normal ---
        contador++;
        int led = 0;
        if (estado & BIT_SOS) led = 1;
        else if (!(estado & BIT_PUESTA)) led = (contador / 10) % 2;   // cambia cada 500 ms
        gpio_set_level(PIN_LED, led);

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

    // Iniciar NimBLE
    ESP_ERROR_CHECK(nimble_port_init());
    ble_hs_cfg.sync_cb = al_sincronizar;
    ble_hs_cfg.reset_cb = al_reiniciar;
    nimble_port_freertos_init(tarea_ble);

    ESP_LOGI(TAG, "Pulsera %s iniciada", CONFIG_SAFEBAND_PULSERA_ID);
    bucle_principal();
}
