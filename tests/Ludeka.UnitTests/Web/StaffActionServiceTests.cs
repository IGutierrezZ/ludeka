using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Staff;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class StaffActionServiceTests
{
    [Fact]
    public async Task StaffActionService_ShouldSetAndTriggerActionsChanged()
    {
        var service = new StaffActionService();
        bool changedTriggered = false;
        service.OnActionsChanged += () => changedTriggered = true;

        bool clicked = false;
        var action = new StaffActionItem("Editar destacados", () =>
        {
            clicked = true;
            return Task.CompletedTask;
        }, "sparkles");

        service.SetCustomActions([action]);

        Assert.True(changedTriggered);
        Assert.Single(service.CustomActions);
        Assert.Equal("Editar destacados", service.CustomActions[0].Label);
        Assert.Equal("sparkles", service.CustomActions[0].Icon);
        Assert.False(service.CustomActions[0].IsDestructive);

        await service.CustomActions[0].OnClick();
        Assert.True(clicked);
    }

    [Fact]
    public void StaffActionService_ShouldClearActionsAndNotify()
    {
        var service = new StaffActionService();
        service.SetCustomActions([new StaffActionItem("Accion 1", () => Task.CompletedTask)]);
        Assert.Single(service.CustomActions);

        int changeCount = 0;
        service.OnActionsChanged += () => changeCount++;

        service.ClearCustomActions();

        Assert.Equal(1, changeCount);
        Assert.Empty(service.CustomActions);

        // Llamar de nuevo con lista vacía no debe disparar eventos redundantes
        service.ClearCustomActions();
        Assert.Equal(1, changeCount);
    }
}
