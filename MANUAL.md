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
    │   ├── index.html             ← panel SafeBand (login + secciones)
    │   ├── css\app.css            ← diseño del panel (colores, tipografía, estructura)
    │   ├── js\
    │   │   ├── app.js             ← arranque: sesión, menú, navegación entre secciones
    │   │   ├── api.js             ← conexión con la API (JSON, cookie, mensajes de error)
    │   │   ├── ui.js              ← ayudantes: crear elementos, fechas, barras de señal, íconos
    │   │   └── vistas\            ← una pantalla por archivo
    │   │       ├── login.js       ← inicio de sesión
    │   │       ├── vivo.js        ← "En vivo": última detección + historial
    │   │       └── pendiente.js   ← temporal para secciones aún no construidas
    │   ├── monitor.html           ← página simple de lecturas SIN login (respaldo para el checkpoint)
    │   ├── monitor.js
    │   └── monitor.css
    ├── Contracts\
    │   ├── LecturaContracts.cs    ← forma del JSON que entra y sale de /api/lecturas
    │   ├── AlumnoContracts.cs     ← JSON de /api/alumnos
    │   ├── PulseraContracts.cs    ← JSON de /api/pulseras
    │   ├── TutorContracts.cs      ← JSON de /api/tutores
    │   ├── ZonaNodoContracts.cs   ← JSON de /api/zonas y /api/nodos
    │   └── AuthContracts.cs       ← JSON de inicio de sesión
    ├── Endpoints\
    │   ├── LecturasEndpoints.cs   ← POST y GET de /api/lecturas
    │   ├── AlumnosEndpoints.cs    ← administración de alumnos
    │   ├── PulserasEndpoints.cs   ← administración de pulseras y asignación a alumnos
    │   ├── TutoresEndpoints.cs    ← administración de tutores y vínculo con sus hijos
    │   ├── ZonasNodosEndpoints.cs ← administración de zonas y nodos
    │   └── AuthEndpoints.cs       ← iniciar sesión, cerrar sesión, ¿quién soy?
    ├── Data\
    │   ├── AppDbContext.cs        ← conexión entre los modelos y SQL Server
    │   └── DbSeeder.cs            ← datos de prueba (solo en tu PC)
    ├── Migrations\                ← historial de cambios de la BD (generado por EF, no se edita)
    └── Models\                    ← clases que se convierten en tablas
        ├── Zona.cs
        ├── Nodo.cs
        ├── Alumno.cs
        ├── Tutor.cs
        ├── TutorAlumno.cs
        ├── Usuario.cs             ← cuenta para iniciar sesión (Identity)
        ├── Roles.cs               ← nombres de los roles
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
2. **Procesamiento**: manejador de errores (JSON mal formado → 400 con mensaje claro; error inesperado → 500), OpenAPI + Swagger UI (**también en Azure**, como evidencia), y al arrancar: migraciones + datos iniciales.
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

### Al arrancar la API (en tu PC y en Azure)
En `Program.cs`:
```csharp
await db.Database.MigrateAsync();   // aplica migraciones pendientes → crea/actualiza tablas solo
await DbSeeder.SembrarAsync(db);    // datos iniciales si la BD está vacía
```
Por eso en Azure **no hace falta correr `Update-Database`** contra la nube: la primera vez que arranca, crea las tablas y los datos.

`EnableRetryOnFailure()` en `AddDbContext`: si la BD gratuita de Azure está "dormida", reintenta en vez de fallar a la primera.

### `Data\DbSeeder.cs` — datos iniciales
Al arrancar la API (**en tu PC y en Azure**), si la tabla `Zonas` está vacía, inserta:

| Tabla | Registro |
|---|---|
| `Zonas` | "Salón 3°A" (tipo `Salon`) |
| `Nodos` | `nodo-1` en esa zona, umbral -75 dBm |
| `Pulseras` | `SB-0001` (el celular con nRF Connect anunciará ese nombre) |

Se necesitan porque una lectura solo se puede guardar si el nodo y la pulsera ya existen (llaves foráneas).

- Se llama desde `Program.cs` al arrancar, **también en Azure** (para que el ESP32 pueda mandar lecturas de `nodo-1`/`SB-0001` a la nube desde el inicio).
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
| `Tutores` | `List<TutorAlumno>` | **No es columna.** Navegación: sus tutores |

**¿Por qué el nombre del niño no va directo en `Pulseras`?** El niño tiene más datos (grupo, tutores, alergias) que le pertenecen a él, no a la pulsera; y la pulsera se puede reemplazar si se pierde sin que el niño pierda su historial.

**Datos que irán en otras tablas más adelante:** grupo (`Grupos`), alergias, foto (fase de recogida).

### `Models\Tutor.cs` → tabla `Tutores`
Padre, madre o tutor legal. Lo da de alta **la escuela**; después el tutor crea su cuenta con un código de invitación.

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | int | Llave primaria |
| `Nombres`, `ApellidoPaterno` | string (100) | Obligatorios |
| `ApellidoMaterno` | string? (100) | Opcional |
| `Email` | string (200) | Correo con el que se registrará. **Único**. Se guardará en minúsculas |
| `Telefono` | string? (20) | Opcional |
| `Activo` | bool | Desactivar sin borrar |
| `FechaAlta` | DateTime | Cuándo se registró |
| `Alumnos` | `List<TutorAlumno>` | **No es columna.** Navegación: sus hijos |

### `Models\TutorAlumno.cs` → tabla `TutoresAlumnos`
Vínculo **muchos a muchos**: un tutor tiene varios hijos y un alumno tiene varios tutores. **Es lo que decide qué niños ve cada padre en la app.**

| Propiedad | Tipo | Descripción |
|---|---|---|
| `TutorId` + `AlumnoId` | int + int | **Llave primaria compuesta** (los dos juntos): el mismo vínculo no puede repetirse. Cada uno es además llave foránea |
| `Parentesco` | string? (50) | "Madre", "Padre", "Abuela"... |
| `EsContactoPrincipal` | bool | A quién se avisa primero ante una alerta |
| `PuedeRecoger` | bool | Si puede recoger al alumno (se usará en la terminal de recogida). Default `true` |

**Borrado:** si se borra un tutor o un alumno, se borran sus vínculos (`Cascade`), pero no el otro lado.

### Usuarios y roles — ASP.NET Core Identity

**Conceptos:**
- **Autenticación** = ¿quién eres? (correo + contraseña).
- **Autorización** = ¿qué puedes hacer? (según tu rol).
- **Identity** es el sistema de usuarios de .NET: guarda las contraseñas **cifradas** (hash PBKDF2, ni viendo la BD se pueden leer), bloquea cuentas tras intentos fallidos y maneja roles.
- La API reconocerá al usuario con una **cookie** cifrada (`HttpOnly`: JavaScript no la puede leer). Es lo más seguro cuando la página y la API están en el mismo sitio.
- **Los nodos ESP32 no inician sesión**: tendrán una clave propia (paso 6).

**`Models\Usuario.cs`** → tabla `AspNetUsers`. Hereda de `IdentityUser` (que ya trae `Email`, `UserName`, `PasswordHash`, bloqueo, etc.) y agrega:

| Propiedad | Tipo | Descripción |
|---|---|---|
| `NombreMostrar` | string (150) | Nombre que se ve en la app |
| `Activo` | bool | Desactivar la cuenta |
| `FechaAlta` | DateTime | |
| `TutorId` | int? | Si la cuenta es de un padre: a qué `Tutor` corresponde → por ahí se sabe qué hijos ve. Único (un tutor, una cuenta) |

**`Models\Roles.cs`**: constantes `Administrador`, `Maestro`, `Padre`.

**`AppDbContext`** ahora hereda de `IdentityDbContext<Usuario>`, que agrega las tablas de Identity:

| Tabla | Qué guarda |
|---|---|
| `AspNetUsers` | Las cuentas (con la contraseña cifrada) |
| `AspNetRoles` | Los 3 roles |
| `AspNetUserRoles` | Qué rol tiene cada usuario |
| `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens` | Datos internos de Identity (no se usan directamente) |

**Reglas configuradas en `Program.cs`:**
| Regla | Valor |
|---|---|
| Contraseña | Mínimo 8 caracteres, con mayúscula, minúscula y número |
| Bloqueo | 5 intentos fallidos → 15 minutos bloqueada |
| Correo | Único por cuenta |

**Administrador inicial:** al arrancar, la API crea los 3 roles y, si no hay ningún administrador, crea uno con `AdminInicial:Email` y `AdminInicial:Password` de la configuración. **La contraseña nunca va en el código ni en GitHub:**
- **En tu PC → "User Secrets":** Visual Studio → clic derecho al proyecto → **Manage User Secrets** → se abre `secrets.json`:
  ```json
  { "AdminInicial": { "Email": "tu-correo@uabc.edu.mx", "Password": "TuContraseña123" } }
  ```
  Ese archivo vive **fuera del proyecto** (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`), así que nunca se sube.
- **En Azure:** se configura en la App Service → *Environment variables* (`AdminInicial__Email`, `AdminInicial__Password`).
- Si falta la configuración, la API arranca igual y avisa en la consola.

### Seguridad: quién ve a qué niño
- **El padre NO elige qué pulsera ver.** Si bastara con escribir "SB-0001", cualquiera podría ver dónde está cualquier niño.
- **La escuela vincula** al tutor con sus hijos (`TutoresAlumnos`) y genera un **código de invitación de un solo uso**. El padre se registra con su correo + contraseña + ese código, y su cuenta queda ligada solo a sus hijos. Al registrarse acepta el aviso de privacidad (LFPDPPP).
- El filtro se hace **en la API**, no en la página: aunque alguien llame la API directamente, solo recibe datos de sus hijos.

| Rol | Ve | Puede |
|---|---|---|
| Padre / tutor | Solo sus hijos | Ver y recibir alertas |
| Maestro | Su grupo | Pase de lista |
| Administrador / director | Todo | Altas de alumnos, pulseras, nodos y tutores; generar códigos |

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

### `/api/alumnos` — `Endpoints\AlumnosEndpoints.cs`
Administración de alumnos. **Por ahora abiertos (sin login)**; se protegerán para el rol Administrador en el paso de usuarios.

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `GET` | `/api/alumnos` | Lista de alumnos **activos**, ordenados por apellido. `?incluirInactivos=true` incluye los dados de baja | 200 |
| `GET` | `/api/alumnos/{id}` | Un alumno | 200 / 404 |
| `POST` | `/api/alumnos` | Alta | 201 / 400 (validación) / 409 (matrícula repetida) |
| `PUT` | `/api/alumnos/{id}` | Edita todos sus datos | 204 / 400 / 404 / 409 |
| `DELETE` | `/api/alumnos/{id}` | **Baja lógica**: `Activo = false`, no se borra | 204 / 404 |

**JSON para alta/edición** (`GuardarAlumnoRequest`):
```json
{ "nombres": "Ana Sofía", "apellidoPaterno": "López", "apellidoMaterno": "García",
  "fechaNacimiento": "2018-05-14", "matricula": "A-0001" }
```
Obligatorios: `nombres`, `apellidoPaterno`. La fecha va como `"AAAA-MM-DD"`.

**Respuesta** (`AlumnoResponse`): sus datos + `pulseras` (lista de IDs BLE de sus pulseras activas, ej. `["SB-0001"]`).

**Códigos HTTP usados (convención REST):**
| Código | Significado |
|---|---|
| 200 OK | Consulta exitosa |
| 201 Created | Se creó (y el header `Location` dice dónde) |
| 204 No Content | Se editó o dio de baja; no hay nada que devolver |
| 400 Bad Request | Datos inválidos |
| 404 Not Found | No existe ese Id |
| 409 Conflict | Choca con algo existente (matrícula repetida) |

**¿Por qué DELETE no borra?** El alumno tiene historial (lecturas de su pulsera, en el futuro asistencias y consumos). Borrarlo perdería ese historial; desactivarlo lo saca de las listas y lo conserva.

### `/api/pulseras` — `Endpoints\PulserasEndpoints.cs`
Administración de pulseras. **Por ahora abiertos (sin login).**

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `GET` | `/api/pulseras` | Pulseras activas. `?sinAsignar=true` solo las libres; `?incluirInactivas=true` también las dadas de baja | 200 |
| `GET` | `/api/pulseras/{id}` | Una pulsera | 200 / 404 |
| `POST` | `/api/pulseras` | Alta (queda sin asignar) | 201 / 400 / 409 |
| `PUT` | `/api/pulseras/{id}` | Edita identificador BLE / UID NFC | 204 / 400 / 404 / 409 |
| `PUT` | `/api/pulseras/{id}/alumno` | **Asigna** a un alumno: `{ "alumnoId": 3 }`. **Quita** la asignación: `{ "alumnoId": null }` | 204 / 400 / 404 / 409 |
| `DELETE` | `/api/pulseras/{id}` | **Baja lógica** (perdida o dañada): `Activa = false`, conserva lecturas | 204 / 404 |

**JSON de alta/edición:**
```json
{ "identificadorBle": "SB-0002", "uidNfc": "04A1B2C3D4E5F6" }
```
- `identificadorBle`: **formato `SB-XXXX`**, máx. 15 caracteres (los nodos solo escuchan el prefijo `SB-` y su firmware admite hasta 15). Se guarda en mayúsculas. Único.
- `uidNfc`: opcional, **hexadecimal**. Acepta separadores (`04:A1:B2...`) y los quita. Único.

**Reglas de asignación (seguridad):**
1. **No se reasigna sin querer:** si la pulsera ya es de otro niño → 409 "ya está asignada a ...; quítale la asignación primero". Evita que unos papás terminen viendo la ubicación de otro niño por error.
2. **Una pulsera activa por alumno:** si el alumno ya tiene otra activa → 409.
3. No se asigna una pulsera dada de baja ni a un alumno dado de baja → 400.

**Flujo típico de pulsera perdida:** `DELETE /api/pulseras/{vieja}` → `POST /api/pulseras` (nueva) → `PUT /api/pulseras/{nueva}/alumno`.

### `/api/tutores` — `Endpoints\TutoresEndpoints.cs`
Administración de tutores y de **qué niños ve cada uno**. **Por ahora abiertos (sin login).**

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `GET` | `/api/tutores` | Tutores activos con sus hijos. `?incluirInactivos=true` | 200 |
| `GET` | `/api/tutores/{id}` | Un tutor con sus hijos | 200 / 404 |
| `POST` | `/api/tutores` | Alta | 201 / 400 / 409 (correo repetido) |
| `PUT` | `/api/tutores/{id}` | Edita sus datos | 204 / 400 / 404 / 409 |
| `DELETE` | `/api/tutores/{id}` | Baja lógica (`Activo = false`) | 204 / 404 |
| `PUT` | `/api/tutores/{id}/alumnos/{alumnoId}` | **Vincula** con un hijo, o actualiza el vínculo si ya existe | 200 (tutor con hijos) / 400 / 404 |
| `DELETE` | `/api/tutores/{id}/alumnos/{alumnoId}` | **Desvincula**: deja de ver a ese niño | 204 / 404 |

**JSON de alta/edición:**
```json
{ "nombres": "María", "apellidoPaterno": "García", "apellidoMaterno": "Ruiz",
  "email": "maria@correo.com", "telefono": "686 123 4567" }
```
Obligatorios: `nombres`, `apellidoPaterno`, `email` (formato válido; se guarda en minúsculas; único). `telefono`: números, espacios, `+`, `-`, `()`.

**JSON para vincular** (`PUT /api/tutores/{id}/alumnos/{alumnoId}`):
```json
{ "parentesco": "Madre", "esContactoPrincipal": true, "puedeRecoger": true }
```
Todos opcionales (`puedeRecoger` default `true`).

**Reglas:**
- **Un solo contacto principal por alumno:** al marcar a un tutor como principal, los demás tutores de ese niño dejan de serlo automáticamente.
- No se vincula un tutor o un alumno dado de baja → 400.
- Vincular dos veces no duplica: actualiza el vínculo existente (por eso es `PUT`).
- Desvincular sí **borra** el renglón de `TutoresAlumnos` (es solo el permiso; no hay historial que perder).

**Respuesta** (`TutorResponse`): sus datos + `hijos`: `[{ alumnoId, nombre, parentesco, esContactoPrincipal, puedeRecoger }]` (solo alumnos activos).

**Flujo de alta de una familia:** `POST /api/alumnos` → `POST /api/pulseras` + `PUT /api/pulseras/{id}/alumno` → `POST /api/tutores` (mamá, papá) → `PUT /api/tutores/{id}/alumnos/{alumnoId}` por cada uno.

### `/api/zonas` y `/api/nodos` — `Endpoints\ZonasNodosEndpoints.cs`
Configuración física de la escuela. **Por ahora abiertos (sin login).**

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `GET` | `/api/zonas` | Zonas activas con cuántos nodos activos tiene cada una | 200 |
| `GET` | `/api/zonas/{id}` | Una zona | 200 / 404 |
| `POST` | `/api/zonas` | Alta: `{ "nombre": "Patio", "tipo": "Patio" }` | 201 / 400 / 409 (nombre repetido) |
| `PUT` | `/api/zonas/{id}` | Edita nombre o tipo | 204 / 400 / 404 / 409 |
| `DELETE` | `/api/zonas/{id}` | Baja lógica, **solo si no tiene nodos activos** | 204 / 404 / 409 |
| `GET` | `/api/nodos` | Nodos activos con su zona y `ultimoContacto` | 200 |
| `GET` | `/api/nodos/{id}` | Un nodo | 200 / 404 |
| `POST` | `/api/nodos` | Alta | 201 / 400 / 409 (código repetido) |
| `PUT` | `/api/nodos/{id}` | Edita (mover de zona, umbral, descripción) | 204 / 400 / 404 / 409 |
| `DELETE` | `/api/nodos/{id}` | Baja lógica: **la API rechaza sus lecturas** desde ese momento | 204 / 404 |

**Tipos de zona** (enum, se escriben como texto): `Salon`, `Patio`, `Tienda`, `Entrada`, `Perimetro`, `Otra`.

**JSON de nodo:**
```json
{ "codigo": "nodo-2", "zonaId": 2, "descripcion": "Junto a la cancha", "umbralRssi": -75, "esPortatil": false }
```
- `codigo`: **debe coincidir con el "Código del nodo" del firmware** (menuconfig). Letras, números, `-`, `_`. Se guarda en minúsculas. Único.
- `zonaId`: zona activa existente. `umbralRssi`: -127 a 0 (default -75).

**Enums como texto:** en `Program.cs`, `JsonStringEnumConverter` hace que los enums viajen como `"Patio"` y no como `1`. Un valor que no existe (ej. `"Cocina"`) → 400.

**Nota:** `ultimoContacto` se actualiza solo cuando el nodo manda una lectura (es decir, cuando ve una pulsera). Un nodo sin pulseras cerca parece "callado" aunque funcione. Se resolverá con el **latido** periódico del nodo (prototipo alfa).

### `/api/auth` — `Endpoints\AuthEndpoints.cs` (sesión)

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `POST` | `/api/auth/login` | Inicia sesión: `{ "email": "...", "password": "...", "recordarme": true }`. Si es correcto, deja la cookie `SafeBand.Sesion` y devuelve quién eres | 200 / 400 / 401 |
| `POST` | `/api/auth/logout` | Cierra sesión (borra la cookie) | 204 |
| `GET` | `/api/auth/yo` | ¿Quién soy?: `{ id, email, nombre, roles: ["Administrador"], tutorId }`. **Requiere sesión** | 200 / 401 |

**Seguridad del login:**
- **Mismo mensaje** para correo inexistente y contraseña incorrecta ("Correo o contraseña incorrectos.") → no revela qué correos están registrados.
- Cuenta desactivada (`Activo = false`) → tampoco entra (mismo mensaje).
- **Bloqueo:** 5 intentos fallidos → 15 minutos bloqueada, con mensaje que lo indica.
- `recordarme: true` → la sesión sobrevive al cerrar el navegador; si no, dura mientras el navegador esté abierto.

**La cookie `SafeBand.Sesion`** (configurada en `Program.cs`):
| Opción | Valor | Para qué |
|---|---|---|
| `HttpOnly` | sí | JavaScript no la puede leer → si alguien inyecta un script en la página, no puede robar la sesión |
| `SameSite` | `Strict` | El navegador no la manda en peticiones que vienen de otros sitios → protege contra CSRF (que otra página haga acciones con tu sesión) |
| `SecurePolicy` | `SameAsRequest` | En HTTPS (Azure) solo viaja cifrada; en tu red local (HTTP) también funciona |
| Duración | 7 días, se renueva con el uso | |

**Sin sesión → 401; sin permiso → 403.** Por defecto ASP.NET redirigiría a una página de login; como es una API, se cambió para que responda esos códigos (`OnRedirectToLogin` / `OnRedirectToAccessDenied`).

**Orden en `Program.cs`:** `app.UseAuthentication()` (¿quién eres?) y luego `app.UseAuthorization()` (¿qué puedes hacer?), antes de los endpoints.

### Permisos por endpoint (paso 2c)

| Endpoints | Quién | Notas |
|---|---|---|
| `/api/alumnos`, `/api/pulseras`, `/api/tutores`, `/api/zonas`, `/api/nodos` | **Solo Administrador** | Sin sesión → 401; con sesión pero otro rol → 403 |
| `/api/auth/login`, `/api/auth/logout` | Cualquiera | |
| `/api/auth/yo` | Cualquier usuario con sesión | |
| `POST /api/lecturas` | ⚠️ **Abierto (temporal)** | Lo usan los ESP32 (no inician sesión) → clave por nodo en el paso 6 |
| `GET /api/lecturas` | ⚠️ **Abierto (temporal)** | Lo usa la página de lecturas, que aún no tiene login → se protege en el paso 5 (y el padre verá solo lecturas de sus hijos) |
| `/`, `/swagger` | Cualquiera | Página y documentación |

**Cómo se aplica:** en `Program.cs` se define la política `Politicas.SoloAdministrador` (= tener el rol `Administrador`), y cada grupo de endpoints la usa con `.RequireAuthorization(Politicas.SoloAdministrador)`. Los de lecturas tienen `.AllowAnonymous()` explícito y un comentario de que es temporal.

**Maestro:** por ahora no accede a la administración. Cuando existan los grupos, verá solo a los alumnos de su grupo.

**Probar en Swagger:** sin sesión, `GET /api/alumnos` → 401. `POST /api/auth/login` con tu correo y contraseña → `GET /api/alumnos` → 200. `GET /api/auth/yo` → debe mostrar tu rol. Swagger está en el mismo sitio que la API, así que el navegador guarda y manda la cookie solo.

**Dar de alta el segundo nodo:** `POST /api/zonas` `{ "nombre": "Patio", "tipo": "Patio" }` → anotar su `id` → `POST /api/nodos` `{ "codigo": "nodo-2", "zonaId": <id> }` → grabar el ESP32 #3 con código `nodo-2` en menuconfig.

---

## 6b. Frontend

### Panel SafeBand (`index.html`) — con inicio de sesión

**Abrir:** con la API corriendo, `http://localhost:5213/` (o `https://localhost:7001/`). Sin sesión aparece el inicio de sesión; con sesión, el panel.

**Diseño:**
| Elemento | Decisión | Por qué |
|---|---|---|
| Colores | Fondo `#F4F7FB`, superficies blancas, texto azul marino `#16283F`, azul principal `#1D5FC2`, azul claro `#E2ECFA`. Verde / ámbar / rojo **solo** para estados (puesta / quitada o batería / SOS) | Claro y sobrio, azul "institucional"; el color siempre significa algo |
| Tipografía | **Atkinson Hyperlegible** (Google Fonts), una sola familia | Diseñada por el Braille Institute para leerse bien con baja visión: distingue I, l, 1. Si no hay internet, usa la fuente del sistema |
| Motivo visual | **Ondas concéntricas** (logo y pantalla de login) | Es lo que hace la pulsera: anunciarse por Bluetooth en ondas. Es el único adorno |
| En vivo | Lo más grande es **el nombre de la zona**; la señal con barritas como las del celular | La pregunta que responde el sistema es "¿dónde está?" |
| Accesibilidad | Foco visible con teclado, respeta "reducir movimiento" del sistema, textos de las barras de señal para lectores de pantalla | |
| Celular | El menú lateral pasa a barra superior con botón ☰; se cierra al tocar fuera o con Esc | |

**Cómo funciona (`js\app.js`):**
1. Al abrir, pide `GET /api/auth/yo`. Si responde 401 → pantalla de login. Si no hay conexión → aviso con botón "Reintentar".
2. Con sesión → panel con las secciones que permite su rol (lista `SECCIONES`: ruta, título, ícono, roles, vista).
3. La navegación usa `#/...` en la dirección (`#/vivo`, `#/alumnos`...). Cada vista recibe un contenedor y devuelve una función para "limpiarse" al salir (ej. detener la actualización automática).
4. Si a mitad del uso la API responde 401 (sesión vencida), `api.js` avisa y la app regresa al login con un mensaje.

**Secciones:**
| Sección | Rol | Estado |
|---|---|---|
| En vivo | Todos | ✅ Lista |
| Alumnos, Pulseras, Tutores, Zonas y nodos | Administrador | ⏳ Pendientes (muestran aviso; mientras tanto, Swagger) |

**Seguridad en el frontend:** todos los datos se insertan con `textContent` (función `el()` de `ui.js`), nunca con `innerHTML`, para que un nombre como `<script>` no se ejecute.

### Página simple de lecturas (`monitor.html`) — sin login

**Abrir:** `http://localhost:5213/monitor.html`. Es la página original del checkpoint, movida aquí como **respaldo**: no pide sesión porque `GET /api/lecturas` sigue abierto temporalmente.

### Por qué está en `wwwroot\`
ASP.NET Core sirve automáticamente los archivos de `wwwroot\` gracias a dos líneas en `Program.cs`:
```csharp
app.UseDefaultFiles();   // "/" → index.html
app.UseStaticFiles();    // sirve los archivos de wwwroot
```
Ventajas: la página y la API están en la misma dirección (no hace falta configurar CORS) y al desplegar en Azure se suben juntas.

### Archivos de `monitor.html` (página simple, sin login)
| Archivo | Qué hace |
|---|---|
| `monitor.html` | Estructura: aviso de error (oculto), tarjeta "Última lectura", tabla "Historial" |
| `monitor.js` | Hace `fetch("/api/lecturas?limit=50")` al cargar y **cada 5 segundos**. La primera lectura del arreglo es la más reciente |
| `monitor.css` | Colores y diseño. Se adapta a celular (la tabla tiene scroll horizontal) |

### Qué muestra (`monitor.html` y la vista "En vivo" del panel)
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
- `API_LECTURAS` y `INTERVALO_MS` están al inicio de `monitor.js` (y `INTERVALO_MS` en `js\vistas\vivo.js`) por si hay que cambiarlos.

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
| `20261002075518_AgregarTutores` | Tablas `Tutores` (correo único) y `TutoresAlumnos` (llave compuesta, borrado en cascada) |
| `20261002081042_AgregarUsuarios` | Tablas de Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`...) con `NombreMostrar`, `Activo`, `FechaAlta` y `TutorId` en `AspNetUsers` |

---

## 7b. Azure (nube)

**Cuenta:** Azure for Students con `onit.sibaja@uabc.edu.mx` — US$100 de crédito, vence 2027-10-02, sin tarjeta.
**Portal:** https://portal.azure.com

### Recursos creados
| Recurso | Nombre | Detalle |
|---|---|---|
| Grupo de recursos | `SafeBandapi20261002002608ResourceGroup` | "Carpeta" que agrupa todo el proyecto. Borrarlo borra todo lo de adentro |
| App Service plan | `plan-safeband` | **Mexico Central**, **F1 (Free)**, Windows |
| App Service | `safeband-api-sibaja` | Donde corre la API + la página (wwwroot) |
| Servidor SQL | `safeband-sql-sibaja` | Mexico Central, SQL authentication, firewall: permite servicios de Azure + IP de la PC. Vacío (no cobra) |
| Azure SQL Database | *(pendiente)* | `safeband-db`. **La oferta gratuita NO está disponible en Mexico Central** (error `ProvisioningDisabled`). Opción elegida al retomar: **Basic (DTU, 2 GB, ~US$5/mes del crédito)** en el servidor existente; alternativa: oferta gratuita en otra región permitida con un servidor nuevo |

### Pendiente para terminar el despliegue (pausado el 2026-10-02 por decisión de avanzar primero con el programa)
1. Crear `safeband-db` (Basic) en `safeband-sql-sibaja` — **sin** "Apply offer".
2. En la App Service → *Settings → Environment variables → Connection strings*: agregar `SafeBand` (tipo SQLAzure) con la cadena de la base.
3. Visual Studio → **Publish** (el perfil ya existe: `Properties\PublishProfiles\safeband-api-sibaja - Web Deploy.pubxml`).
4. Probar `https://<app>.azurewebsites.net/swagger` y la página.
5. Nodo: URL de Azure (`https://...`) — requiere agregar certificados al firmware para HTTPS.

### Lecciones
- **Región:** la cuenta de estudiante solo permite ciertas regiones (error `RequestDisallowedByAzure`). **Mexico Central** sí funciona. Lista completa: portal → Policy → Assignments → *Allowed resource deployment regions* → Parameters.
- **Plan:** el asistente de Visual Studio propone **S1 (de pago, ~US$70/mes)** por defecto. Siempre cambiar a **F1 (Free)**. Se revisa en la App Service → *App Service plan* → *Pricing plan*.
- **Plan gratuito F1:** la app se "duerme" si no recibe peticiones; la primera tarda unos segundos en despertar. Tiene límite de CPU diario (suficiente para el prototipo).

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
- [ ] **Usuarios y roles** (decidido 2026-10-02: la escuela vincula padres con código de invitación):
  - [x] 1a. Modelos `Tutor` y `TutorAlumno`
  - [x] 1a. Migración `AgregarTutores`
  - [x] 1b. Endpoints de alumnos
  - [x] 1b. Endpoints de pulseras (alta y asignar a alumno)
  - [x] 1b. Endpoints de tutores (alta y vincular hijos)
  - [x] 1b. Endpoints de nodos y zonas
  - [x] 2a. Identity instalado: `Usuario`, roles, reglas de contraseña/bloqueo, administrador inicial por User Secrets
  - [x] 2a. Migración `AgregarUsuarios` + configurar User Secrets
  - [x] 2b. Endpoints de iniciar sesión, cerrar sesión y "¿quién soy?"
  - [x] 2c. Proteger endpoints por rol (administración = solo Administrador; lecturas abiertas temporalmente)
  - [ ] 3. Códigos de invitación y registro del padre (con aviso de privacidad)
  - [ ] 4. Filtrar endpoints por rol (el padre solo ve a sus hijos)
  - [ ] 5. Frontend:
    - [x] Login, estructura del panel (menú, usuario, cerrar sesión), vista "En vivo"
    - [ ] Alumnos
    - [ ] Pulseras
    - [ ] Tutores
    - [ ] Zonas y nodos
    - [ ] Vista de padre (después del paso 3)
  - [ ] 6. Clave (API key) por nodo para que nadie mande lecturas falsas
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
| 2026-10-02 | Activado Azure for Students (US$100, vence 2027-10-02). API lista para la nube: migraciones y datos iniciales automáticos al arrancar, reintentos de conexión a la BD, Swagger también en producción. Probado en modo Production local. |
| 2026-10-02 | Creada App Service `safeband-api-sibaja` en Mexico Central, plan F1 gratuito (West US 3 no estaba permitida; el asistente proponía S1 de pago). |
| 2026-10-02 | Servidor SQL creado; la base gratuita falló (no disponible en Mexico Central). Despliegue pausado: se continúa con el programa en local y se termina Azure antes de la entrega. |
| 2026-10-02 | Diseño de usuarios: la escuela vincula padres con hijos mediante código de invitación; roles Padre / Maestro / Administrador; filtro en la API. Creados `Models\Tutor.cs` y `Models\TutorAlumno.cs` (muchos a muchos). |
| 2026-10-02 | Migración `AgregarTutores` aplicada. Endpoints de alumnos (`GET`, `POST`, `PUT`, `DELETE` = baja lógica). Probados: lista vacía 200, Id inexistente 404, falta nombre 400, fecha inválida 400. |
| 2026-10-02 | Endpoints de pulseras: alta, edición, asignar/quitar alumno, baja lógica. Reglas: no reasignar la pulsera de otro niño, una pulsera activa por alumno, formato `SB-XXXX`, UID NFC hexadecimal. Probados: formato inválido 400, duplicado 409, UID no hex 400, alumno inexistente 400, Id inexistente 404. |
| 2026-10-02 | Endpoints de tutores: alta, edición, baja lógica, vincular/desvincular hijos (parentesco, contacto principal único por alumno, puede recoger). Probados: correo inválido 400, teléfono inválido 400, tutor inexistente 404, vínculo inexistente 404. |
| 2026-10-02 | Endpoints de zonas y nodos (alta, edición, baja lógica; no se da de baja una zona con nodos activos). Enums como texto en el JSON (`JsonStringEnumConverter`). Probados: tipo inválido 400, falta tipo 400, código duplicado 409, zona inexistente 400, código con caracteres inválidos 400, umbral fuera de rango 400, baja de zona con nodos 409. Hay 95 lecturas reales en la BD. |
| 2026-10-02 | Paso 2a: instalado ASP.NET Core Identity 10.0.12. `Models\Usuario.cs` (con `TutorId`), `Models\Roles.cs`, `AppDbContext` hereda de `IdentityDbContext`. Reglas: contraseña 8+ con mayúscula/minúscula/número, bloqueo 5 intentos/15 min. Roles y admin inicial se crean al arrancar (credenciales por User Secrets). |
| 2026-10-02 | Migración `AgregarUsuarios` aplicada al arrancar. Creados los roles Administrador, Maestro, Padre y la cuenta de administrador `onit.sibaja@uabc.edu.mx`. |
| 2026-10-02 | Paso 2b: sesión con cookie (`SafeBand.Sesion`: HttpOnly, SameSite Strict, 7 días). Endpoints `/api/auth/login`, `/logout`, `/yo`. Sin sesión → 401, sin permiso → 403. Probados con un correo inexistente (para no sumar intentos a la cuenta real): `/yo` sin sesión 401, login incorrecto 401 con mensaje genérico, correo inválido 400, logout 204. |
| 2026-10-02 | Paso 2c: política `SoloAdministrador` en alumnos, pulseras, tutores, zonas y nodos. Lecturas (POST y GET) abiertas temporalmente con `.AllowAnonymous()` para no romper el ESP32 ni la página del checkpoint. Probado sin sesión: administración 401, lecturas 200/400, página y Swagger 200. Falta probar 403 (cuando exista una cuenta de Padre). |
| 2026-10-02 | Frontend profesional (paso 5, parte 1): panel con login, menú por rol y vista "En vivo". Colores claros con azul, tipografía Atkinson Hyperlegible, motivo de ondas BLE. Revisado en escritorio y celular. La página anterior pasó a `monitor.html` (sin login, respaldo del checkpoint). |
