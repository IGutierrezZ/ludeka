using System;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Intención de vinculación transportada en <see cref="AuthenticationProperties.Items"/> (INC-49,
/// diseño §D1): ida y vuelta pura, sin HTTP real. <see cref="ExternalLoginIntent.TryReadLink"/> solo
/// lee de <c>Items</c>, nunca de un formulario ni de la cadena de consulta.
/// </summary>
public class ExternalLoginIntentTests
{
    [Fact]
    public void MarkLinkAndTryReadLink_ShouldRoundTripTheSessionUserId()
    {
        var properties = new AuthenticationProperties();

        ExternalLoginIntent.MarkLink(properties, "user-42");

        Assert.True(ExternalLoginIntent.TryReadLink(properties, out var userId));
        Assert.Equal("user-42", userId);
    }

    [Fact]
    public void TryReadLink_WhenIntentIsAbsent_ShouldReturnFalse()
    {
        var properties = new AuthenticationProperties();

        Assert.False(ExternalLoginIntent.TryReadLink(properties, out var userId));
        Assert.Equal(string.Empty, userId);
    }

    [Fact]
    public void TryReadLink_WhenIntentHasAnotherValue_ShouldReturnFalse()
    {
        var properties = new AuthenticationProperties();
        properties.Items[ExternalLoginIntent.IntentKey] = "algo-distinto";
        properties.Items[ExternalLoginIntent.UserIdKey] = "user-42";

        Assert.False(ExternalLoginIntent.TryReadLink(properties, out _));
    }

    [Fact]
    public void TryReadLink_WhenPropertiesAreNull_ShouldReturnFalse()
    {
        Assert.False(ExternalLoginIntent.TryReadLink(null, out var userId));
        Assert.Equal(string.Empty, userId);
    }

    [Fact]
    public void TryReadLink_ShouldNeverReadTheIntentFromParametersOrAnyOtherChannel()
    {
        // Parameters es un diccionario distinto de Items dentro del mismo AuthenticationProperties:
        // lo más parecido a "otro canal" disponible sobre este mismo objeto. TryReadLink no debe
        // consultarlo nunca, igual que nunca consulta un formulario ni la cadena de consulta.
        var properties = new AuthenticationProperties();
        properties.Parameters[ExternalLoginIntent.IntentKey] = ExternalLoginIntent.LinkValue;

        Assert.False(ExternalLoginIntent.TryReadLink(properties, out _));
    }

    [Fact]
    public void TryReadLink_ShouldOnlyAcceptAuthenticationPropertiesAsInput()
    {
        // Contrato de firma: ningún HttpContext, HttpRequest, IFormCollection o cadena de consulta
        // puede llegar a TryReadLink. Es estructuralmente imposible leer de un formulario o de la
        // URL porque el método no declara ningún parámetro de ese tipo.
        var method = typeof(ExternalLoginIntent).GetMethod(nameof(ExternalLoginIntent.TryReadLink));

        Assert.NotNull(method);
        var parameterTypeNames = Array.ConvertAll(method!.GetParameters(), p => p.ParameterType.Name);
        Assert.Equal(["AuthenticationProperties", "String&"], parameterTypeNames);
    }
}
