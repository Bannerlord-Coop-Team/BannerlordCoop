using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Utils.NetworkEvents;
using HarmonyLib;
using Helpers;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace E2E.Tests.Services.MobileParties;

public class DeserterPartyQuestUseTests : SyncTestBase
{
    private const string QuestUseMessageName = "MobileParty__isCurrentlyUsedByAQuest_SetNetworkMessage";
    private readonly ITestOutputHelper output;

    public DeserterPartyQuestUseTests(ITestOutputHelper output) : base(output)
    {
        this.output = output;
    }

    [Fact]
    public void NativeQuestConstructor_ReplicatesQuestUseToBothOriginalClientParties()
    {
        var giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townComponentId = TestEnvironment.CreateRegisteredObject<Town>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            UseNavigationMesh();
            var giver = Server.GetRegisteredObject<Hero>(giverId);
            var owner = Server.GetRegisteredObject<Hero>(ownerId);
            var village = Server.GetRegisteredObject<Village>(villageId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var town = Server.GetRegisteredObject<Settlement>(townId);
            var troop = Server.GetRegisteredObject<CharacterObject>(troopId);
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            template.Stacks.Clear();
            template.Stacks.Add(new PartyTemplateStack(troop, 24, 48));

            using (new AllowedThread())
            {
                town.SetSettlementComponent(Server.GetRegisteredObject<Town>(townComponentId));
                village.Bound = town;
                settlement.SetSettlementComponent(village);
                settlement.Culture = troop.Culture;
                troop.Culture.DefaultPartyTemplate = template;
                giver.StayingInSettlement = settlement;
                giver.Occupation = Occupation.Headman;
                owner._partyBelongedTo = Server.GetRegisteredObject<MobileParty>(ownerPartyId);
            }

            var deserters = Clan.CreateClan("deserters");
            deserters.StringId = "deserters";
            deserters.IsBanditFaction = true;
            deserters.Culture = troop.Culture;
            Assert.Contains(deserters, Clan.BanditFactions);
        });

        Server.NetworkSentMessages.Clear();
        string partyId = null!;
        uint handle = 0;
        Server.Call(() =>
        {
            var owner = Server.GetRegisteredObject<Hero>(ownerId);
            using var ownerScope = new MainHeroSubstitutionScope(owner,
                Server.GetRegisteredObject<MobileParty>(ownerPartyId));
            Assert.False(CallOriginalPolicy.IsOriginalAllowed());

            // Leave the native caller and setter untouched after the environment's normal patch installation.
            var quest = new Quest("deserter_quest_use", Server.GetRegisteredObject<Hero>(giverId),
                0f, 100, CampaignTime.DaysFromNow(10));
            var party = Assert.IsType<MobileParty>(quest._deserterMobileParty);
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetHandle(party, out handle));
            Assert.Same(party, Server.GetRegisteredObject<MobileParty>(partyId));
            Assert.True(party.IsActive);
            Assert.True(party.IsCurrentlyUsedByAQuest);
            Assert.Equal(24, party.MemberRoster.TotalManCount);
            output.WriteLine($"server party={partyId} handle={handle} questUsed={party.IsCurrentlyUsedByAQuest}");
        }, new[] { AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)) });

        var originals = new Dictionary<EnvironmentInstance, MobileParty>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var party = client.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(handle, client.GetHandle(party));
                originals.Add(client, party);
                output.WriteLine($"client party={partyId} handle={handle} questUsed={party.IsCurrentlyUsedByAQuest}");
            });
        }
        Assert.Equal(2, originals.Count);

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var party = client.GetRegisteredObject<MobileParty>(partyId);
                Assert.Same(originals[client], party);
                Assert.True(party.IsActive);
                Assert.True(party.IsCurrentlyUsedByAQuest);
            });
        }

        Assert.Single(Server.NetworkSentMessages.GetMessages<GenericNetworkEvent<MobileParty, byte[]>>()
            .Where(message => message.GetType().Name == QuestUseMessageName && message.InstanceId == handle));
    }

    private static void UseNavigationMesh()
    {
        var scene = new Mock<IMapScene>();
        scene.Setup(map => map.GetFaceIndex(in It.Ref<CampaignVec2>.IsAny))
            .Returns(new PathFaceRecord(1, 1, 1));
        scene.Setup(map => map.GetFaceTerrainType(It.IsAny<PathFaceRecord>())).Returns(TerrainType.Plain);
        var minimum = new Vec2(-10000, -10000);
        var maximum = new Vec2(10000, 10000);
        float height = 0;
        scene.Setup(map => map.GetMapBorders(out minimum, out maximum, out height));
        float distance = 1;
        scene.Setup(map => map.GetPathDistanceBetweenAIFaces(It.IsAny<PathFaceRecord>(),
            It.IsAny<PathFaceRecord>(), It.IsAny<Vec2>(), It.IsAny<Vec2>(), It.IsAny<float>(),
            It.IsAny<float>(), out distance, It.IsAny<int[]>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(true);
        Campaign.Current._mapSceneWrapper = scene.Object;
    }
}
