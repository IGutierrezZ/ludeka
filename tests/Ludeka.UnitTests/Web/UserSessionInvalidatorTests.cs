using System.Collections.Generic;
using Ludeka.Application.Contracts;
using Ludeka.Web.Services;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Invalidación de sesión de INC-46 F3: el aviso llega con el usuario normalizado y una versión
/// monótona, y las peticiones sin identidad no notifican nada.
/// </summary>
public class UserSessionInvalidatorTests
{
    [Fact]
    public void Invalidate_ShouldNotifyTheVersionedSession()
    {
        var invalidator = new InMemoryUserSessionInvalidator();
        var received = new List<UserSessionInvalidatedEventArgs>();
        invalidator.Invalidated += (_, args) => received.Add(args);

        invalidator.Invalidate("Laura_Mod");

        var notification = Assert.Single(received);
        Assert.Equal("laura_mod", notification.UserId);
        Assert.Equal(1L, notification.Version);
        Assert.Equal(1L, invalidator.GetVersion("laura_mod"));
    }

    [Fact]
    public void Invalidate_ShouldAdvanceTheVersionOnEachChange()
    {
        var invalidator = new InMemoryUserSessionInvalidator();

        invalidator.Invalidate("laura_mod");
        invalidator.Invalidate("laura_mod");

        Assert.Equal(2L, invalidator.GetVersion("LAURA_MOD"));
        Assert.Equal(0L, invalidator.GetVersion("otra_cuenta"));
    }

    [Fact]
    public void Invalidate_ShouldIgnoreSessionsWithoutIdentity()
    {
        var invalidator = new InMemoryUserSessionInvalidator();
        var notifications = 0;
        invalidator.Invalidated += (_, _) => notifications++;

        invalidator.Invalidate("   ");
        invalidator.Invalidate(string.Empty);

        Assert.Equal(0, notifications);
        Assert.Equal(0L, invalidator.GetVersion(string.Empty));
    }
}
