# Manual del proyecto SafeBand

Guía de qué hay en el proyecto, para qué sirve cada cosa y dónde está.
Se actualiza cada vez que agregamos algo.

---

## 1. Ubicación y cómo abrirlo

| Qué | Dónde |
|---|---|
| Carpeta del proyecto | `C:\SafeBand\SafeBand.api\` |
| Solución (abrir en VS 2026) | `C:\SafeBand\SafeBand.api\SafeBand.api.slnx` |
| Código de la API | `C:\SafeBand\SafeBand.api\SafeBand.api\` |
| Este manual | `C:\SafeBand\SafeBand.api\MANUAL.md` |

**Ejecutar:** abrir la solución en Visual Studio 2026 y presionar **F5**.
La consola muestra `Now listening on: https://localhost:7001` → la API está corriendo.
Cerrar esa consola (o Ctrl+C) apaga la API.

---

## 2. Decisiones tomadas

| Decisión | Elección | Por qué |
|---|---|---|
| Tipo de proyecto | ASP.NET Core Web API | Es un servidor sin ventanas: recibe datos del ESP32 y responde a la app de padres/escuela |
| Versión | .NET 10 (LTS) | Soporte hasta 2028; .NET 9 pierde soporte en nov 2026 |
| Estilo de endpoints | Minimal API (sin controllers) | Menos código: cada endpoint es `app.MapGet(...)` / `app.MapPost(...)` |
| Base de datos | SQL Server — LocalDB en la PC, Azure SQL en la nube | Mismo motor en ambos lados |
| Cómo se crean las tablas | **Code First** con Entity Framework Core | Las clases de C# se convierten en tablas mediante migraciones; hay historial de cambios y la BD de Azure queda idéntica a la local |
| Comunicación ESP32 → API | HTTP (JSON) | Más simple de desplegar. Si se cae el internet, el ESP32 guarda las lecturas en cola y las reenvía con su hora original. MQTT queda como opción futura (útil para el modo emergencia) |
| Volumen de `LecturasBle` | El nodo **promedia** el RSSI de cada pulsera en una ventana (~10 s) y manda 1 lectura por ventana; SOS / pulsera quitada / batería baja se mandan de inmediato. A futuro: tabla de **estancias** (entrada/salida por zona) como dato permanente y **purga** de lecturas crudas con más de ~30 días | Sin resumir serían ~880 mil registros/día por salón. Resumiendo: ~88 mil/día (~10 MB), manejable. Promediar además reduce el ruido del RSSI |
| Pulsera → nodo | **BLE** (no WiFi ni ESP-NOW) | Batería: WiFi conectado agotaría 400 mAh en horas; BLE dura días. El alcance corto (~10-30 m) ayuda a no detectar niños del salón de al lado. Es estándar (un celular puede simular la pulsera) |
| Nodo → API | **WiFi** al router si la zona tiene internet; **ESP-NOW** hacia un nodo vecino si no tiene (nodos puente). El nodo con internet recibe por ESP-NOW y sube por WiFi | En muchas escuelas solo algunas zonas tienen internet. ESP-NOW no necesita router y llega más lejos. Requisito: todos en el mismo canal WiFi que el router. **Prototipo alfa**, no checkpoint |
| Cuántos nodos | **Uno por zona** (salón); **3-4 en áreas grandes** (patio) | Los define el número de zonas, no el alcance BLE. Para posición con punto se necesitan ≥3 nodos que vean la pulsera |
| Envío de datos (futuro) | Pasar de "lectura cada 10 s" a **eventos** (entró / salió / SOS / quitada) + **latido** periódico | ~3 mil registros/día por salón en vez de ~88 mil. El latido distingue "se fue" de "nodo caído". Se hace **después del checkpoint**: primero se necesitan datos crudos para calibrar umbrales |
| Framework del firmware | **ESP-IDF** para pulsera y nodos. Terminales (PN532, LCD, teclado): decidir al llegar el hardware; Arduino/PlatformIO es válido ahí | IDF da control fino de batería y sueño; pulsera y nodo ya funcionan. Cada dispositivo es independiente (hablan JSON con la API), se pueden mezclar frameworks |
| Ubicación | **Zona** como dato principal (alertas). **Posición en un plano** como función extra donde haya ≥3 nodos (trilateración / centroide ponderado), con círculo de incertidumbre y limitada a la zona detectada | Precisión BLE en interiores: 2-5 m; el cuerpo del niño atenúa la señal. Pedido del profesor |

---

## 3. Estructura del proyecto

```
SafeBand.api\
├── SafeBand.api.slnx              ← la solución
├── MANUAL.md                      ← este manual
├── firmware\                      ← código de los ESP32 (VS Code + ESP-IDF v6.0.2)
│   ├── .gitignore                 ← no sube build\ ni sdkconfig
│   ├── pulsera\                   ← pulsera simulada con un ESP32 clásico
│   └── nodo\                      ← nodo receptor (un mismo programa para nodo-1, nodo-2...)
└── SafeBand.api\                  ← el proyecto
    ├── Program.cs                 ← punto de arranque de la API
    ├── appsettings.json           ← configuración general
    ├── appsettings.Development.json ← configuración solo para tu PC
    ├── SafeBand.api.csproj        ← definición del proyecto y paquetes NuGet
    ├── SafeBand.api.http          ← peticiones de prueba desde VS
    ├── Properties\
    │   └── launchSettings.json    ← puertos y perfiles de arranque (http / https)
    ├── wwwroot\                   ← FRONTEND (la API lo sirve en "/")
    │   ├── index.html             ← estructura de la página
    │   ├── app.js                 ← lógica: pide datos a la API y los muestra
    │   └── styles.css             ← diseño
    ├── Contracts\
    │   └── LecturaContracts.cs    ← forma del JSON que entra y sale de /api/lecturas
    ├── Endpoints\
    │   └── LecturasEndpoints.cs   ← POST y GET de /api/lecturas
    ├── Data\
    │   ├── AppDbContext.cs        ← conexión entre los modelos y SQL Server
    │   └── DbSeeder.cs            ← datos de prueba (solo en tu PC)
    ├── Migrations\                ← historial de cambios de la BD (generado por EF, no se edita)
    └── Models\                    ← clases que se convierten en tablas
        ├── Zona.cs
        ├── Nodo.cs
        ├── Alumno.cs
        ├── Pulsera.cs
        └── LecturaBle.cs
```

**Carpetas que se generan solas (no se tocan):**

| Carpeta | Qué es |
|---|---|
| `bin\` | El programa ya compilado |
| `obj\` | Archivos intermedios de compilación (`Debug` = modo de compilación, no "bug") |
| `.vs\` | Caché y configuración personal de Visual Studio |

Se pueden borrar sin problema; VS las regenera. No se suben a GitHub.

---

## 4. Archivos explicados

### `Program.cs`
Arranca la API. Tiene tres partes, de arriba hacia abajo:

1. **Registrar servicios** — `builder.Services.Add...(...)`: herramientas que la app usará (OpenAPI; después, la base de datos).
2. **Configurar el procesamiento de peticiones** — `app.Use...(...)`: por ejemplo, redirigir http → https.
3. **Definir endpoints y arrancar** — `app.MapGet("/ruta", ...)` define qué responde la API en cada ruta; `app.Run()` enciende el servidor.

Estado actual (organizado en 3 secciones con comentarios `// ---------- 1. / 2. / 3.`):
1. **Servicios**: OpenAPI, base de datos, validación (`AddValidation`).
2. **Procesamiento**: manejador de errores (JSON mal formado → 400 con mensaje claro; error inesperado → 500), y en desarrollo: OpenAPI, Swagger UI y el sembrador de datos.
3. **Endpoints**: `app.MapLecturas()` registra los de `Endpoints\LecturasEndpoints.cs`.

Se quitó el ejemplo `/weatherforecast` y `UseHttpsRedirection()` (el ESP32 manda por `http://` en la red local y la redirección lo haría fallar; en Azure el HTTPS lo da el propio App Service).

Registro de la base de datos (en la parte 1):
```csharp
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SafeBand")));
```
Le dice a la app: "usa `AppDbContext` con SQL Server, y la cadena de conexión se llama `SafeBand` en appsettings". Después, cualquier endpoint puede pedir `AppDbContext db` como parámetro y .NET se lo entrega listo (esto se llama *inyección de dependencias*).

### `appsettings.Development.json` — cadena de conexión
```json
"ConnectionStrings": {
  "SafeBand": "Server=(localdb)\\MSSQLLocalDB;Database=SafeBand;Trusted_Connection=True;TrustServerCertificate=True"
}
```
| Parte | Significado |
|---|---|
| `Server=(localdb)\\MSSQLLocalDB` | El SQL Server LocalDB que viene con Visual Studio (el `\\` es un `\` escapado en JSON) |
| `Database=SafeBand` | Nombre de la base de datos (la crea la primera migración) |
| `Trusted_Connection=True` | Entra con tu usuario de Windows, sin usuario/contraseña |
| `TrustServerCertificate=True` | Acepta el certificado local de SQL Server |

Está en `appsettings.Development.json` (no en `appsettings.json`) porque solo aplica en tu PC. En Azure se configura otra cadena, apuntando a Azure SQL, desde el portal — así las contraseñas de la nube nunca quedan en el código.

### `Data\AppDbContext.cs`
Es el puente entre el código y SQL Server.

- **`DbSet<Zona> Zonas`** (y los otros 3): cada `DbSet` es una tabla. Con `db.Zonas` consultas, agregas o borras zonas.
- **`OnModelCreating`**: reglas que EF no puede adivinar solo:

| Regla | Dónde | Para qué |
|---|---|---|
| `HasMaxLength(n)` | Textos | Sin esto, SQL crea `nvarchar(max)`. Con límite, la columna es más eficiente y evita datos absurdos |
| `HasConversion<string>()` | `Zona.Tipo` | Guarda el enum como texto (`"Salon"`) en vez de número (`0`). Más legible al ver la tabla en SQL |
| `HasIndex(...).IsUnique()` | `Zona.Nombre`, `Nodo.Codigo`, `Pulsera.IdentificadorBle`, `Pulsera.UidNfc` | No puede haber dos nodos `"nodo-1"` ni dos pulseras `"SB-0001"` |
| `HasFilter("[UidNfc] IS NOT NULL")` | `Pulsera.UidNfc` | El UID es único solo entre pulseras que ya tienen sticker; varias pueden estar sin UID (null) |
| `HasIndex(l => l.Timestamp)` y `(PulseraId, Timestamp)` | `LecturaBle` | Índices para que "últimas lecturas" y "lecturas de una pulsera" sean rápidas aunque haya millones |
| `OnDelete(DeleteBehavior.Restrict)` | Relaciones | Impide borrar una zona con nodos, o un nodo/pulsera con lecturas. Por defecto EF borraría en **cascada** (se perdería el historial). Para eso existe `Activo`/`Activa`: se desactiva en vez de borrar |

### `Data\DbSeeder.cs` — datos de prueba
Al arrancar la API **en desarrollo**, si la tabla `Zonas` está vacía, inserta:

| Tabla | Registro |
|---|---|
| `Zonas` | "Salón 3°A" (tipo `Salon`) |
| `Nodos` | `nodo-1` en esa zona, umbral -75 dBm |
| `Pulseras` | `SB-0001` (el celular con nRF Connect anunciará ese nombre) |

Se necesitan porque una lectura solo se puede guardar si el nodo y la pulsera ya existen (llaves foráneas).

- Se llama desde `Program.cs` dentro de `if (app.Environment.IsDevelopment())` → **nunca corre en Azure**.
- `if (await db.Zonas.AnyAsync()) return;` → si ya hay datos, no hace nada (no duplica).
- `Zona = salon` en el nodo: se asigna el objeto, y EF llena `ZonaId` solo al guardar.
- `SaveChangesAsync()` es donde realmente se ejecutan los `INSERT`.
- Para volver a sembrar desde cero: borrar los datos de las tablas (o la BD y correr `Update-Database`) y arrancar la API.

### `SafeBand.api.csproj` — paquetes instalados

| Paquete | Versión | Para qué |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | Genera la descripción de la API (`/openapi/v1.json`) |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.12 | Conecta EF Core con SQL Server |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | Permite crear migraciones |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.12 | Comandos `Add-Migration` / `Update-Database` en la Package Manager Console |
| `Swashbuckle.AspNetCore.SwaggerUI` | 10.2.3 | Página `/swagger` para probar la API desde el navegador |

### `Properties\launchSettings.json`
Perfiles de arranque. Al presionar F5 se abre el navegador en `/swagger` (`launchBrowser: true`, `launchUrl: "swagger"`).

| Perfil | URL |
|---|---|
| `http` | `http://localhost:5213` |
| `https` | `https://localhost:7001` (y también `http://localhost:5213`) |
| `red-local (ESP32)` | `http://0.0.0.0:5213` → escucha en **toda la red**, no solo en `localhost`. Es el que se usa para que un ESP32 alcance la API |

**Probar con el ESP32 en la red local:**
1. PC y ESP32 deben estar en la **misma red WiFi** (el ESP32 solo usa **2.4 GHz**; la PC puede estar en 5 GHz del mismo router).
2. Ejecutar la API con el perfil **red-local (ESP32)** (selector junto al botón ▶ de VS).
3. La primera vez, Windows pregunta si permite el acceso por el firewall → **Permitir en redes privadas**.
4. IP de la PC: `ipconfig` → *Dirección IPv4* del adaptador WiFi. La URL del nodo queda `http://<esa-IP>:5213/api/lecturas` (`menuconfig` → SafeBand - Nodo).
5. Comprobar desde el celular (misma red): abrir `http://<IP>:5213/` → debe verse la página de lecturas. Si no carga, es el firewall o la red.

La IP de la PC puede cambiar si se reconecta o se reinicia el router.

Se administran con: clic derecho al proyecto → **Manage NuGet Packages…**

---

## 5. Modelos (tablas)

Cada clase en `Models\` se convertirá en una tabla; cada propiedad, en una columna.
Convención: una propiedad llamada `Id` es la llave primaria autoincremental.

### `Models\Zona.cs` → tabla `Zonas`
Área física de la escuela.

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | int | Llave primaria (autoincremental) |
| `Nombre` | string | Ej. "Salón 3°A", "Patio" |
| `Tipo` | `TipoZona` | Tipo de área (ver enum abajo) |
| `Activa` | bool | Permite desactivar una zona sin borrarla. Empieza en `true` |
| `Nodos` | `List<Nodo>` | **No es columna.** Propiedad de navegación: la lista de nodos de esta zona |

**`enum TipoZona`**: `Salon`, `Patio`, `Tienda`, `Entrada`, `Perimetro`, `Otra`.
Lista cerrada para que no se guarden tipos inventados. La lógica futura lo usa:
- `Perimetro` → alerta si la pulsera sale sin recogida autorizada.
- `Salon` → pase de lista.
- `Tienda` → registro de consumo.

### `Models\Nodo.cs` → tabla `Nodos`
Cada ESP32 receptor que escanea BLE en una zona.

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | int | Llave primaria |
| `Codigo` | string | Nombre que el ESP32 manda en cada lectura, ej. `"nodo-1"` |
| `Descripcion` | string? | Opcional (el `?` = puede quedar vacío/null). Ej. "Arriba del pizarrón" |
| `ZonaId` | int | **Llave foránea** → `Zonas.Id`. Guarda en qué zona está el nodo |
| `Zona` | `Zona` | **No es columna.** Propiedad de navegación para acceder a la zona desde el nodo (`nodo.Zona.Nombre`) |
| `UmbralRssi` | int | RSSI mínimo (dBm) para decir "la pulsera está en esta zona". Default -75. Se ajusta por nodo al calibrar |
| `EsPortatil` | bool | Nodo con batería para salidas escolares |
| `Activo` | bool | Desactivar sin borrar |
| `UltimoContacto` | DateTime? | Última vez que mandó datos. Si pasa mucho tiempo → nodo apagado o sin WiFi |

### `Models\Pulsera.cs` → tabla `Pulseras`
La pulsera del niño. Tiene dos formas de identificarse: BLE (para saber en qué zona está) y NFC (para la tienda).

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | int | Llave primaria |
| `IdentificadorBle` | string | ID que la pulsera anuncia por BLE, ej. `"SB-0001"`. Es lo que el nodo lee y manda a la API |
| `UidNfc` | string? | UID del sticker NFC NTAG215 (opcional hasta que se pegue el sticker) |
| `AlumnoId` | int? | **Llave foránea** → `Alumnos.Id`. A quién pertenece. Opcional: una pulsera nueva puede no estar asignada |
| `Alumno` | `Alumno?` | **No es columna.** Navegación: `pulsera.Alumno.Nombres` |
| `Activa` | bool | Desactivar (ej. pulsera perdida) sin borrar su historial |
| `FechaAlta` | DateTime | Cuándo se registró |
| `UltimaLectura` | DateTime? | Última vez que un nodo la detectó (null = nunca) |

**¿Por qué `IdentificadorBle` y no la MAC?** Los celulares (usados para pruebas con nRF Connect) cambian su dirección MAC BLE cada cierto tiempo por privacidad. Un ID propio en el anuncio (nombre o manufacturer data) no cambia, y funciona igual con el celular y con la XIAO ESP32-C3.

**¿Por qué el estado actual (puesta, batería) no está aquí?** Porque ya está en cada lectura y la más reciente se obtiene rápido gracias al índice `(PulseraId, Timestamp)`. Copiarlo aquí duplicaría datos y podría quedar desincronizado. Se agregaría solo si una consulta resultara lenta (desnormalizar).

### `Models\Alumno.cs` → tabla `Alumnos`
El niño que usa la pulsera.

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | int | Llave primaria |
| `Nombres` | string (100) | Ej. "Ana Sofía" |
| `ApellidoPaterno` | string (100) | Obligatorio |
| `ApellidoMaterno` | string? (100) | Opcional: hay niños con un solo apellido |
| `FechaNacimiento` | `DateOnly?` | Solo fecha (sin hora). Columna SQL `date` |
| `Matricula` | string? (30) | Número que asigna la escuela. Opcional, pero **único** entre los que lo tienen |
| `Activo` | bool | Baja del alumno sin borrar su historial |
| `FechaAlta` | DateTime | Cuándo se registró |
| `Pulseras` | `List<Pulsera>` | **No es columna.** Navegación: las pulseras del alumno |

**¿Por qué el nombre del niño no va directo en `Pulseras`?** El niño tiene más datos (grupo, tutores, alergias) que le pertenecen a él, no a la pulsera; y la pulsera se puede reemplazar si se pierde sin que el niño pierda su historial.

**Datos que irán en otras tablas más adelante:** grupo (`Grupos`), tutores (relación muchos a muchos), alergias, foto (fase de recogida).

**Datos que no se guardan a propósito:** CURP, dirección, datos médicos detallados. La LFPDPPP pide guardar solo lo necesario, más aún con datos de menores.

### `Models\LecturaBle.cs` → tabla `LecturasBle`
Cada vez que un nodo detecta una pulsera. Es el dato base de todo el sistema: de aquí salen la ubicación por zona, el tiempo en cada zona, las alertas y el pase de lista. También es la tabla que cumple el requisito del checkpoint (id, variable = RSSI, valor, unidad = dBm, fecha/hora).

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | **long** | Llave primaria. `long` en vez de `int` porque esta tabla crece muy rápido (varias lecturas por segundo por pulsera) |
| `NodoId` / `Nodo` | int / navegación | Qué nodo hizo la detección (llave foránea → `Nodos`) |
| `PulseraId` / `Pulsera` | int / navegación | Qué pulsera fue detectada (llave foránea → `Pulseras`) |
| `Rssi` | int | Intensidad de señal en dBm. Siempre negativo: -40 = muy cerca, -90 = lejos |
| `Puesta` | bool | Reed switch: `true` = pulsera cerrada en la muñeca |
| `Sos` | bool | Botón de pánico presionado |
| `BateriaBaja` | bool | Batería de la pulsera bajo el umbral |
| `Timestamp` | DateTime | Hora de la detección en **UTC**. La manda el ESP32 (para lecturas guardadas sin internet); si no llega, la pone la API |

**¿Por qué UTC?** Es una hora universal sin zona horaria. Evita confusiones con horario de verano o si el servidor de Azure está en otro país. El frontend la convierte a hora local de México al mostrarla.

**¿Por qué `Nodo` y `Pulsera` no tienen una lista `Lecturas`?** A propósito. Serían millones de registros; si alguien accediera a `pulsera.Lecturas` por error, cargaría todo el historial a memoria. Las lecturas se consultan siempre directo desde la tabla con filtros (por pulsera, por fecha, con límite).

### Relaciones entre tablas

**Nodo 1 ── N LecturasBle** y **Pulsera 1 ── N LecturasBle** (cada lectura tiene un nodo y una pulsera). Solo navegación de la lectura hacia el nodo/pulsera, no al revés.

**Alumno 1 ── N Pulseras** (opcional: `AlumnoId` puede ser null). Si se borra un alumno, sus pulseras quedan sin asignar (`OnDelete(SetNull)`), no se borran.

Diagrama general:
```
Zonas ──< Nodos ──< LecturasBle >── Pulseras >── Alumnos
```

**Zona 1 ── N Nodos** (una zona tiene varios nodos; cada nodo está en una sola zona)

- La columna real es `Nodo.ZonaId` (llave foránea). EF Core la reconoce por convención: `<NombreDeLaClase>Id`.
- `Nodo.Zona` y `Zona.Nodos` son **propiedades de navegación**: no crean columnas, solo permiten moverse entre objetos en C# sin escribir JOINs.
- En SQL equivale a: `FOREIGN KEY (ZonaId) REFERENCES Zonas(Id)`.

---

## 6. Endpoints (API)

Un endpoint es una dirección de la API a la que se mandan o piden datos.
**Probarlos:** F5 → se abre `https://localhost:7001/swagger` → abrir un endpoint → **Try it out** → **Execute**.

### Organización
| Carpeta | Qué contiene |
|---|---|
| `Contracts\` | *Contratos*: la forma del JSON que entra (request) y sale (response). Son distintos de los modelos para no exponer las tablas tal cual y para validar la entrada |
| `Endpoints\` | Las rutas agrupadas por tema. Cada archivo tiene un método `MapXxx()` que se llama desde `Program.cs` |

### `/api/lecturas` — `Endpoints\LecturasEndpoints.cs`

| Método | Ruta | Quién lo usa | Qué hace |
|---|---|---|---|
| `POST` | `/api/lecturas` | ESP32 (nodo) | Valida y guarda una lectura |
| `GET` | `/api/lecturas?limit=N` | Frontend | Devuelve las últimas N lecturas (default 100, máx. 1000), más recientes primero. `limit=1` = la última |

**JSON que manda el ESP32** (`CrearLecturaRequest`):
```json
{ "nodo": "nodo-1", "pulsera": "SB-0001", "rssi": -67, "puesta": true, "sos": false, "bateriaBaja": false }
```
- Obligatorios: `nodo`, `pulsera`, `rssi` (entre -127 y 0).
- Opcionales: `puesta` (default true), `sos` y `bateriaBaja` (default false), `timestamp` con zona horaria (ej. `"2026-09-29T18:30:00-06:00"`). Si no llega `timestamp`, se usa la hora del servidor.

**Pasos del POST:**
1. Busca el nodo por `Codigo` y la pulsera por `IdentificadorBle`. Si no existen o están inactivos → 400.
2. Toma la hora (del ESP32 convertida a UTC, o la actual). Si está en el futuro → 400.
3. Guarda la lectura y actualiza `Nodo.UltimoContacto` y `Pulsera.UltimaLectura`.
4. Responde **201 Created** con la lectura guardada (`LecturaResponse`).

**Respuesta** (`LecturaResponse`):
```json
{ "id": 1, "nodo": "nodo-1", "zona": "Salón 3°A", "pulsera": "SB-0001", "alumno": null,
  "rssi": -67, "unidad": "dBm", "puesta": true, "sos": false, "bateriaBaja": false,
  "timestamp": "2026-09-30T02:58:38Z" }
```
La `Z` al final de la hora significa UTC; el frontend la convierte a hora local.

### Respuestas de error (lo que pide el checkpoint: "responder de forma comprensible ante un dato incorrecto")

| Caso | Código | Respuesta |
|---|---|---|
| Falta un campo obligatorio | 400 | `errors: { "Pulsera": ["El campo 'pulsera' es obligatorio."] }` |
| RSSI fuera de rango | 400 | `errors: { "Rssi": ["El campo 'rssi' debe estar entre -127 y 0 dBm."] }` |
| Tipo incorrecto (`"rssi": "abc"`) | 400 | `{ "error": "El campo 'rssi' tiene un valor o tipo de dato inválido." }` |
| JSON mal formado | 400 | `{ "error": "El cuerpo de la petición no es un JSON válido." }` |
| Nodo o pulsera no registrados | 400 | `{ "error": "El nodo 'nodo-9' no está registrado o está inactivo." }` |
| `limit` fuera de rango | 400 | `{ "error": "El parámetro 'limit' debe estar entre 1 y 1000." }` |
| Error inesperado del servidor | 500 | `{ "error": "Error interno del servidor." }` |

Las validaciones de campos (`[Required]`, `[Range]`) están como atributos en `Contracts\LecturaContracts.cs` y las aplica `AddValidation()` automáticamente antes de entrar al endpoint.

---

## 6b. Frontend (página de lecturas)

**Abrir:** con la API corriendo, ir a `https://localhost:7001/` (o `http://localhost:5213/`).

### Por qué está en `wwwroot\`
ASP.NET Core sirve automáticamente los archivos de `wwwroot\` gracias a dos líneas en `Program.cs`:
```csharp
app.UseDefaultFiles();   // "/" → index.html
app.UseStaticFiles();    // sirve los archivos de wwwroot
```
Ventajas: la página y la API están en la misma dirección (no hace falta configurar CORS) y al desplegar en Azure se suben juntas.

### Archivos
| Archivo | Qué hace |
|---|---|
| `index.html` | Estructura: aviso de error (oculto), tarjeta "Última lectura", tabla "Historial" |
| `app.js` | Hace `fetch("/api/lecturas?limit=50")` al cargar y **cada 5 segundos**. La primera lectura del arreglo es la más reciente |
| `styles.css` | Colores y diseño. Se adapta a celular (la tabla tiene scroll horizontal) |

### Qué muestra
- **Última lectura:** RSSI grande, nivel de señal (fuerte ≥ -60, media ≥ -75, débil < -75 dBm), fecha/hora local, pulsera (y alumno si tiene), nodo y zona, y etiquetas de estado.
- **Historial:** tabla con las últimas 50 lecturas.
- **Etiquetas de estado:** `Puesta` (verde) / `Quitada` (naranja), `SOS` (rojo), `Batería baja` (naranja).

### Estados (requisito del checkpoint)
| Estado | Qué se ve |
|---|---|
| Cargando | "Cargando…" al abrir la página |
| Sin datos | "Sin datos todavía. Esperando la primera lectura del nodo…" |
| Error de conexión | Aviso rojo arriba "No se pudo conectar con la API…". Se conservan los últimos datos y se reintenta cada 5 s; al volver la API, el aviso desaparece solo |

### Detalles técnicos
- La hora llega en UTC (`...Z`) y `toLocaleString("es-MX")` la convierte a la hora local del navegador.
- Los datos se insertan con `textContent` (no `innerHTML`) para que ningún texto se interprete como código (seguridad).
- `API_LECTURAS` y `INTERVALO_MS` están al inicio de `app.js` por si hay que cambiarlos.

---

## 6c. Firmware (ESP32)

**Herramientas:** VS Code + extensión ESP-IDF, **ESP-IDF v6.0.2** (instalado en `C:\esp\v6.0.2\esp-idf`).
**Placas:** 3 × ESP32 clásico ("ESP32 WiFi+BT SoC Inside"). LED integrado en GPIO 2, botón BOOT en GPIO 0.

### Plan de hardware de prueba
| Placa | Firmware | Papel |
|---|---|---|
| ESP32 #1 | `firmware\pulsera` | Simula la pulsera `SB-0001` |
| ESP32 #2 | `firmware\nodo` | `nodo-1` en "Salón 3°A" |
| ESP32 #3 | `firmware\nodo` (mismo programa) | `nodo-2` en "Patio" (hay que darlo de alta en la BD) |

**Un programa por *tipo* de dispositivo, no por dispositivo:** todos los nodos usan `firmware\nodo`; solo cambia el código del nodo al grabar (`menuconfig`). Lo mismo aplicará a las terminales de tienda y de recogida cuando lleguen.

### `firmware\pulsera` — pulsera simulada
Anuncia por BLE su nombre (`SB-0001`) y su estado. No se conecta a WiFi.

| Archivo | Qué es |
|---|---|
| `CMakeLists.txt` | Define el proyecto ESP-IDF |
| `sdkconfig.defaults` | Activa Bluetooth con NimBLE, solo BLE |
| `main\main.c` | El programa |
| `main\CMakeLists.txt` | Archivos y componentes que usa (`bt`, `nvs_flash`, `esp_driver_gpio`) |
| `main\Kconfig.projbuild` | Opción configurable: **identificador de la pulsera** (default `SB-0001`). Se cambia en `idf.py menuconfig` → *SafeBand - Pulsera* |

**Conexiones para simular (sin hardware extra):**
| Función real | Simulación | Pin |
|---|---|---|
| Botón SOS | Botón **BOOT** presionado **2 s** → SOS activo **30 s** | GPIO 0 |
| Reed switch (puesta) | **Jumper a GND** = puesta; sin jumper = quitada | GPIO 25 |
| Batería baja | **Jumper a GND** = batería baja | GPIO 26 |
| LED de estado | Fijo = SOS · parpadeo = quitada · apagado = normal | GPIO 2 |

**Qué anuncia (máx. 31 bytes por anuncio BLE):**
- **Nombre:** `SB-0001` → así el nodo sabe qué pulsera es.
- **Manufacturer data (4 bytes):** `FF FF 01 EE`
  - `FF FF`: ID de compañía reservado para pruebas.
  - `01`: versión del formato.
  - `EE`: byte de estado → bit0 = puesta, bit1 = SOS, bit2 = batería baja.
  - Ejemplos: `01` = puesta normal · `00` = quitada · `03` = puesta + SOS · `05` = puesta + batería baja.

**Intervalo de anuncio:** 500 ms normal; **100 ms durante SOS** (para que el nodo lo capte más rápido). El anuncio solo se reinicia cuando cambia el estado.

**Por qué NimBLE y no Bluedroid:** NimBLE es el stack BLE ligero de ESP-IDF; usa menos memoria y es el mismo que usará la XIAO ESP32-C3.

### Compilar y grabar (terminal de ESP-IDF en VS Code, dentro de `firmware\pulsera`)
```
idf.py set-target esp32      ← solo la primera vez
idf.py build                 ← compilar
idf.py -p COM5 flash monitor ← grabar y ver los mensajes (cambiar COM5 por tu puerto)
```
Salir del monitor: `Ctrl + ]`.

### Verificar con el celular
nRF Connect → **Scanner** → debe aparecer `SB-0001`. Al tocarlo se ve el *Manufacturer data* `0xFFFF 0x01XX`.

### `firmware\nodo` — nodo receptor
Escanea BLE, promedia el RSSI de cada pulsera en una ventana y manda **una lectura por pulsera** a la API por WiFi. El SOS se manda **de inmediato**.

| Archivo | Qué es |
|---|---|
| `main\main.c` | El programa |
| `main\Kconfig.projbuild` | Opciones configurables (ver abajo) |
| `sdkconfig.defaults` | BLE (NimBLE), coexistencia WiFi+BLE y partición de 1.5 MB (con WiFi+BLE+HTTP el programa pesa 1.09 MB y no cabía en la de 1 MB) |

**Configuración** (`idf.py menuconfig` → *SafeBand - Nodo*). Se guarda en `sdkconfig`, que **no se sube a GitHub** (ahí queda la contraseña del WiFi):

| Opción | Ejemplo | Nota |
|---|---|---|
| Código del nodo | `nodo-1` / `nodo-2` | Debe existir en la tabla `Nodos`. Es lo único que cambia entre nodos |
| SSID / contraseña WiFi | | El ESP32 solo conecta a redes de **2.4 GHz** |
| URL de la API | `http://192.168.1.50:5213/api/lecturas` | Local: IP de la PC. Azure: `https://<app>.azurewebsites.net/api/lecturas` |
| Ventana de promedio | `10` s | Cuánto acumula antes de mandar |

**Cómo funciona (3 tareas):**
| Tarea | Qué hace |
|---|---|
| NimBLE (escaneo) | Por cada anuncio, si el nombre empieza con `SB-` y el manufacturer data es `FF FF 01 EE`, suma el RSSI en una tabla (máx. 16 pulseras). Si trae SOS, lo pone en la cola de inmediato (máx. 1 aviso cada 5 s por pulsera) |
| `tarea_ventana` | Cada ventana calcula el **promedio** por pulsera y lo pone en la cola |
| `tarea_envio` | Saca de la cola y hace `POST /api/lecturas` con el JSON. Registra la respuesta y **cuántos ms tardó** (sirve para la prueba de latencia del checkpoint) |

**Detalles de diseño:**
- **Escaneo pasivo, sin filtro de duplicados:** la pulsera no responde a escaneos activos y se necesitan todos los anuncios para promediar.
- **Escucha 50 ms de cada 100 ms:** deja tiempo de antena al WiFi (comparten la misma radio).
- **Sin WiFi:** la lectura se descarta con un aviso. *Pendiente (mejora):* guardar en cola y reenviar con su hora original al reconectar (por eso la API acepta `timestamp`).
- **Hora:** el nodo no manda `timestamp`; la asigna la API al recibir.

**Mensajes del monitor:** `WiFi conectado, IP ...` · `Ventana: SB-0001 promedio -63 dBm (18 anuncios)` · `OK SB-0001 rssi=-63 (85 ms)` · `¡SOS de SB-0001!` · `La API rechazó ... HTTP 400`.

---

## 7. Migraciones (crear y cambiar la base de datos)

Una migración es un archivo de C# que describe un cambio en la base de datos (crear tablas, agregar columnas...). Funciona como un "commit" de la BD.

### Comandos (Package Manager Console)
Abrir: **Tools → NuGet Package Manager → Package Manager Console**. Verificar que *Default project* sea `SafeBand.api`. Detener la API antes.

| Comando | Qué hace |
|---|---|
| `Add-Migration NombreDelCambio` | Compara los modelos contra el último snapshot y **genera** el archivo de migración. No toca SQL Server |
| `Update-Database` | **Aplica** a SQL Server las migraciones pendientes |
| `Remove-Migration` | Borra la última migración **si todavía no se aplicó** (para corregir un error) |
| `Update-Database NombreMigracion` | Regresa la BD a esa migración (ejecuta los `Down()` de las posteriores) |

### Flujo cuando cambias un modelo
1. Modificas o agregas clases en `Models\` (y su configuración en `AppDbContext`).
2. `Add-Migration DescripcionDelCambio` → revisar el archivo generado.
3. `Update-Database` → la BD se actualiza, los datos existentes se conservan.

### Archivos en `Migrations\`
| Archivo | Qué es |
|---|---|
| `<fecha>_<Nombre>.cs` | La migración: `Up()` aplica el cambio, `Down()` lo deshace. El número es fecha/hora UTC para mantener el orden |
| `<fecha>_<Nombre>.Designer.cs` | Metadatos internos de EF |
| `AppDbContextModelSnapshot.cs` | "Foto" del modelo actual; EF la compara en la siguiente migración para saber qué cambió |

Ninguno se edita a mano. Todos se suben a GitHub.

### Tabla `__EFMigrationsHistory`
EF la crea sola en la BD y anota ahí cada migración aplicada. Así `Update-Database` sabe cuáles faltan.

### Migraciones aplicadas
| Migración | Contenido |
|---|---|
| `20260929153232_Fase1_Inicial` | Tablas `Zonas`, `Nodos`, `Pulseras`, `LecturasBle` con llaves foráneas (borrado restringido) e índices |
| `20260930025056_AgregarAlumnos` | Tabla `Alumnos`; columna `AlumnoId` (nullable) en `Pulseras` con llave foránea `FK_Pulseras_Alumnos_AlumnoId` |

---

## 8. Git y GitHub

| Qué | Valor |
|---|---|
| Repositorio en GitHub | https://github.com/FernandoSibaja/SafeBand |
| Carpeta del repositorio local | `C:\SafeBand\SafeBand.api` |
| Rama principal | `main` |
| Cuenta para subir | `FernandoSibaja` (configurada en la URL del remoto: `https://FernandoSibaja@github.com/...`, porque en esta PC también hay sesión de otra cuenta) |

`bin\`, `obj\` y `.vs\` no se suben (los excluye `.gitignore`).

### Subir cambios (cada vez que algo funcione)

**Terminal** (en `C:\SafeBand\SafeBand.api`):
```
git status                               ← ver qué cambió
git add .                                ← elegir todos los cambios
git commit -m "Agrega endpoint X"        ← guardar en tu PC con un mensaje
git push                                 ← subir a GitHub
```

**Visual Studio:** View → Git Changes → escribir mensaje → **Commit All** → **Push** (↑).

### Mensajes de commit
Una línea corta que empieza con verbo y dice qué cambió: `Agrega tabla Alumnos`, `Corrige validación de RSSI`. Evitar `cambios`, `ya funciona`.

### Problema conocido
`403 Permission denied to onitsibaja-crypto` → Git usó la otra cuenta guardada en la PC. Se resolvió con:
```
git remote set-url origin https://FernandoSibaja@github.com/FernandoSibaja/SafeBand.git
```

---

## 9. Pendientes / siguientes pasos

- [x] Crear proyecto ASP.NET Core Web API (.NET 10, Minimal API)
- [x] Instalar paquetes de EF Core
- [x] Modelo `Zona`
- [x] Modelo `Nodo` (ESP32 receptor, pertenece a una zona)
- [x] Modelo `Pulsera` (sin alumno todavía)
- [x] Modelo `LecturaBle` (cada detección: RSSI, estado, hora)
- [x] `AppDbContext` + cadena de conexión a LocalDB
- [x] Primera migración → crear la base de datos
- [x] Datos iniciales de prueba (zona, `nodo-1`, pulsera `SB-0001`)
- [x] Modelo `Alumno` + `AlumnoId` en `Pulsera`
- [x] Migración `AgregarAlumnos` (`Add-Migration` + `Update-Database`)
- [x] Endpoint `POST /api/lecturas` (guardar)
- [x] Endpoint `GET /api/lecturas` (consultar)
- [x] Probar con Swagger (POST válido → 201, POST con `rssi: 25` → 400, GET → 200 con 2 lecturas)
- [x] Subir a GitHub
- [x] Frontend: página con última lectura, historial y estados cargando / sin datos / error
- [x] Firmware de la pulsera simulada (compila; falta grabar y probar con nRF Connect)
- [x] Firmware del nodo (BLE → promedio → WiFi → POST) — compila; falta grabar y probar
- [ ] Alta de zona "Patio" y `nodo-2` en la BD
- [ ] API escuchando en la red local (para que el ESP32 la alcance)
- [x] Nodo enviando lecturas reales a la API por WiFi (red local)
- [ ] Desplegar en Azure (App Service + Azure SQL)
- [ ] **Pedido del profesor:** prueba de alcance BLE (modo calibración en el nodo; 1-20 m, con pared y con cuerpo) → obtener `RSSI_a_1m` y `n`
- [ ] **Pedido del profesor:** triangulación → X/Y en `Nodos`, cálculo de posición en la API, `GET /api/ubicaciones`, página de mapa con marcadores
- [ ] Prototipo alfa: eventos + latido, guardar y reenviar sin WiFi, nodos puente con ESP-NOW
- [ ] Pruebas del checkpoint: 10 envíos, latencia, dato incorrecto, pérdida de conexión
- [ ] Endpoints de administración: alumnos, pulseras (asignar a alumno), nodos, zonas
- [ ] Después: alumnos, tutores, grupos, alertas, tienda, recogida, PWA

---

## 10. Bitácora

| Fecha | Qué se hizo |
|---|---|
| 2026-09-26 | Proyecto creado en VS 2026 (ASP.NET Core Web API, .NET 10, sin controllers). Probado con F5 → `/weatherforecast` responde. |
| 2026-09-28 | Instalados paquetes EF Core 10.0.12; actualizado OpenApi a 10.0.12 (quita vulnerabilidad). Decidido Code First. Creado `Models\Zona.cs`. Creado este manual. |
| 2026-09-28 | Creado `Models\Nodo.cs` con relación Zona 1─N Nodos (se agregó `Nodos` a `Zona`). |
| 2026-09-28 | Creado `Models\Pulsera.cs` (identificador BLE + UID NFC). La relación con alumno queda para cuando exista `Alumno`. |
| 2026-09-28 | Creado `Models\LecturaBle.cs` (relación con Nodo y Pulsera). Terminados los 4 modelos iniciales. |
| 2026-09-28 | Creado `Data\AppDbContext.cs` (tablas, longitudes, índices únicos, borrado restringido). Cadena de conexión a LocalDB en `appsettings.Development.json`. Registrado en `Program.cs`. |
| 2026-09-29 | `Add-Migration Fase1_Inicial` + `Update-Database`: creada la base `SafeBand` en LocalDB con las 4 tablas + `__EFMigrationsHistory`. |
| 2026-09-29 | Decidido: el nodo promedia lecturas (~10 s) para no saturar la BD. Creado `Data\DbSeeder.cs`; al arrancar se insertaron zona "Salón 3°A", `nodo-1` y pulsera `SB-0001`. |
| 2026-09-29 | Decidido no copiar el estado (puesta/batería) en `Pulseras`. Creado `Models\Alumno.cs`; `Pulsera` ahora tiene `AlumnoId` (opcional). Configurado en `AppDbContext`. |
| 2026-09-29 | Migración `AgregarAlumnos` aplicada. Los datos existentes se conservaron (la pulsera `SB-0001` quedó con `AlumnoId` = null). |
| 2026-09-29 | Endpoints `POST` y `GET /api/lecturas` con validación y errores claros. Instalado Swagger UI (F5 abre `/swagger`). Quitados `/weatherforecast` y la redirección a HTTPS. Probado: 1 lectura válida guardada (id 1) y 5 casos de error → 400. |
| 2026-09-29 | Probado por Fernando en Swagger: POST válido (id 2) → 201; `rssi: 25` → 400; GET → 200 con las 2 lecturas, más reciente primero. |
| 2026-09-29 | Creado repositorio Git (rama `main`) y subido a GitHub `FernandoSibaja/SafeBand`. Resuelto error 403 por cuenta equivocada. Agregada sección Git al manual. |
| 2026-09-29 | Frontend en `wwwroot\` (index.html, app.js, styles.css) servido por la API en `/`. Se actualiza cada 5 s. Probado: muestra las 2 lecturas; al apagar la API aparece el aviso de error y conserva los datos. |
| 2026-09-29 | Decidido usar los 3 ESP32 clásicos: 1 pulsera simulada + 2 nodos. Creado `firmware\pulsera` (NimBLE, anuncia `SB-0001` + byte de estado; BOOT = SOS, jumpers = puesta / batería baja). Compila con ESP-IDF v6.0.2 sin errores. |
| 2026-09-30 | Creado `firmware\nodo` (un solo programa para todos los nodos): escanea `SB-*`, promedia RSSI por ventana de 10 s, POST a la API; SOS inmediato. Compila; partición ampliada a 1.5 MB. |
| 2026-09-30 | Nodo conectado: muestra el motivo de desconexión WiFi; perfil `red-local (ESP32)` en la API. Problemas resueltos: SSID mal escrito, IP de ejemplo, red Pública en Windows, punto final en la URL (404). Lecturas reales llegando. |
| 2026-10-01 | Decisiones de arquitectura: BLE en pulsera, WiFi/ESP-NOW en nodos, un nodo por zona, eventos + latido a futuro, seguir con ESP-IDF, mapa con posición. Pedidos del profesor anotados: alcance BLE y triangulación. |
