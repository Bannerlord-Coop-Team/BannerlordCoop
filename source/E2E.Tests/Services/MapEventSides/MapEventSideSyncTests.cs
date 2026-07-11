using E2E.Tests.Services.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideSyncTests : MapEventTestBase
{
    private readonly string sideId;
    private readonly string clanId;

    public MapEventSideSyncTests(ITestOutputHelper output) : base(output)
    {
        sideId = CreateServerMapEventSide();
        TestEnvironment.CreateRegisteredObject<CharacterObject>();
        TestEnvironment.CreateRegisteredObject<PartyBase>();
        clanId = TestEnvironment.CreateRegisteredObject<Clan>();
    }

    [Fact]
    public void Server_MapEventSide_Sync()
    {
        Assert.True(Server.ObjectManager.TryGetObject(sideId, out MapEventSide side));

        // Synced fields (value types)
        TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide.TroopCasualties), 5, instanceStringId: sideId, defaultValue: side.TroopCasualties);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.InfluenceValue), 5f, instanceStringId: sideId, defaultValue: side.InfluenceValue);
        TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide.IsSurrendered), true, instanceStringId: sideId, defaultValue: side.IsSurrendered);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.LeaderSimulationModifier), 5f, instanceStringId: sideId, defaultValue: side.LeaderSimulationModifier);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownValue), 5f, instanceStringId: sideId, defaultValue: side.RenownValue);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.StrengthRatio), 5f, instanceStringId: sideId, defaultValue: side.StrengthRatio);

        // Synced properties
        TestEnvironment.AssertProperty<MapEventSide, float>(nameof(MapEventSide.CasualtyStrength), 5f, defaultValue: side.CasualtyStrength, instanceStringId: sideId);
        TestEnvironment.AssertProperty<MapEventSide, BattleSideEnum>(nameof(MapEventSide.MissionSide), BattleSideEnum.Defender, defaultValue: side.MissionSide, instanceStringId: sideId);
        TestEnvironment.AssertReferenceProperty<MapEventSide, PartyBase>(nameof(MapEventSide.LeaderParty), instanceStringId: sideId);

        // The parent MapEvent edge is immutable and is owned by aggregate initialization. _mapFaction remains
        // mutable, so resolve it against a registered Clan.
        TestEnvironment.AssertReferenceField<MapEventSide, IFaction>(nameof(MapEventSide._mapFaction), instanceStringId: sideId, referenceStringId: clanId, defaultValue: side._mapFaction);

        // Not currently synced (commented out in MapEventSideSync). Kept here for future reference -
        // enable the matching registration in MapEventSideSync, then uncomment the assertion.
        //TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownAtMapEventEnd), 5f);
        //TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide._requiresTroopCacheUpdate), true);
        //TestEnvironment.AssertReferenceField<MapEventSide, CharacterObject>(nameof(MapEventSide._selectedSimulationTroop));
        //TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide._selectedSimulationTroopIndex), 5);
        //TestEnvironment.AssertCollectionReferenceField<MapEventSide, MapEventParty>(nameof(MapEventSide._battleParties));
    }
}
