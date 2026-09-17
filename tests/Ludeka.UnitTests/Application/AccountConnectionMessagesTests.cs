using Ludeka.Application.Features.Identity;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Auditoría de honestidad de los mensajes de vinculación y desvinculación (INC-49, diseño §3.1):
/// el rechazo de vincular un proveedor ya usado por otra cuenta no debe prometer una resolución
/// automática que el sistema no ofrece.
/// </summary>
public class AccountConnectionMessagesTests
{
    private static readonly string[] ForbiddenWords =
    [
        "fusion", "fusión", "transferir", "traspas", "soporte", "contacta"
    ];

    [Fact]
    public void RejectedOwnedByAnotherAccountMessage_ShouldNotPromiseAnAutomaticResolution()
    {
        // Act
        var headline = AccountConnectionMessages.RejectedOwnedByAnotherAccountHeadline("Google");
        var detail = AccountConnectionMessages.RejectedOwnedByAnotherAccountDetail("Google");

        // Assert: ninguna de las dos frases contiene una promesa de fusión o traspaso automático.
        foreach (var forbidden in ForbiddenWords)
        {
            Assert.DoesNotContain(forbidden, headline, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, detail, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RejectedOwnedByAnotherAccountMessage_ShouldDirectToUnlinkFromTheOtherAccount()
    {
        // Act
        var detail = AccountConnectionMessages.RejectedOwnedByAnotherAccountDetail("Discord");

        // Assert: la única vía de resolución que ofrece es desvincular desde la otra cuenta.
        Assert.Contains("desvincúlalo", detail, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Discord", detail, System.StringComparison.Ordinal);
    }
}
