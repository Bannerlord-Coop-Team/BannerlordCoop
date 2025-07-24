using System.Reflection;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace E2E.Tests.Services.MobileParties;
public class MobilePartyPropertyTests : SyncTestBase
{
    private string MobilePartyId;

    public MobilePartyPropertyTests(ITestOutputHelper output) : base(output)
    {
        MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<MobilePartyAi>();
        TestEnvironment.CreateRegisteredObject<BesiegerCamp>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_MobileParty_Properties()
    {
        Server.ObjectManager.TryGetObject(MobilePartyId, out MobileParty mobileParty);
        TestEnvironment.AssertProperty<MobileParty, TextObject>(nameof(MobileParty.CustomName), new TextObject("customName"), mobileParty.CustomName);
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
        TestEnvironment.AssertReferenceProperty<MobileParty, MobileParty>(nameof(MobileParty.AttachedTo));
        //TestEnvironment.AssertReferenceProperty<MobileParty, BesiegerCamp>(nameof(MobileParty.BesiegerCamp));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Scout));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Engineer));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Quartermaster));
        TestEnvironment.AssertReferenceProperty<MobileParty, Hero>(nameof(MobileParty.Surgeon));
        TestEnvironment.AssertProperty<MobileParty, float>(nameof(MobileParty.RecentEventsMorale), 5f);
        TestEnvironment.AssertProperty<MobileParty, Vec2>(nameof(MobileParty.EventPositionAdder), new Vec2(2,2));
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsMilitia), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsLordParty), false, true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsVillager), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsCaravan), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsGarrison), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsCustomParty), true);
        TestEnvironment.AssertProperty<MobileParty, bool>(nameof(MobileParty.IsBandit), true);
    }
}