using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato de interfaz para INC-75: baja voluntaria de cuentas (RGPD art. 17),
/// anonimización irreversible y gestión administrativa con auditoría obligatoria.
/// Sigue la convención del repositorio de verificar invariantes de Razor mediante análisis de fuentes.
/// </summary>
public class AccountClosureAndAnonymizationUiContractTests
{
    private const string AccountPrivacyPath = "src/Ludeka.Web/Components/Pages/AccountPrivacy.razor";
    private const string UserManagementPath = "src/Ludeka.Web/Components/Pages/UserManagement.razor";

    [Fact]
    public void AccountPrivacy_ShouldRenderDangerZoneAndConfirmationModal()
    {
        var source = ReadSource(AccountPrivacyPath);

        // Tarjeta Zona de Peligro
        Assert.Contains("Zona de peligro", source, StringComparison.Ordinal);
        Assert.Contains("Dar de baja mi cuenta", source, StringComparison.Ordinal);
        Assert.Contains("CurrentUserService.IsFoundingTeam", source, StringComparison.Ordinal);

        // Modal Editorial y Frase de Confirmación
        Assert.Contains("<EditorialModal", source, StringComparison.Ordinal);
        Assert.Contains("DAR DE BAJA", source, StringComparison.Ordinal);
        Assert.Contains("delete-account-confirm-input", source, StringComparison.Ordinal);

        // Formulario POST con Antiforgery y sin navegación interceptada
        Assert.Contains("method=\"post\"", source, StringComparison.Ordinal);
        Assert.Contains("action=\"/cuenta/baja\"", source, StringComparison.Ordinal);
        Assert.Contains("data-enhance=\"false\"", source, StringComparison.Ordinal);
        Assert.Contains("<AntiforgeryToken />", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserManagement_ShouldSupportDeletedStatusFilterAndDisplay()
    {
        var source = ReadSource(UserManagementPath);

        // Filtro por Estado incluye Eliminados
        Assert.Contains("<option value=\"Deleted\">Eliminados</option>", source, StringComparison.Ordinal);

        // Estilo de badge para Deleted
        Assert.Contains("GetStatusBadgeStyle", source, StringComparison.Ordinal);
        Assert.Contains("UserStatus.Deleted", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserManagement_ShouldProtectDeletedUsersAndProvideAnonymizeAction()
    {
        var source = ReadSource(UserManagementPath);

        // Usuarios eliminados tienen acciones bloqueadas
        Assert.Contains("Cuenta anonimizada", source, StringComparison.Ordinal);

        // Botón de baja administrativa
        Assert.Contains("OpenAnonymizeModal", source, StringComparison.Ordinal);
        Assert.Contains("user.Role != UserRole.FoundingTeam", source, StringComparison.Ordinal);

        // Modal de baja administrativa y justificación para auditoría
        Assert.Contains("admin-anonymize-reason", source, StringComparison.Ordinal);
        Assert.Contains("AnonymizeUserAsync", source, StringComparison.Ordinal);
        Assert.Contains("CloseAnonymizeModal", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmAnonymizeAsync", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
