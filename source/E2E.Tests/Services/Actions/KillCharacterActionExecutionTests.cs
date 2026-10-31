using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Utils.Commands;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Actions;

/// <summary>
/// v1.5 moved the execution trait penalty into ExecutionCampaignBehavior, keyed on Hero.MainHero.
/// The server's KillCharacterAction replacement applies it to the executing player instead.
/// </summary>
public class KillCharacterActionExecutionTests : MapEventTestBase
{
    public KillCharacterActionExecutionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void PlayerExecutingALord_GetsTheBloodFeudTraitPenalty(int victimHonor)
    {
        var (killerId, _) = CreatePlayerHeroParty("Executioner");
        var victimId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var killerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            var killer = Server.GetRegisteredObject<Hero>(killerId);
            var victim = Server.GetRegisteredObject<Hero>(victimId);
            var honorId = RegisterTrait(DefaultTraits.Honor);
            var mercyId = RegisterTrait(DefaultTraits.Mercy);
            var serverStore = new PropertyOwner<PropertyObject>();
            Campaign.Current.PlayerTraitDeveloper = serverStore;

            using (new AllowedThread())
            {
                var victimClan = Server.GetRegisteredObject<Clan>(victimClanId);
                var killerClan = Server.GetRegisteredObject<Clan>(killerClanId);
                victimClan.InitMembers();
                killerClan.InitMembers();
                victim.Clan = victimClan;
                victimClan.SetLeader(Server.GetRegisteredObject<Hero>(victimLeaderId));
                killer.Clan = killerClan;
                victim.SetTraitLevel(DefaultTraits.Honor, victimHonor);
            }

            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            int expectedHonor = 0;
            int expectedMercy;
            if (victimHonor >= 0)
            {
                model.GetTraitLevelForTraitXp(killer, DefaultTraits.Honor, -1000, out _, out expectedHonor);
                model.GetTraitLevelForTraitXp(killer, DefaultTraits.Mercy, -500, out _, out expectedMercy);
            }
            else
            {
                model.GetTraitLevelForTraitXp(killer, DefaultTraits.Mercy, -500, out _, out var firstMercy);
                model.GetTraitLevelForTraitXp(killer, DefaultTraits.Mercy, firstMercy - 500, out _, out expectedMercy);
            }

            KillCharacterAction.ApplyInternal(victim, killer, KillCharacterAction.KillCharacterActionDetail.Executed, false, true);

            Assert.False(victim.IsAlive);
            var xp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.True(xp.TryGetValue(killerId, out var killerXp));
            killerXp.TryGetValue(honorId, out var honor);
            killerXp.TryGetValue(mercyId, out var mercy);
            Assert.True(expectedMercy < 0);
            Assert.Equal(victimHonor >= 0, expectedHonor < 0);
            Assert.Equal(expectedHonor, honor);
            Assert.Equal(expectedMercy, mercy);
            Assert.Equal(0, serverStore.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(0, serverStore.GetPropertyValue(DefaultTraits.Mercy));
        }, WithoutObituary);
    }

    [Fact]
    public void AiLordExecutingALord_GetsNoPlayerTraitXp()
    {
        var killerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var killerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            var killer = Server.GetRegisteredObject<Hero>(killerId);
            var victim = Server.GetRegisteredObject<Hero>(victimId);
            RegisterTrait(DefaultTraits.Honor);
            RegisterTrait(DefaultTraits.Mercy);

            using (new AllowedThread())
            {
                var victimClan = Server.GetRegisteredObject<Clan>(victimClanId);
                var killerClan = Server.GetRegisteredObject<Clan>(killerClanId);
                victimClan.InitMembers();
                killerClan.InitMembers();
                victim.Clan = victimClan;
                victimClan.SetLeader(Server.GetRegisteredObject<Hero>(victimLeaderId));
                killer.Clan = killerClan;
            }

            KillCharacterAction.ApplyInternal(victim, killer, KillCharacterAction.KillCharacterActionDetail.Executed, false, true);

            Assert.False(victim.IsAlive);
            var xp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.False(xp.ContainsKey(killerId));
        }, WithoutObituary);
    }

    [Fact]
    public void KillNpcCommandExecution_WithVanillaExecutionBehavior_CostsOnePenalty()
    {
        // kill_npc runs in the acting player's context; vanilla ExecutionCampaignBehavior must not see
        // that player as Hero.MainHero for an execution, or it charges the blood feud penalty again
        var (killerId, _) = CreatePlayerHeroParty("Executioner");
        var victimId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var victimClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var killerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            var killer = Server.GetRegisteredObject<Hero>(killerId);
            var victim = Server.GetRegisteredObject<Hero>(victimId);
            var honorId = RegisterTrait(DefaultTraits.Honor);
            var mercyId = RegisterTrait(DefaultTraits.Mercy);
            Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();

            using (new AllowedThread())
            {
                var victimClan = Server.GetRegisteredObject<Clan>(victimClanId);
                var killerClan = Server.GetRegisteredObject<Clan>(killerClanId);
                victimClan.InitMembers();
                killerClan.InitMembers();
                victim.Clan = victimClan;
                victimClan.SetLeader(Server.GetRegisteredObject<Hero>(victimLeaderId));
                killer.Clan = killerClan;
                victim.SetTraitLevel(DefaultTraits.Honor, 1);
            }

            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            model.GetTraitLevelForTraitXp(killer, DefaultTraits.Honor, -1000, out _, out var expectedHonor);
            model.GetTraitLevelForTraitXp(killer, DefaultTraits.Mercy, -500, out _, out var expectedMercy);

            var behavior = new ExecutionCampaignBehavior();
            behavior.RegisterEvents();
            try
            {
                var result = new HeroDebugCommand.HeroKillNpcCoopCommand(Server.Resolve<ICommandObjectResolver>(), Server.ObjectManager)
                    .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { victimId, "execution", "Executioner", killerId }));
                Assert.True(result.Succeeded, result.Output);
            }
            finally
            {
                Campaign.Current.CampaignEvents.RemoveListeners(behavior);
            }

            Assert.False(victim.IsAlive);
            var xp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.True(xp.TryGetValue(killerId, out var killerXp));
            killerXp.TryGetValue(honorId, out var honor);
            killerXp.TryGetValue(mercyId, out var mercy);
            Assert.True(expectedHonor < 0);
            Assert.Equal(expectedHonor, honor);
            Assert.Equal(expectedMercy, mercy);
        }, WithoutObituary.Append(AccessTools.Method(typeof(ExecutionCampaignBehavior), "OnBloodFeudStateChanged")).ToList());
    }

    // The obituary needs the encyclopedia, which the E2E campaign does not load.
    private IReadOnlyList<MethodBase> WithoutObituary => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.CreateObituary)))
        .ToList();

    private string RegisterTrait(TraitObject trait)
    {
        if (Server.ObjectManager.TryGetId(trait, out var existingId)) return existingId;

        var id = "execution_" + trait.StringId;
        var handle = Server.Resolve<TestNetworkRouter>().GetOrCreateFixtureHandle(id);
        Assert.True(Server.ObjectManager.AddExisting(id, trait, handle));
        return id;
    }
}
