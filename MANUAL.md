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

---

## 3. Estructura del proyecto

```
SafeBand.api\
├── SafeBand.api.slnx              ← la solución
├── MANUAL.md                      ← este manual
└── SafeBand.api\                  ← el proyecto
    ├── Program.cs                 ← punto de arranque de la API
    ├── appsettings.json           ← configuración general
    ├── appsettings.Development.json ← configuración solo para tu PC
    ├── SafeBand.api.csproj        ← definición del proyecto y paquetes NuGet
    ├── SafeBand.api.http          ← peticiones de prueba desde VS
    ├── Properties\
    │   └── launchSettings.json    ← puertos y perfiles de arranque (http / https)
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

## 8. Pendientes / siguientes pasos

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
- [ ] Endpoints de administración: alumnos, pulseras (asignar a alumno), nodos, zonas
- [ ] Después: alumnos, tutores, grupos, alertas, tienda, recogida, PWA

---

## 9. Bitácora

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
