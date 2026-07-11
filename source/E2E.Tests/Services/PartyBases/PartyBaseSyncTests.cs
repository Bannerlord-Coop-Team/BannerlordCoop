using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyBases;

public class PartyBaseSyncTests : SyncTestBase
{
    private string PartyBaseId;

    public PartyBaseSyncTests(ITestOutputHelper output) : base(output)
    {
        PartyBaseId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<ItemRoster>();
        TestEnvironment.CreateRegisteredObject<TroopRoster>();
        TestEnvironment.CreateRegisteredObject<TroopRoster>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_PartyBase_Properties()
    {
        Server.ObjectManager.TryGetObject<PartyBase>(PartyBaseId, out var partyBase);
        TestEnvironment.AssertReferenceProperty<PartyBase, MobileParty>(nameof(PartyBase.MobileParty));
        // ItemRoster is protobuf-serializable, so the dynamic sync sends it by value (a new instance on
        // the client) rather than by reference - assert value equality, not Assert.Same.
        TestEnvironment.AssertProperty<PartyBase, ItemRoster>(nameof(PartyBase.ItemRoster), new ItemRoster(), defaultValue: partyBase.ItemRoster);
        TestEnvironment.AssertReferenceProperty<PartyBase, TroopRoster>(nameof(PartyBase.MemberRoster));
        TestEnvironment.AssertReferenceProperty<PartyBase, TroopRoster>(nameof(PartyBase.PrisonRoster));
        TestEnvironment.AssertProperty<PartyBase, int>(nameof(PartyBase.RandomValue), 5, defaultValue: partyBase.RandomValue);
        TestEnvironment.AssertReferenceProperty<PartyBase, Settlement>(nameof(PartyBase.Settlement));

        // Not synced - cached/derived values (commented out in PartyBaseSync). Kept for future reference.
        //TestEnvironment.AssertProperty<PartyBase, bool>(nameof(PartyBase.IsVisualDirty), true);
        //TestEnvironment.AssertProperty<PartyBase, bool>(nameof(PartyBase.LevelMaskIsDirty), true);
        // MapEventSide is committed by the MapEvent initialization barrier, not generic PartyBase sync.
    }

    [Fact]
    public void Server_PartyBase_Fields()
    {
        // _index is set by a static counter in PartyBase and is not 0 by default
        Server.ObjectManager.TryGetObject<PartyBase>(PartyBaseId, out var partyBase);
        var initialIndex = partyBase._index;

        TestEnvironment.AssertReferenceField<PartyBase, Hero>(nameof(PartyBase._customOwner));
        TestEnvironment.AssertField<PartyBase, int>(nameof(PartyBase._index), 5, PartyBaseId, defaultValue: initialIndex);
        TestEnvironment.AssertField<PartyBase, CampaignTime>(nameof(PartyBase._lastEatingTime), new CampaignTime(1512));
        TestEnvironment.AssertField<PartyBase, int>(nameof(PartyBase._remainingFoodPercentage), 9);

        // Not synced - cached/version-counter values (commented out in PartyBaseSync). Kept for future reference.
        //TestEnvironment.AssertField<PartyBase, int>(nameof(PartyBase._lastNumberOfMenPerTierVersionNo), 6);
        //TestEnvironment.AssertField<PartyBase, int>(nameof(PartyBase._lastNumberOfMenWithHorseVersionNo), 7);
        //TestEnvironment.AssertField<PartyBase, int>(nameof(PartyBase._numberOfMenWithHorse), 8);
    }
}
