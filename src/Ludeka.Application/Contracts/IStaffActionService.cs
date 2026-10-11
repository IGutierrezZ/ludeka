using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Representa una acción contextual expuesta por una pantalla activa hacia la barra de gestión StaffBar.
/// </summary>
public sealed record StaffActionItem(
    string Label,
    Func<Task> OnClick,
    string? Icon = null,
    bool IsDestructive = false
)
{
    public StaffActionItem(string label, Action onClick, string? icon = null, bool isDestructive = false)
        : this(label, () => { onClick(); return Task.CompletedTask; }, icon, isDestructive)
    {
    }

    public StaffActionItem(string label, string? icon, Action onClick, bool isDestructive = false)
        : this(label, () => { onClick(); return Task.CompletedTask; }, icon, isDestructive)
    {
    }

    public StaffActionItem(string label, string? icon, Func<Task> onClick, bool isDestructive = false)
        : this(label, onClick, icon, isDestructive)
    {
    }
}

/// <summary>
/// Mediador de estado de sesión para acciones contextuales de administración y moderación.
/// Permite que las páginas activas publiquen sus botones de gestión en la StaffBar del layout.
/// </summary>
public interface IStaffActionService
{
    IReadOnlyList<StaffActionItem> CustomActions { get; }
    event Action? OnActionsChanged;
    void SetCustomActions(IEnumerable<StaffActionItem> actions);
    void ClearCustomActions();
}
