using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Regresión de la pérdida de permisos del modal de gestión de usuarios (INC-46, slice de corrección):
/// el modal debe representar las doce banderas de <see cref="ModeratorPermission"/> y el guardado no
/// puede descartar ninguna, ni siquiera las que la interfaz no represente. Las pruebas invocan el
/// ciclo real del componente (parámetros y guardado) con un servicio espía; el render es real vía
/// <see cref="HtmlRenderer"/>.
/// </summary>
public class UserPermissionsModalTests
{
    private static readonly ModeratorPermission[] DeclaredFlags = Enum.GetValues<ModeratorPermission>()
        .Where(flag => flag != ModeratorPermission.None && flag != ModeratorPermission.All)
        .ToArray();

    private sealed class RecordingUserManagementService : IUserManagementService
    {
        public UpdateUserRoleAndPermissionsCommand? LastUpdateCommand { get; private set; }

        public Task<IReadOnlyList<AppUserDto>> GetUsersAsync(UserFilterDto? filter = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AppUserDto>>([]);

        public Task<AppUserDto?> GetUserByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult<AppUserDto?>(null);

        public Task<AppUserDto> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default)
            => throw new NotSupportedException("El modal de permisos no crea usuarios.");

        public Task<AppUserDto> UpdateUserRoleAndPermissionsAsync(
            UpdateUserRoleAndPermissionsCommand command,
            CancellationToken ct = default)
        {
            LastUpdateCommand = command;
            return Task.FromResult(BuildDto(command.UserId, command.Role, command.Permissions));
        }

        public Task<AppUserDto> UpdateUserStatusAsync(UpdateUserStatusCommand command, CancellationToken ct = default)
            => throw new NotSupportedException("El modal de permisos no cambia el estado de la cuenta.");
    }

    private static AppUserDto BuildDto(string id, UserRole role, ModeratorPermission permissions)
        => new(
            Id: id,
            UserName: "Moderadora de Pruebas",
            Email: $"{id}@ludeka.es",
            Role: role,
            RoleDisplayName: role.ToString(),
            Status: UserStatus.Active,
            StatusDisplayName: "Activo",
            Permissions: permissions,
            PermissionNames: [],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

    private static UserPermissionsModal CreateModalFor(
        UserRole role,
        ModeratorPermission permissions,
        RecordingUserManagementService service)
    {
        var modal = new UserPermissionsModal();

        var serviceProperty = typeof(UserPermissionsModal).GetProperty(
            "UserService",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("El modal no expone la propiedad inyectada UserService.");
        serviceProperty.SetValue(modal, service);

        modal.IsOpen = true;
        modal.User = BuildDto("mod_doce", role, permissions);
        modal.OnUserUpdated = EventCallback.Factory.Create<AppUserDto>(modal, _ => { });
        modal.OnClose = EventCallback.Factory.Create(modal, () => { });

        var onParametersSet = typeof(UserPermissionsModal).GetMethod(
            "OnParametersSet",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("El modal no declara OnParametersSet.");
        onParametersSet.Invoke(modal, null);

        return modal;
    }

    private static Task InvokeSavePermissions(UserPermissionsModal modal)
    {
        var savePermissions = typeof(UserPermissionsModal).GetMethod(
            "SavePermissions",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("El modal no declara SavePermissions.");

        return Assert.IsAssignableFrom<Task>(savePermissions.Invoke(modal, null));
    }

    [Fact]
    public async Task RenderedModal_ForAModeratorWithEveryFlag_ShouldOfferOneCheckboxPerDeclaredFlag()
    {
        Assert.Equal(12, DeclaredFlags.Length);

        var services = new ServiceCollection();
        services.AddSingleton<IUserManagementService>(new RecordingUserManagementService());
        var renderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<UserPermissionsModal>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["IsOpen"] = true,
                ["User"] = BuildDto("mod_doce", UserRole.Moderator, ModeratorPermission.All)
            }));
            return root.ToHtmlString();
        });

        var checkboxes = Regex.Matches(html, "<input[^>]*type=\"checkbox\"", RegexOptions.IgnoreCase);
        Assert.Equal(DeclaredFlags.Length, checkboxes.Count);

        // Cada bandera declarada tiene su vía de concesión visible en la interfaz.
        foreach (var label in new[]
                 {
                     "Gestionar Cuentas y Privilegios",
                     "Consultar la Bitácora de Auditoría",
                     "Gestionar Eventos Lúdicos",
                     "Gestionar Notificaciones Comunitarias"
                 })
        {
            Assert.Contains(label, html, StringComparison.Ordinal);
        }

        var checkedBoxes = Regex.Matches(html, "<input[^>]*checked", RegexOptions.IgnoreCase);
        Assert.Equal(DeclaredFlags.Length, checkedBoxes.Count);
    }

    [Fact]
    public async Task SavePermissions_WithEveryFlagInTheUserMask_ShouldSendTheWholeTwelveFlagMask()
    {
        var service = new RecordingUserManagementService();
        var modal = CreateModalFor(UserRole.Moderator, ModeratorPermission.All, service);

        await InvokeSavePermissions(modal);

        var command = Assert.IsType<UpdateUserRoleAndPermissionsCommand>(service.LastUpdateCommand);
        Assert.Equal(UserRole.Moderator, command.Role);
        Assert.Equal(ModeratorPermission.All, command.Permissions);
        foreach (var flag in DeclaredFlags)
        {
            Assert.True(
                command.Permissions.HasFlag(flag),
                $"El guardado del modal perdió la bandera {flag}.");
        }
    }

    [Fact]
    public async Task SavePermissions_WithOnlyNewGranularFlags_ShouldLoadAndSendExactlyThoseFlags()
    {
        var service = new RecordingUserManagementService();
        var mask = ModeratorPermission.CanManageUsers
                   | ModeratorPermission.CanViewAuditLog
                   | ModeratorPermission.CanManageEvents
                   | ModeratorPermission.CanManageNotifications;
        var modal = CreateModalFor(UserRole.Moderator, mask, service);

        await InvokeSavePermissions(modal);

        Assert.Equal(mask, service.LastUpdateCommand!.Permissions);
    }

    [Fact]
    public async Task SavePermissions_ShouldPreserveBitsOutsideTheRepresentedGranularMask()
    {
        // Un bit fuera de las doce banderas declaradas (dato heredado o bandera futura) no puede
        // desaparecer por abrir el modal y guardar sin tocarlo.
        var unknownBit = (ModeratorPermission)(1 << 20);
        var service = new RecordingUserManagementService();
        var modal = CreateModalFor(
            UserRole.Moderator,
            ModeratorPermission.CanEditGames | unknownBit,
            service);

        await InvokeSavePermissions(modal);

        Assert.Equal(ModeratorPermission.CanEditGames | unknownBit, service.LastUpdateCommand!.Permissions);
    }

    [Fact]
    public async Task SavePermissions_WhenTheRoleIsNotModerator_ShouldSendAnEmptyMask()
    {
        var service = new RecordingUserManagementService();
        var modal = CreateModalFor(UserRole.CommunityUser, ModeratorPermission.All, service);

        await InvokeSavePermissions(modal);

        Assert.Equal(ModeratorPermission.None, service.LastUpdateCommand!.Permissions);
    }
}
