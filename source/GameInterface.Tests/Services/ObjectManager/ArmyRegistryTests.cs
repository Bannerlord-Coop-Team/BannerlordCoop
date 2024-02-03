using Common.Util;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Extensions;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.ObjectManager;
public class ArmyRegistryTests
{
    [Fact]
    public void RegisterArmy()
    {
        var armyRegistry = new ArmyRegistry();

        var army = ObjectHelper.SkipConstructor<Army>();

        armyRegistry.RegisterNewObject(army, out var newId);

        Assert.Equal(army.GetStringId(), newId);
    }
}
