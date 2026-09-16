using System;
using System.IO;
using System.Linq;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de fuente del modal de permisos (convención del repositorio, sin bUnit): el modal debe
/// referenciar cada bandera declarada y reconstruir la máscara al guardar desde todas ellas, de modo
/// que una lista parcial vuelva a poner estas pruebas en rojo.
/// </summary>
public class UserPermissionsModalContractTests
{
    private const string ModalPath = "src/Ludeka.Web/Components/Shared/UserPermissionsModal.razor";

    private static readonly ModeratorPermission[] DeclaredFlags = Enum.GetValues<ModeratorPermission>()
        .Where(flag => flag != ModeratorPermission.None && flag != ModeratorPermission.All)
        .ToArray();

    [Fact]
    public void Modal_ShouldReferenceEveryDeclaredGranularFlag()
    {
        var source = ReadSource(ModalPath);

        Assert.Equal(12, DeclaredFlags.Length);
        foreach (var flag in DeclaredFlags)
        {
            Assert.Contains($"ModeratorPermission.{flag}", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SaveBlock_ShouldRebuildTheMaskFromEveryDeclaredGranularFlag()
    {
        var source = ReadSource(ModalPath);

        var saveStart = source.IndexOf("private async Task SavePermissions()", StringComparison.Ordinal);
        Assert.True(saveStart >= 0, "No se encontró el bloque SavePermissions en el modal.");

        var closeStart = source.IndexOf("private async Task Close()", saveStart, StringComparison.Ordinal);
        var saveBlock = closeStart > saveStart ? source[saveStart..closeStart] : source[saveStart..];

        foreach (var flag in DeclaredFlags)
        {
            Assert.Contains($"ModeratorPermission.{flag}", saveBlock, StringComparison.Ordinal);
        }
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
