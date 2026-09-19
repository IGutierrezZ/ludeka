using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Ludeka.Jobs;

/// <summary>Desenlace de <see cref="JobSelectionResolver.Resolve"/>: o bien un nombre de trabajo
/// válido, o bien inválido/ausente (nunca ambas cosas).</summary>
public sealed record JobSelectionResult(bool IsValid, string? JobName)
{
    public static JobSelectionResult Valid(string jobName) => new(true, jobName);
    public static JobSelectionResult Invalid() => new(false, null);
}

/// <summary>
/// Selección del trabajo a ejecutar (INC-47, R6, diseño §8.4): precedencia determinista entre las
/// tres fuentes — argumento posicional, <c>--job=&lt;nombre&gt;</c>, y <c>Workers:JobName</c> de
/// configuración — nombre y comparación siempre sensibles a mayúsculas y exactos contra
/// <see cref="JobNames.All"/>. Nombre de clase decidido por <c>sdd-apply</c> (tasks.md 10.1): el
/// diseño fija la precedencia, no dónde vive el código que la aplica.
/// </summary>
public static class JobSelectionResolver
{
    private const string JobFlagPrefix = "--job=";

    public static JobSelectionResult Resolve(IReadOnlyList<string> args, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);

        var positional = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var flagValue = args
            .Where(a => a.StartsWith(JobFlagPrefix, StringComparison.Ordinal))
            .Select(a => a[JobFlagPrefix.Length..])
            .FirstOrDefault();

        string? candidate =
            !string.IsNullOrEmpty(positional) ? positional :
            !string.IsNullOrEmpty(flagValue) ? flagValue :
            configuration["Workers:JobName"];

        if (string.IsNullOrWhiteSpace(candidate) || !JobNames.All.Contains(candidate, StringComparer.Ordinal))
        {
            return JobSelectionResult.Invalid();
        }

        return JobSelectionResult.Valid(candidate);
    }
}
