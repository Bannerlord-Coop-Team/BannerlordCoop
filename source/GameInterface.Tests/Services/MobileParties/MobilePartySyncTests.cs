using GameInterface.AutoSync;
using GameInterface.Services.MobileParties;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Verifies movement state is excluded from independent AutoSync registration.
/// </summary>
public class MobilePartySyncTests
{
    [Fact]
    public void MovementSnapshotMembers_AreNotRegisteredForIndependentAutoSync()
    {
        var registry = new AutoSyncRegistry();
        _ = new MobilePartySync(registry);

        var registration = registry.Registrations[typeof(MobileParty)];
        var fieldNames = registration.Fields.Select(field => field.Value.Name).ToArray();
        var propertyNames = registration.Properties.Select(property => property.Value.Name).ToArray();

        Assert.DoesNotContain(nameof(MobileParty._targetSettlement), fieldNames);
        Assert.DoesNotContain(nameof(MobileParty.MoveTargetPoint), fieldNames);
        Assert.DoesNotContain(nameof(MobileParty.NextTargetPosition), fieldNames);
        Assert.DoesNotContain(nameof(MobileParty.PartyMoveMode), fieldNames);
        Assert.DoesNotContain(nameof(MobileParty.MoveTargetParty), fieldNames);
        Assert.DoesNotContain(nameof(MobileParty.TargetParty), propertyNames);
        Assert.DoesNotContain(nameof(MobileParty.DefaultBehavior), propertyNames);
        Assert.DoesNotContain(nameof(MobileParty.ShortTermBehavior), propertyNames);
        Assert.DoesNotContain(nameof(MobileParty.DesiredAiNavigationType), propertyNames);

        Assert.Contains(nameof(MobileParty.HasUnpaidWages), fieldNames);
    }
}
