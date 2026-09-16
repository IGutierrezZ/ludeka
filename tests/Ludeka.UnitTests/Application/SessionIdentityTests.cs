using System;
using Ludeka.Application.Contracts;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Contrato de la guarda de sesión (INC-46, F4): <c>SessionIdentity.Require</c> es el único punto que
/// decide si hay identidad suficiente para escribir; sin sesión la denegación es siempre controlada.
/// </summary>
public class SessionIdentityTests
{
    [Fact]
    public void Require_WithoutSession_ThrowsUnauthorizedAccessException()
        => Assert.Throws<UnauthorizedAccessException>(() => SessionIdentity.Require(StubCurrentUserService.Anonymous()));

    [Fact]
    public void Require_WithNullService_ThrowsUnauthorizedAccessException()
        => Assert.Throws<UnauthorizedAccessException>(() => SessionIdentity.Require((ICurrentUserService?)null));

    [Fact]
    public void Require_WithSession_ReturnsTheSessionUserId()
        => Assert.Equal("jugadora-real", SessionIdentity.Require(StubCurrentUserService.WithSession("jugadora-real")));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Require_WithEmptyStringIdentity_ThrowsUnauthorizedAccessException(string userId)
        => Assert.Throws<UnauthorizedAccessException>(() => SessionIdentity.Require(userId));

    [Fact]
    public void Require_WithNullStringIdentity_ThrowsUnauthorizedAccessException()
        => Assert.Throws<UnauthorizedAccessException>(() => SessionIdentity.Require((string?)null));

    [Fact]
    public void Require_WithStringIdentity_ReturnsTheTrimmedUserId()
        => Assert.Equal("jugadora-real", SessionIdentity.Require("  jugadora-real  "));
}
