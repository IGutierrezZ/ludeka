// src/Ludeka.Jobs/Program.cs (INC-47, R6, diseño §8.3): host genérico de vida corta para los
// cuatro trabajos de fondo externalizados. NUNCA se llama a host.RunAsync() — es lo que garantiza
// "una unidad de trabajo por disparo" y "el proceso termina por sí mismo"
// (specs/background-jobs-scheduling/spec.md, "Ejecución de vida corta").
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Ludeka.Application.Contracts;
using Ludeka.Infrastructure.DependencyInjection;
using Ludeka.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Selección de trabajo ANTES de construir el contenedor (diseño §8.4): un nombre de trabajo
// desconocido, vacío o mal escrito no necesita levantar la composición completa de dominio para
// fallar con el código de salida 2 (matriz de amenazas, "Selección de trabajo por argumento").
var selection = JobSelectionResolver.Resolve(args, builder.Configuration);
if (!selection.IsValid)
{
    Console.Error.WriteLine(
        $"Nombre de trabajo inválido o ausente. Nombres válidos: {string.Join(", ", JobNames.All)}");
    return 2;
}

// Composición completa de dominio e infraestructura (diseño §4, decisión D1) — la misma raíz que
// consume Ludeka.Web, sin ningún registro específicamente web.
builder.Services.AddLudekaApplicationCore(builder.Configuration);

// Registros mínimos de un host sin web (diseño §4.6). ISessionPermissionGuard deniega siempre
// (§4.2): un proceso de trabajo solo invoca puntos de entrada de sistema. ICurrentUserService es
// la resolución del hueco de diseño detectado por el orquestador (Engram
// sdd/change-47-workers-cloud-run/hueco-currentuserservice, §4.6 solo preveía la guarda de
// permisos): ninguna de las dos implementaciones existentes en Ludeka.Web sirve fuera del host
// web, y 11 servicios de Ludeka.Application la exigen como dependencia dura de constructor.
builder.Services.AddScoped<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();
builder.Services.AddScoped<ICurrentUserService, SystemCurrentUserService>();

builder.Services.AddLudekaJobRunners();

// Convierte cualquier registro que falte en el grafo (como el hueco de arriba, antes de
// resolverlo) en un fallo ruidoso al construir el contenedor, no en una NullReferenceException en
// tiempo de ejecución (informe de sdd-apply, Fase 10). Precedente: la prueba de composición de R2b
// (LudekaServiceCollectionExtensionsTests) ya usa las mismas dos opciones.
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
}));

using var host = builder.Build();

// Guarda de arranque: un trabajo contra la base de datos equivocada es peor que un trabajo caído
// (diseño §8.6). Cualquier fallo antes de la unidad de trabajo —guarda en rojo, o una excepción
// de composición/conectividad no anticipada— sale con el mismo código 3 (diseño §8.5).
string? guardFailure;
try
{
    guardFailure = await StartupGuards.EvaluateAsync(
        host.Services, builder.Configuration, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error de arranque antes de la unidad de trabajo: {ex.Message}");
    return 3;
}

if (guardFailure is not null)
{
    Console.Error.WriteLine(guardFailure);
    return 3;
}

var timeoutMinutes = builder.Configuration.GetValue("Workers:JobTimeoutMinutes", defaultValue: 30);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    cts.Cancel();
});

return await JobHostRunner.RunSelectedJobAsync(host.Services, selection.JobName!, cts.Token);
