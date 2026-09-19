// src/Ludeka.Jobs/Program.cs (INC-47, R6, diseño §8.3): host genérico de vida corta para los
// cuatro trabajos de fondo externalizados. NUNCA se llama a host.RunAsync() — es lo que garantiza
// "una unidad de trabajo por disparo" y "el proceso termina por sí mismo"
// (specs/background-jobs-scheduling/spec.md, "Ejecución de vida corta").
//
// En este PR el punto de entrada cubre únicamente la selección de trabajo. La composición del
// contenedor, las guardas de arranque y la ejecución del runner llegan en el PR 10c, cuando
// existan las piezas que invocan.
using System;
using Ludeka.Jobs;
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

return 0;
