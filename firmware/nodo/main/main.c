/*
 * SafeBand · Nodo receptor (ESP32 clásico)
 *
 * Escanea anuncios BLE de pulseras (nombre "SB-xxxx"), promedia el RSSI de cada
 * una durante una ventana (por defecto 10 s) y manda UNA lectura por pulsera a
 * la API por WiFi (HTTP POST /api/lecturas).
 * Si una pulsera anuncia SOS, se manda de inmediato sin esperar la ventana.
 *
 * Tareas:
 *   NimBLE (host)   -> recibe anuncios y los acumula en la tabla
 *   tarea_ventana   -> cada ventana saca los promedios y los pone en la cola
 *   tarea_envio     -> saca de la cola y hace el POST a la API
 *
 * Formato del manufacturer data de la pulsera (4 bytes):
 *   FF FF 01 EE   EE: bit0 = puesta, bit1 = SOS, bit2 = batería baja
 */

#include <math.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "freertos/semphr.h"
#include "freertos/event_groups.h"
#include "esp_log.h"
#include "esp_event.h"
#include "esp_netif.h"
#include "esp_timer.h"
#include "esp_wifi.h"
#include "esp_http_client.h"
#include "nvs_flash.h"
#include "nimble/nimble_port.h"
#include "nimble/nimble_port_freertos.h"
#include "host/ble_hs.h"
#include "host/util/util.h"

static const char *TAG = "nodo";

// ---------- Ajustes ----------
#define PREFIJO_PULSERA      "SB-"
#define MAX_PULSERAS         16
#define ID_MAX               16
#define COOLDOWN_SOS_MS      5000    // mínimo entre dos avisos SOS de la misma pulsera
#define TIMEOUT_HTTP_MS      5000
#define TAM_COLA             16

// Bits de estado (iguales a los de la pulsera)
#define BIT_PUESTA        (1 << 0)
#define BIT_SOS           (1 << 1)
#define BIT_BATERIA_BAJA  (1 << 2)

// ---------- Datos ----------
typedef struct {
    char pulsera[ID_MAX];
    int rssi;
    uint8_t estado;
} lectura_t;

// Acumulador por pulsera: suma de RSSI y cuántas lecturas van en la ventana
typedef struct {
    bool usada;
    char id[ID_MAX];
    int32_t suma;
    int cuenta;
    uint8_t estado;          // último estado recibido
    bool sos_previo;
    TickType_t ultimo_sos;   // cuándo se avisó el último SOS
} acumulador_t;

static acumulador_t tabla[MAX_PULSERAS];
static SemaphoreHandle_t mutex_tabla;
static QueueHandle_t cola_envio;
static EventGroupHandle_t eventos_wifi;
#define WIFI_CONECTADO  BIT0

static uint8_t tipo_direccion;

static void iniciar_escaneo(void);

// ============================================================
// WiFi
// ============================================================
// Traduce el código de desconexión a algo que se pueda entender
static const char *motivo_wifi(uint8_t motivo)
{
    switch (motivo) {
    case WIFI_REASON_NO_AP_FOUND:
        return "no encuentra la red (nombre mal escrito, muy lejos o red solo de 5 GHz)";
    case WIFI_REASON_AUTH_FAIL:
    case WIFI_REASON_4WAY_HANDSHAKE_TIMEOUT:
    case WIFI_REASON_HANDSHAKE_TIMEOUT:
        return "contraseña incorrecta o tipo de seguridad no compatible";
    case WIFI_REASON_ASSOC_FAIL:
        return "el router rechazó la conexión";
    case WIFI_REASON_ASSOC_LEAVE:
    case WIFI_REASON_AUTH_EXPIRE:
        return "el router cerró la conexión";
    default:
        return "otro motivo (ver el código en la documentación de ESP-IDF)";
    }
}

static void al_evento_wifi(void *arg, esp_event_base_t base, int32_t id, void *datos)
{
    if (base == WIFI_EVENT && id == WIFI_EVENT_STA_START) {
        esp_wifi_connect();
    } else if (base == WIFI_EVENT && id == WIFI_EVENT_STA_DISCONNECTED) {
        wifi_event_sta_disconnected_t *ev = (wifi_event_sta_disconnected_t *)datos;
        xEventGroupClearBits(eventos_wifi, WIFI_CONECTADO);
        ESP_LOGW(TAG, "WiFi desconectado, motivo %d: %s. Reintentando...", ev->reason, motivo_wifi(ev->reason));
        esp_wifi_connect();
    } else if (base == IP_EVENT && id == IP_EVENT_STA_GOT_IP) {
        ip_event_got_ip_t *ev = (ip_event_got_ip_t *)datos;
        ESP_LOGI(TAG, "WiFi conectado, IP " IPSTR, IP2STR(&ev->ip_info.ip));
        xEventGroupSetBits(eventos_wifi, WIFI_CONECTADO);
    }
}

static void wifi_iniciar(void)
{
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));
    ESP_ERROR_CHECK(esp_event_handler_register(WIFI_EVENT, ESP_EVENT_ANY_ID, al_evento_wifi, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_STA_GOT_IP, al_evento_wifi, NULL));

    wifi_config_t wc = {0};
    strlcpy((char *)wc.sta.ssid, CONFIG_SAFEBAND_WIFI_SSID, sizeof(wc.sta.ssid));
    strlcpy((char *)wc.sta.password, CONFIG_SAFEBAND_WIFI_PASSWORD, sizeof(wc.sta.password));
    wc.sta.threshold.authmode = strlen(CONFIG_SAFEBAND_WIFI_PASSWORD) ? WIFI_AUTH_WPA2_PSK : WIFI_AUTH_OPEN;

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wc));
    ESP_ERROR_CHECK(esp_wifi_start());
}

// ============================================================
// Envío a la API
// ============================================================
static void enviar(const lectura_t *l)
{
    char json[192];
    snprintf(json, sizeof(json),
             "{\"nodo\":\"%s\",\"pulsera\":\"%s\",\"rssi\":%d,"
             "\"puesta\":%s,\"sos\":%s,\"bateriaBaja\":%s}",
             CONFIG_SAFEBAND_NODO_ID, l->pulsera, l->rssi,
             (l->estado & BIT_PUESTA) ? "true" : "false",
             (l->estado & BIT_SOS) ? "true" : "false",
             (l->estado & BIT_BATERIA_BAJA) ? "true" : "false");

    esp_http_client_config_t cfg = {
        .url = CONFIG_SAFEBAND_API_URL,
        .method = HTTP_METHOD_POST,
        .timeout_ms = TIMEOUT_HTTP_MS,
    };
    esp_http_client_handle_t cliente = esp_http_client_init(&cfg);
    esp_http_client_set_header(cliente, "Content-Type", "application/json");
    esp_http_client_set_post_field(cliente, json, strlen(json));

    int64_t inicio = esp_timer_get_time();
    esp_err_t err = esp_http_client_perform(cliente);
    int64_t ms = (esp_timer_get_time() - inicio) / 1000;

    if (err == ESP_OK) {
        int estado = esp_http_client_get_status_code(cliente);
        if (estado == 201) {
            ESP_LOGI(TAG, "OK  %s rssi=%d sos=%d  (%lld ms)", l->pulsera, l->rssi,
                     (l->estado & BIT_SOS) != 0, (long long)ms);
        } else {
            ESP_LOGW(TAG, "La API rechazó la lectura de %s: HTTP %d", l->pulsera, estado);
        }
    } else {
        ESP_LOGE(TAG, "No se pudo enviar %s: %s", l->pulsera, esp_err_to_name(err));
    }
    esp_http_client_cleanup(cliente);
}

static void tarea_envio(void *param)
{
    lectura_t l;
    while (true) {
        xQueueReceive(cola_envio, &l, portMAX_DELAY);
        if (!(xEventGroupGetBits(eventos_wifi) & WIFI_CONECTADO)) {
            ESP_LOGW(TAG, "Sin WiFi: se descarta la lectura de %s", l.pulsera);
            continue;
        }
        enviar(&l);
    }
}

// ============================================================
// Acumulación de lecturas BLE
// ============================================================
static acumulador_t *buscar_o_crear(const char *id)
{
    acumulador_t *libre = NULL;
    for (int i = 0; i < MAX_PULSERAS; i++) {
        if (tabla[i].usada && strcmp(tabla[i].id, id) == 0) return &tabla[i];
        if (!tabla[i].usada && !libre) libre = &tabla[i];
    }
    if (libre) {
        memset(libre, 0, sizeof(*libre));
        libre->usada = true;
        strlcpy(libre->id, id, ID_MAX);
    }
    return libre;
}

// Se llama por cada anuncio recibido (desde la tarea de NimBLE)
static void registrar(const char *id, int rssi, uint8_t estado)
{
    bool avisar_sos = false;

    xSemaphoreTake(mutex_tabla, portMAX_DELAY);
    acumulador_t *a = buscar_o_crear(id);
    if (a) {
        a->suma += rssi;
        a->cuenta++;
        a->estado = estado;

        if (estado & BIT_SOS) {
            TickType_t ahora = xTaskGetTickCount();
            if (!a->sos_previo || (ahora - a->ultimo_sos) >= pdMS_TO_TICKS(COOLDOWN_SOS_MS)) {
                a->sos_previo = true;
                a->ultimo_sos = ahora;
                avisar_sos = true;
            }
        }
    }
    xSemaphoreGive(mutex_tabla);

    // El SOS no espera la ventana: sale ya, con el RSSI instantáneo
    if (avisar_sos) {
        lectura_t l = { .rssi = rssi, .estado = estado };
        strlcpy(l.pulsera, id, ID_MAX);
        ESP_LOGW(TAG, "¡SOS de %s! Enviando de inmediato", id);
        xQueueSend(cola_envio, &l, 0);
    }
}

// Cada ventana: promedio por pulsera -> cola de envío
static void tarea_ventana(void *param)
{
    const TickType_t ventana = pdMS_TO_TICKS(CONFIG_SAFEBAND_VENTANA_S * 1000);

    while (true) {
        vTaskDelay(ventana);

        lectura_t listas[MAX_PULSERAS];
        int n = 0;

        xSemaphoreTake(mutex_tabla, portMAX_DELAY);
        for (int i = 0; i < MAX_PULSERAS; i++) {
            if (tabla[i].usada && tabla[i].cuenta > 0) {
                strlcpy(listas[n].pulsera, tabla[i].id, ID_MAX);
                listas[n].rssi = (int)lroundf((float)tabla[i].suma / tabla[i].cuenta);
                listas[n].estado = tabla[i].estado;
                ESP_LOGI(TAG, "Ventana: %s promedio %d dBm (%d anuncios)",
                         tabla[i].id, listas[n].rssi, tabla[i].cuenta);
                tabla[i].suma = 0;
                tabla[i].cuenta = 0;
                n++;
            }
        }
        xSemaphoreGive(mutex_tabla);

        if (n == 0) ESP_LOGI(TAG, "Ventana sin pulseras cerca");
        for (int i = 0; i < n; i++) xQueueSend(cola_envio, &listas[i], 0);
    }
}

// ============================================================
// BLE: escaneo
// ============================================================
static int al_evento_gap(struct ble_gap_event *evento, void *arg)
{
    if (evento->type == BLE_GAP_EVENT_DISC) {
        struct ble_hs_adv_fields campos;
        if (ble_hs_adv_parse_fields(&campos, evento->disc.data, evento->disc.length_data) != 0) return 0;

        // Solo pulseras SafeBand: nombre "SB-..." + manufacturer data FF FF 01 EE
        size_t prefijo = strlen(PREFIJO_PULSERA);
        if (campos.name == NULL || campos.name_len <= prefijo || campos.name_len >= ID_MAX) return 0;
        if (memcmp(campos.name, PREFIJO_PULSERA, prefijo) != 0) return 0;
        if (campos.mfg_data == NULL || campos.mfg_data_len < 4) return 0;
        if (campos.mfg_data[0] != 0xFF || campos.mfg_data[1] != 0xFF || campos.mfg_data[2] != 0x01) return 0;

        char id[ID_MAX];
        memcpy(id, campos.name, campos.name_len);
        id[campos.name_len] = '\0';
        registrar(id, evento->disc.rssi, campos.mfg_data[3]);
    } else if (evento->type == BLE_GAP_EVENT_DISC_COMPLETE) {
        iniciar_escaneo();   // por si el escaneo termina, se reinicia
    }
    return 0;
}

static void iniciar_escaneo(void)
{
    struct ble_gap_disc_params p = {0};
    p.passive = 1;              // la pulsera no responde a escaneos activos
    p.filter_duplicates = 0;    // queremos TODOS los anuncios para promediar
    p.itvl = 160;               // 100 ms
    p.window = 80;              // escucha 50 ms de cada 100 (deja aire al WiFi)

    int rc = ble_gap_disc(tipo_direccion, BLE_HS_FOREVER, &p, al_evento_gap, NULL);
    if (rc != 0) ESP_LOGE(TAG, "No se pudo iniciar el escaneo: %d", rc);
    else ESP_LOGI(TAG, "Escaneando pulseras (%s*)...", PREFIJO_PULSERA);
}

static void al_sincronizar(void)
{
    ble_hs_util_ensure_addr(0);
    ble_hs_id_infer_auto(0, &tipo_direccion);
    iniciar_escaneo();
}

static void al_reiniciar(int motivo)
{
    ESP_LOGW(TAG, "Bluetooth reiniciado, motivo=%d", motivo);
}

static void tarea_ble(void *param)
{
    nimble_port_run();
    nimble_port_freertos_deinit();
}

// ============================================================
void app_main(void)
{
    esp_err_t err = nvs_flash_init();
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        err = nvs_flash_init();
    }
    ESP_ERROR_CHECK(err);

    mutex_tabla = xSemaphoreCreateMutex();
    cola_envio = xQueueCreate(TAM_COLA, sizeof(lectura_t));
    eventos_wifi = xEventGroupCreate();

    ESP_LOGI(TAG, "Nodo %s | API: %s | ventana %d s",
             CONFIG_SAFEBAND_NODO_ID, CONFIG_SAFEBAND_API_URL, CONFIG_SAFEBAND_VENTANA_S);

    wifi_iniciar();

    xTaskCreate(tarea_envio, "envio", 6144, NULL, 5, NULL);
    xTaskCreate(tarea_ventana, "ventana", 4096, NULL, 5, NULL);

    ESP_ERROR_CHECK(nimble_port_init());
    ble_hs_cfg.sync_cb = al_sincronizar;
    ble_hs_cfg.reset_cb = al_reiniciar;
    nimble_port_freertos_init(tarea_ble);
}
