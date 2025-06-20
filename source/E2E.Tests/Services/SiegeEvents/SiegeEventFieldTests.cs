using System.Reflection;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEventTests : SyncTestBase
{
    private string siegeEventId;

    public SiegeEventTests(ITestOutputHelper output) : base(output)
    {
        siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
            AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
        });
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<BesiegerCamp>();
    }

    [Fact]
    public void Server_SiegeEvent_Fields()
    {
        //READ ONLY?????
        //TestEnvironment.AssertReferenceField<SiegeEvent, Settlement>(nameof(SiegeEvent.BesiegedSettlement));
        //TestEnvironment.AssertReferenceField<SiegeEvent, BesiegerCamp>(nameof(SiegeEvent.BesiegerCamp));
        TestEnvironment.AssertField<SiegeEvent, bool>(nameof(SiegeEvent._isBesiegerDefeated), true);
    }

    [Fact]
    public void Server_SiegeEvent_Properties()
    {
        TestEnvironment.Server.ObjectManager.TryGetObject(siegeEventId, out SiegeEvent siegeEvent);
        var defaultCampaignTime = new CampaignTime(200);
        siegeEvent.SiegeStartTime = defaultCampaignTime;
        TestEnvironment.AssertProperty<SiegeEvent, CampaignTime>(nameof(SiegeEvent.SiegeStartTime), new CampaignTime(500), defaultValue: defaultCampaignTime);
    }
}