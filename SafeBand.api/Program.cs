using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SafeBand.api.Data;
using SafeBand.api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. Servicios ----------

// Descripción de la API (la usa Swagger)
builder.Services.AddOpenApi();

// Base de datos: SQL Server con la cadena "SafeBand" de appsettings
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SafeBand")));

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Página para probar la API desde el navegador: /swagger
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SafeBand API"));

    // Datos de prueba (solo en tu PC, solo si la BD está vacía)
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SembrarAsync(db);
}

// ---------- 3. Endpoints ----------

app.MapLecturas();   // /api/lecturas  (Endpoints/LecturasEndpoints.cs)

app.Run();
