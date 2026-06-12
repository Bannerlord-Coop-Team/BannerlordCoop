using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideSyncTests : SyncTestBase
{
    private readonly string sideId;
    private readonly string clanId;

    public MapEventSideSyncTests(ITestOutputHelper output) : base(output)
    {
        sideId = TestEnvironment.CreateRegisteredObject<MapEventSide>();
        TestEnvironment.CreateRegisteredObject<MapEvent>();
        TestEnvironment.CreateRegisteredObject<CharacterObject>();
        TestEnvironment.CreateRegisteredObject<PartyBase>();
        clanId = TestEnvironment.CreateRegisteredObject<Clan>();
    }

    [Fact]
    public void Server_MapEventSide_Sync()
    {
        Server.ObjectManager.TryGetObject(sideId, out MapEventSide side);

        // Synced fields (value types)
        TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide.TroopCasualties), 5);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.InfluenceValue), 5f);
        TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide.IsSurrendered), true);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.LeaderSimulationModifier), 5f);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownValue), 5f);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.StrengthRatio), 5f, defaultValue: 1f);

        // Synced properties
        TestEnvironment.AssertProperty<MapEventSide, float>(nameof(MapEventSide.CasualtyStrength), 5f);
        TestEnvironment.AssertProperty<MapEventSide, BattleSideEnum>(nameof(MapEventSide.MissionSide), BattleSideEnum.Defender, defaultValue: BattleSideEnum.Attacker);
        TestEnvironment.AssertReferenceProperty<MapEventSide, PartyBase>(nameof(MapEventSide.LeaderParty));

        // Synced reference fields. The builder seeds these non-null, so pass the current value as the
        // pre-check default. _mapFaction is an IFaction, so resolve it against a registered Clan.
        TestEnvironment.AssertReferenceField<MapEventSide, MapEvent>(nameof(MapEventSide._mapEvent), defaultValue: side._mapEvent);
        TestEnvironment.AssertReferenceField<MapEventSide, IFaction>(nameof(MapEventSide._mapFaction), referenceStringId: clanId, defaultValue: side._mapFaction);

        // Not currently synced (commented out in MapEventSideSync). Kept here for future reference -
        // enable the matching registration in MapEventSideSync, then uncomment the assertion.
        //TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownAtMapEventEnd), 5f);
        //TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide._requiresTroopCacheUpdate), true);
        //TestEnvironment.AssertReferenceField<MapEventSide, CharacterObject>(nameof(MapEventSide._selectedSimulationTroop));
        //TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide._selectedSimulationTroopIndex), 5);
        //TestEnvironment.AssertCollectionReferenceField<MapEventSide, MapEventParty>(nameof(MapEventSide._battleParties));
    }
}
