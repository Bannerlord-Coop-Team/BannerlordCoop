using GameInterface.Services.MobileParties.Commands;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class MercenaryStockDebugCommandTests
{
    [Fact]
    public void RefreshCommand_UsesVanillaMercenaryRefreshSignature()
    {
        var field = typeof(MercenaryStockDebugCommand).GetField(
            "UpdateCurrentMercenaryTroopAndCount",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        var method = Assert.IsAssignableFrom<MethodInfo>(field.GetValue(null));

        Assert.Equal("UpdateCurrentMercenaryTroopAndCount", method.Name);
        Assert.Equal(typeof(RecruitmentCampaignBehavior), method.DeclaringType);
        Assert.Equal(new[] { typeof(Town), typeof(bool) }, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
