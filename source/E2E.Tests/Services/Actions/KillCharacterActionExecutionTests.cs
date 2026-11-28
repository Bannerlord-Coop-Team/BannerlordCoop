using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Actions;

/// <summary>
/// ExecutionCampaignBehavior charges the execution trait penalty only to Hero.MainHero.
/// The server's KillCharacterAction replacement applies it to the executing player instead.
/// </summary>
public class KillCharacterActionExecutionTests : MapEventTestBase
{
    public KillCharacterActionExecutionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerExecutingALord_GetsTheBloodFeudTraitPenalty()
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
                // An honorable victim makes vanilla charge both honor and mercy.
                victim.SetTraitLevel(DefaultTraits.Honor, 1);
            }

            KillCharacterAction.ApplyInternal(victim, killer, KillCharacterAction.KillCharacterActionDetail.Executed, false, true);

            Assert.False(victim.IsAlive);
            var xp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.True(xp.TryGetValue(killerId, out var killerXp));
            killerXp.TryGetValue(honorId, out var honor);
            killerXp.TryGetValue(mercyId, out var mercy);
            Assert.True(honor < 0);
            Assert.True(mercy < 0);
            Assert.Equal(0, serverStore.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(0, serverStore.GetPropertyValue(DefaultTraits.Mercy));
        }, WithoutObituary);
    }

    // A rebel clan that loses its last town executes its imprisoned heroes, but vanilla only removes the main party's (#4894).
    [Theory]
    [InlineData(true, KillCharacterAction.KillCharacterActionDetail.Lost)]
    [InlineData(false, KillCharacterAction.KillCharacterActionDetail.Executed)]
    public void RebelClanWithoutTowns_ExecutesOnlyRebelsHeldByAiParties(bool heldByPlayer, KillCharacterAction.KillCharacterActionDetail expectedDeath)
    {
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var serverMainPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var rebelId = TestEnvironment.CreateRegisteredObject<Hero>();
        var rebelClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var captorClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        string captorId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(Server.GetRegisteredObject<MobileParty>(captorPartyId).LeaderHero, out captorId)));
        if (heldByPlayer) RegisterAsPlayerParty("RebelCaptor", captorId, captorPartyId);

        Server.Call(() =>
        {
            var captorParty = Server.GetRegisteredObject<MobileParty>(captorPartyId);
            var captorClan = Server.GetRegisteredObject<Clan>(captorClanId);
            var rebel = Server.GetRegisteredObject<Hero>(rebelId);
            var rebelClan = Server.GetRegisteredObject<Clan>(rebelClanId);
            RegisterTrait(DefaultTraits.Honor);
            RegisterTrait(DefaultTraits.Mercy);
            Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
            // The server's own main party, which no player leads
            Campaign.Current.MainParty = Server.GetRegisteredObject<MobileParty>(serverMainPartyId);

            using (new AllowedThread())
            {
                captorClan.InitMembers();
                captorParty.LeaderHero.Clan = captorClan;
                rebelClan.InitMembers();
                rebelClan.IsRebelClan = true;
                rebelClan.SetLeader(rebel);
            }
            TakePrisonerAction.Apply(captorParty.Party, rebel);

            new RebellionsCampaignBehavior().DailyTickClan(rebelClan);

            Assert.False(rebel.IsAlive);
            Assert.Equal(expectedDeath, rebel.DeathMark);
            var xp = Server.Resolve<ICoopSessionProvider>().CoopSession.PlayerTraitData.PlayerTraitXp;
            Assert.DoesNotContain(captorId, xp.Keys);
        }, WithoutObituary);
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
