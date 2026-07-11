using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventBattleFactoryTests
{
    [Fact]
    public void FieldBattle_WithValidMobileParties_IsAllowed()
    {
        var attacker = CreateMobileParty();
        var defender = CreateMobileParty();

        Assert.True(MapEventBattleFactory.CanCreateMapEvent(attacker, defender, default, out _));
    }

    [Fact]
    public void FieldBattle_WithSameParty_IsRejected()
    {
        var party = CreateMobileParty();

        Assert.False(MapEventBattleFactory.CanCreateMapEvent(party, party, default, out _));
    }

    [Fact]
    public void FieldBattle_WithInactiveParty_IsRejected()
    {
        var attacker = CreateMobileParty(active: false);
        var defender = CreateMobileParty();

        Assert.False(MapEventBattleFactory.CanCreateMapEvent(attacker, defender, default, out _));
    }

    [Fact]
    public void FieldBattle_WithPartyAlreadyInMapEvent_IsRejected()
    {
        var attacker = CreateMobileParty();
        var defender = CreateMobileParty();
        attacker._mapEventSide = (TaleWorlds.CampaignSystem.MapEvents.MapEventSide)
            FormatterServices.GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.MapEvents.MapEventSide));

        Assert.False(MapEventBattleFactory.CanCreateMapEvent(attacker, defender, default, out _));
    }

    [Fact]
    public void FieldBattle_WithPartyInsideSettlement_IsRejected()
    {
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        var attacker = CreateMobileParty(currentSettlement: settlement);
        var defender = CreateMobileParty();

        Assert.False(MapEventBattleFactory.CanCreateMapEvent(attacker, defender, default, out _));
    }

    [Fact]
    public void ForcedSettlementBattle_DoesNotUseFieldBattleSettlementGuard()
    {
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        var attacker = CreateMobileParty(currentSettlement: settlement);
        var defender = CreateMobileParty();
        var flags = new BattleCreationFlags(
            forceRaid: false,
            forceSallyOut: true,
            forceVolunteers: false,
            forceSupplies: false,
            isSallyOutAmbush: false,
            forceBlockadeAttack: false,
            forceBlockadeSallyOutAttack: false,
            forceHideoutSendTroops: false);

        Assert.True(MapEventBattleFactory.CanCreateMapEvent(attacker, defender, flags, out _));
    }

    private static PartyBase CreateMobileParty(bool active = true, Settlement currentSettlement = null)
    {
        var mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        mobileParty.IsActive = active;
        mobileParty._currentSettlement = currentSettlement;
        mobileParty.Party = party;
        party.MobileParty = mobileParty;
        return party;
    }

}
