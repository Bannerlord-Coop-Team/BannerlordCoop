using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

/// <summary>
/// The environment and server side setup shared by the battle wreckage tests.
/// </summary>
public abstract class BattleWreckageTestBase : IDisposable
{
    protected BattleWreckageTestBase(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    internal E2ETestEnvironment TestEnvironment { get; }
    internal EnvironmentInstance Server => TestEnvironment.Server;
    internal IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    // The test bootstrap pins CampaignTime.Now, so read the map time tracker as RomanceRetryCooldownTests does.
    // Unpatch it in a finally
    protected static Harmony UseMapClock()
    {
        var clock = new Harmony($"e2e.wreckage-clock.{Guid.NewGuid():N}");
        clock.Patch(AccessTools.PropertyGetter(typeof(CampaignTime), nameof(CampaignTime.Now)),
            postfix: new HarmonyMethod(typeof(BattleWreckageTestBase), nameof(ProvideCampaignTime)));
        return clock;
    }

    // Starts a day in, because the hourly expiry ignores a LastInteractionTime of CampaignTime.Zero. Run inside Server.Call
    protected static void StartMapClock()
    {
        Campaign.Current.MapTimeTracker._numTicks = CampaignTime.Days(1).NumTicks;
    }

    // Run inside Server.Call
    protected static void AdvanceHours(float hours)
    {
        Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Hours(hours).NumTicks;
    }

    protected static BattleWreckageCampaignBehavior AddBattleWreckageBehavior()
    {
        var behavior = new BattleWreckageCampaignBehavior();
        Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { behavior }));
        return behavior;
    }

    // The host has no party, so vanilla's MainParty and MainHero reads come from this party under substitution
    protected static Hero CreatePartyLeader()
    {
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        return hero;
    }

    // A registered player with no peer, which is how the player manager sees one that disconnected. Run inside Server.Call
    protected Hero CreateDisconnectedPlayerLeader(string controllerId)
    {
        var hero = CreatePartyLeader();
        Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
        Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, null, null, null)));
        Assert.True(Server.Resolve<IPlayerManager>().IsOwnerOfHeroDisconnected(hero));
        return hero;
    }

    protected string CreateServerWreckage()
    {
        string? battleWreckageId = null;

        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            SetSides(mapEvent,
                GameObjectCreator.CreateInitializedObject<MobileParty>(),
                GameObjectCreator.CreateInitializedObject<MobileParty>());

            BattleWreckage.CreateWreckage(mapEvent, BattleWreckage.WreckageType.Small, CampaignTime.Zero);

            var battleWreckage = Campaign.Current.Wreckages.Last();

            Assert.True(Server.ObjectManager.TryGetId(battleWreckage, out battleWreckageId));
        });

        Assert.NotNull(battleWreckageId);

        return battleWreckageId!;
    }

    // Creating one as Epic needs a settlement for its name, and the field is readonly
    protected static void SetWreckageType(BattleWreckage battleWreckage, BattleWreckage.WreckageType wreckageType)
    {
        AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory)).SetValue(battleWreckage, wreckageType);
    }

    private static void ProvideCampaignTime(ref CampaignTime __result) => __result = Campaign.Current.MapTimeTracker.Now;

    // The wreckage constructor reads both sides' leader parties and their names. Custom names skip the
    // lord party name lookup, which needs a fully built leader hero
    private static void SetSides(MapEvent mapEvent, MobileParty attackerParty, MobileParty defenderParty)
    {
        attackerParty.Party.SetCustomName(new TextObject("Attackers"));
        defenderParty.Party.SetCustomName(new TextObject("Defenders"));

        mapEvent._sides[(int)BattleSideEnum.Attacker] = new MapEventSide(mapEvent, BattleSideEnum.Attacker, attackerParty.Party);
        mapEvent._sides[(int)BattleSideEnum.Defender] = new MapEventSide(mapEvent, BattleSideEnum.Defender, defenderParty.Party);
    }
}
