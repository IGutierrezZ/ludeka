using Ludeka.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-52 (Fase 3 / PR #3), especificación <c>reverse-proxy-forwarded-headers</c>, decisiones de
/// diseño D2-D5. Pruebas unitarias puras sobre <see cref="ForwardedHeadersConfiguration.Build"/>:
/// no levantan ningún host, solo comprueban el <see cref="ForwardedHeadersOptions"/> que la
/// función devuelve. El caso con host real (arnés de la Fase 2, incluido el caso negativo que
/// confirmó la puerta H4/D4) vive en <see cref="ForwardedHeadersPipelineTests"/>.
/// </summary>
public class ForwardedHeadersConfigurationTests
{
    // P2 — design.md D2: se procesa ÚNICAMENTE X-Forwarded-Proto. Igualdad EXACTA con "==", no
    // HasFlag: si alguien añade una bandera de más (por ejemplo XForwardedFor), esta prueba debe
    // romperse, y HasFlag no lo detectaría porque seguiría cumpliendo el subconjunto.
    [Fact]
    public void Build_DebeProcesarUnicamenteLaCabeceraXForwardedProto()
    {
        var options = ForwardedHeadersConfiguration.Build();

        Assert.True(
            options.ForwardedHeaders == ForwardedHeaders.XForwardedProto,
            "ForwardedHeaders debe ser exactamente XForwardedProto, sin ninguna bandera adicional.");
    }

    // P3 — design.md D4: las listas de confianza deben quedar vacías explícitamente. Sus valores
    // por defecto son justo lo que este incremento no puede seguir heredando en silencio (puerta
    // H4/D4, confirmada empíricamente en la Fase 2 con el caso negativo N2).
    [Fact]
    public void Build_DebeVaciarLasListasDeProxiesYRedesConocidas()
    {
        var options = ForwardedHeadersConfiguration.Build();

        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    // P4 — design.md D5: un único salto de proxy (mapeo de dominio directo a Cloud Run / Nginx).
    [Fact]
    public void Build_DebeFijarForwardLimitEnUno()
    {
        var options = ForwardedHeadersConfiguration.Build();

        Assert.Equal(1, options.ForwardLimit);
    }

    // P5 — design.md D2: XForwardedHost (suplantación de host, ninguna capa lo detendría) y
    // XForwardedFor (nadie lee la IP remota hoy) quedan fuera a propósito.
    [Fact]
    public void Build_NoDebeIncluirXForwardedHostNiXForwardedFor()
    {
        var options = ForwardedHeadersConfiguration.Build();

        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
    }
}
