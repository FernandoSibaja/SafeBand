using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Data;
using SafeBand.api.Endpoints;
using SafeBand.api.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. Servicios ----------

// Descripción de la API (la usa Swagger)
builder.Services.AddOpenApi();

// Base de datos: SQL Server con la cadena "SafeBand"
// (en tu PC viene de appsettings.Development.json; en Azure, de la configuración del App Service).
// EnableRetryOnFailure: reintenta si la BD gratuita de Azure está "dormida" y tarda en despertar.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SafeBand"),
        sql => sql.EnableRetryOnFailure()));

// Usuarios y roles (ASP.NET Core Identity), guardados en la misma base de datos
builder.Services.AddIdentityCore<Usuario>(o =>
    {
        // Contraseña: mínimo 8 caracteres con mayúscula, minúscula y número
        o.Password.RequiredLength = 8;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireDigit = true;
        o.Password.RequireNonAlphanumeric = false;

        // Bloqueo: 5 intentos fallidos → cuenta bloqueada 15 minutos
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;

        o.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();   // iniciar/cerrar sesión

// Sesión con cookie: al iniciar sesión el navegador guarda "SafeBand.Sesion" y la manda en cada petición
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "SafeBand.Sesion";
    o.Cookie.HttpOnly = true;                       // JavaScript no puede leerla (protege contra robo por scripts)
    o.Cookie.SameSite = SameSiteMode.Strict;        // no se manda desde otros sitios (protege contra CSRF)
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // en HTTPS (Azure) viaja solo cifrada
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;                     // se renueva mientras se use

    // Es una API: sin sesión → 401, sin permiso → 403 (en vez de redirigir a una página de login)
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
// Reglas de permisos (qué rol puede usar qué endpoints)
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.SoloAdministrador, p => p.RequireRole(Roles.Administrador));

// Validación automática de los [Required], [Range]... de los contratos (Contracts/)
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

// Los enums viajan en el JSON como texto ("Patio") en vez de número (1)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// ---------- 2. Cómo se procesa cada petición ----------

// Si llega un JSON mal formado o con un tipo incorrecto (ej. "rssi": "abc") → 400 con mensaje claro.
// Cualquier otro error inesperado → 500.
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (ex is BadHttpRequestException bad)
    {
        var campo = (bad.InnerException as JsonException)?.Path?.TrimStart('$', '.');
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = string.IsNullOrEmpty(campo)
                ? "El cuerpo de la petición no es un JSON válido."
                : $"El campo '{campo}' tiene un valor o tipo de dato inválido."
        });
        return;
    }
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { error = "Error interno del servidor." });
}));

// Descripción de la API y página /swagger para probarla.
// Activas también en Azure como evidencia del backend desplegado (prototipo, sin datos sensibles aún).
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SafeBand API"));

// Al arrancar (en tu PC y en Azure):
//   1. Aplica las migraciones pendientes → crea/actualiza las tablas solo.
//   2. Inserta los datos iniciales si la BD está vacía.
//   3. Crea los roles y el administrador inicial (si falta).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SembrarAsync(db);
    await DbSeeder.SembrarUsuariosAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

// Frontend: sirve los archivos de wwwroot/ (index.html en "/")
app.UseDefaultFiles();
app.UseStaticFiles();

// Sesión: primero "¿quién eres?" (cookie) y luego "¿qué puedes hacer?" (roles)
app.UseAuthentication();
app.UseAuthorization();

// ---------- 3. Endpoints ----------

app.MapAuth();       // /api/auth      (Endpoints/AuthEndpoints.cs)
app.MapLecturas();   // /api/lecturas  (Endpoints/LecturasEndpoints.cs)
app.MapAlumnos();    // /api/alumnos   (Endpoints/AlumnosEndpoints.cs)
app.MapPulseras();   // /api/pulseras  (Endpoints/PulserasEndpoints.cs)
app.MapTutores();    // /api/tutores   (Endpoints/TutoresEndpoints.cs)
app.MapZonasNodos(); // /api/zonas y /api/nodos (Endpoints/ZonasNodosEndpoints.cs)

app.Run();
