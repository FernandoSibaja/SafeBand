using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Data;
using SafeBand.api.Endpoints;

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

// Validación automática de los [Required], [Range]... de los contratos (Contracts/)
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SembrarAsync(db);
}

// Frontend: sirve los archivos de wwwroot/ (index.html en "/")
app.UseDefaultFiles();
app.UseStaticFiles();

// ---------- 3. Endpoints ----------

app.MapLecturas();   // /api/lecturas  (Endpoints/LecturasEndpoints.cs)
app.MapAlumnos();    // /api/alumnos   (Endpoints/AlumnosEndpoints.cs)
app.MapPulseras();   // /api/pulseras  (Endpoints/PulserasEndpoints.cs)
app.MapTutores();    // /api/tutores   (Endpoints/TutoresEndpoints.cs)

app.Run();
