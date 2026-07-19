using GameInterface.AutoSync;
using GameInterface.Services.MobileParties;
using System.Linq;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class MobilePartySyncTests
{
    [Fact]
    public void MovementSnapshotMembers_AreNotRegisteredForIndependentAutoSync()
    {
        var registry = new AutoSyncRegistry();
        _ = new MobilePartySync(registry);

        var registration = registry.Registrations[typeof(MobileParty)];
        var registeredNames = registration.Fields.Select(field => field.Value.Name)
            .Concat(registration.Properties.Select(property => property.Value.Name))
            .ToHashSet();

        Assert.DoesNotContain(new[] {
            nameof(MobileParty._targetSettlement), nameof(MobileParty.MoveTargetPoint),
            nameof(MobileParty.NextTargetPosition), nameof(MobileParty.PartyMoveMode),
            nameof(MobileParty.MoveTargetParty), nameof(MobileParty.TargetParty),
            nameof(MobileParty.DefaultBehavior), nameof(MobileParty.ShortTermBehavior),
            nameof(MobileParty.DesiredAiNavigationType),
        }, registeredNames.Contains);
    }

    [Fact]
    public void DefaultClanFinanceModelTarget_IsDeferredUntilGameStart()
    {
        var registry = new AutoSyncRegistry();
        _ = new MobilePartySync(registry);

        var registration = registry.Registrations[typeof(MobileParty)];

        Assert.Equal(GameInterface.HARMONY_GAME_STARTED_CATEGORY, registration.PatchCategory);
        Assert.Contains(registration.TargetMethods, method =>
            method.DeclaringType == typeof(DefaultClanFinanceModel) &&
            method.Name == nameof(DefaultClanFinanceModel.ApplyMoraleEffect));
    }
}
