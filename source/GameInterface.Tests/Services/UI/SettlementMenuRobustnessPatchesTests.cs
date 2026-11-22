using GameInterface.Services.UI.Patches;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class SettlementMenuRobustnessPatchesTests
{
    private static readonly System.Reflection.MethodBase RefreshProperties =
        typeof(GameMenuPartyItemVM).GetMethod(nameof(GameMenuPartyItemVM.RefreshProperties));

    [Fact]
    public void TargetMethods_ResolveBothMenuMethods()
    {
        var methods = SettlementMenuRobustnessPatches.TargetMethods().ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, Assert.NotNull);
    }

    [Fact]
    public void Finalizer_WithMissingPartyData_SwallowsException()
    {
        var result = SettlementMenuRobustnessPatches.Finalizer(new NullReferenceException(), RefreshProperties);

        Assert.Null(result);
    }

    [Fact]
    public void Finalizer_WithOtherException_KeepsException()
    {
        var exception = new InvalidOperationException();

        var result = SettlementMenuRobustnessPatches.Finalizer(exception, RefreshProperties);

        Assert.Same(exception, result);
    }
}