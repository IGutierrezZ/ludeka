using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Application.Contracts;

namespace Ludeka.Application.Features.Staff;

/// <summary>
/// Implementación reactiva en circuito/sesión de <see cref="IStaffActionService"/>.
/// </summary>
public sealed class StaffActionService : IStaffActionService
{
    private readonly List<StaffActionItem> _customActions = [];

    public IReadOnlyList<StaffActionItem> CustomActions => _customActions.AsReadOnly();

    public event Action? OnActionsChanged;

    public void SetCustomActions(IEnumerable<StaffActionItem> actions)
    {
        _customActions.Clear();
        if (actions != null)
        {
            _customActions.AddRange(actions);
        }

        OnActionsChanged?.Invoke();
    }

    public void ClearCustomActions()
    {
        if (_customActions.Count == 0)
        {
            return;
        }

        _customActions.Clear();
        OnActionsChanged?.Invoke();
    }
}
