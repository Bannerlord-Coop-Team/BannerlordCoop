using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace E2E.Tests.Services.MobileParties;
public class MobilePartyPropertyTests : SyncTestBase
{
    private string MobilePartyId;
    private string FirstParentPartyId;
    private string SecondParentPartyId;
    private string LordPartyId;

    public MobilePartyPropertyTests(ITestOutputHelper output) : base(output)
    {
        MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        FirstParentPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SecondParentPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<MobilePartyAi>();
        TestEnvironment.CreateRegisteredObject<BesiegerCamp>(new System.Collections.Generic.List<System.Reflection.MethodBase>
        {
            // These methods require full game state (Campaign/Siege infrastructure) not present in tests
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
            AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
        });
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_MobileParty_Properties()
    {
        Server.ObjectManager.TryGetObject(MobilePartyId, out MobileParty mobileParty);
        // Name property getter calls Hero.EncyclopediaLink which requires full game state (Campaign) not available in tests
        //TestEnvironment.AssertProperty<MobileParty, TextObject>(nameof(MobileParty.Name), new TextObject("customName"), mobileParty.Name);
        TestEnvironment.AssertReferenceProperty<MobileParty, Settlement>(nameof(MobileParty.LastVisitedSettlement));
        //TestEnvironment.AssertProperty<MobileParty, float>(nameof(MobileParty.Aggressiveness), 5f);
        TestEnvironment.AssertProperty<MobileParty, PartyObjective>(nameof(MobileParty.Objective), PartyObjective.Aggressive);
        //TestEnvironment.AssertReferenceProperty<MobileParty, MobilePartyAi>(nameof(MobileParty.Ai));
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsActive), false, true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsPartyTradeActive), true);
        TestEnvironment.AssertProperty<MobileParty, int>(nameof(MobileParty.PartyTradeGold), 5);
        TestEnvironment.AssertProperty<MobileParty, int>(nameof(MobileParty.PartyTradeTaxGold), 5);
        //TestEnvironment.AssertProperty<MobileParty, int>(nameof(MobileParty.VersionNo), 3);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.ShouldJoinPlayerBattles), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsDisbanding), true);
        TestEnvironment.AssertReferenceProperty<MobileParty, Settlement>(nameof(MobileParty.CurrentSettlement));
        Server.NetworkSentMessages.Clear();
        TestEnvironment.AssertReferenceProperty<MobileParty, MobileParty>(nameof(MobileParty.AttachedTo), MobilePartyId, FirstParentPartyId);
        AssertSingleAutoSyncMessageForPair("MobileParty_AttachedTo_SetNetworkMessage", "MobileParty__attachedTo_SetNetworkMessage");
        //TestEnvironment.AssertReferenceProperty<MobileParty, BesiegerCamp>(nameof(MobileParty.BesiegerCamp));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Scout));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Engineer));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Quartermaster));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Surgeon));
        TestEnvironment.AssertProperty<MobileParty, float>(nameof(MobileParty.RecentEventsMorale), 5f);
        TestEnvironment.AssertProperty<MobileParty, Vec2>(nameof(MobileParty.EventPositionAdder), new Vec2(2,2));
    }

    [Fact]
    public void Server_MobileParty_AttachedTo_ReparentsAndDetaches()
    {
        SetAttachedTo(FirstParentPartyId);
        AssertAttachmentState(FirstParentPartyId, SecondParentPartyId);

        SetAttachedTo(SecondParentPartyId);
        AssertAttachmentState(SecondParentPartyId, FirstParentPartyId);

        SetAttachedTo(null);
        AssertAttachmentState(null, FirstParentPartyId, SecondParentPartyId);
    }

    [Fact]
    public void Server_MobileParty_LordPartyTradeGold()
    {
        Server.Call(() =>
        {
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            hero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var party = LordPartyComponent.CreateLordParty(null, hero, new CampaignVec2(new Vec2(5, 5), true), 0, settlement, hero);
            Server.ObjectManager.TryGetId(party, out LordPartyId);
            party.PartyTradeGold = 500;
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(LordPartyId, out var clientParty));
            Assert.Equal(500, clientParty.LeaderHero.Gold);
            Assert.Equal(500, clientParty.PartyTradeGold);
        }
    }

    private void SetAttachedTo(string? parentPartyId)
    {
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var childParty));

            MobileParty? parentParty = null;
            if (parentPartyId != null)
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(parentPartyId, out parentParty));
            }

            childParty.AttachedTo = parentParty;
        });
        AssertSingleAutoSyncMessageForPair("MobileParty_AttachedTo_SetNetworkMessage", "MobileParty__attachedTo_SetNetworkMessage");
    }

    private void AssertAttachmentState(string? parentPartyId, params string[] detachedParentPartyIds)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var childParty));

                if (parentPartyId == null)
                {
                    Assert.Null(childParty.AttachedTo);
                }
                else
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(parentPartyId, out var parentParty));
                    Assert.Same(parentParty, childParty.AttachedTo);
                    Assert.Single(parentParty.AttachedParties, attachedParty => ReferenceEquals(attachedParty, childParty));
                }

                foreach (var detachedParentPartyId in detachedParentPartyIds)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(detachedParentPartyId, out var detachedParentParty));
                    Assert.DoesNotContain(childParty, detachedParentParty.AttachedParties);
                }
            });
        }
    }
}
