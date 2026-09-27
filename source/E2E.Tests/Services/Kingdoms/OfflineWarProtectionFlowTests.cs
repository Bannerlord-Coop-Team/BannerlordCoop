using Common.Util;
using Coop.Core.Server.Services.Kingdoms.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Configuration;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

/// <summary>
/// Drives AI war proposals through the live <see cref="Kingdom.AddDecision"/> patch on the server.
/// </summary>
public class OfflineWarProtectionFlowTests : IDisposable
{
    private const string RulerControllerId = "OfflineWarRuler";
    private const string VoterControllerId = "OfflineWarVoter";
    private const float StartingInfluence = 500f;

    private readonly ModOptions previousOptions = ModConfigProvider.ModOptions;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string aiKingdomId;
    private readonly string aiClanId;
    private readonly string playerKingdomId;

    public OfflineWarProtectionFlowTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        aiKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        playerKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        aiClanId = CreateLedClan("OfflineWarAiClan", controllerId: null);
        ConfigureClanInKingdom(aiClanId, aiKingdomId);

        // A connected player vassal makes the AI kingdom queue the vote instead of resolving it at once.
        string voterClanId = CreateLedClan(VoterControllerId, VoterControllerId);
        ConfigureClanInKingdom(voterClanId, aiKingdomId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), VoterControllerId);

        string rulerClanId = CreateLedClan(RulerControllerId, RulerControllerId);
        ConfigureClanInKingdom(rulerClanId, playerKingdomId);
    }

    public void Dispose()
    {
        ModConfigProvider.ModOptions = previousOptions;
        TestEnvironment.Dispose();
    }

    [Fact]
    public void AiWarProposal_AgainstOfflinePlayerKingdom_IsDroppedOnTheServer()
    {
        SetOption(true);

        ProposeWar();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(aiKingdomId, out Kingdom aiKingdom));
            Assert.True(Server.ObjectManager.TryGetObject(aiClanId, out Clan aiClan));
            Assert.True(Server.ObjectManager.TryGetObject(playerKingdomId, out Kingdom playerKingdom));

            Assert.Empty(aiKingdom.UnresolvedDecisions);
            Assert.Equal(StartingInfluence, aiClan.Influence);
            Assert.False(FactionManager.IsAtWarAgainstFaction(aiKingdom, playerKingdom));
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAddDecision>());
    }

    [Fact]
    public void AiWarProposal_AgainstOnlinePlayerKingdom_IsQueued()
    {
        SetOption(true);
        TestEnvironment.ConnectRegisteredPlayer(Clients.Skip(1).First(), RulerControllerId);

        ProposeWar();

        AssertWarProposalQueued();
    }

    [Fact]
    public void AiWarProposal_WithOptionOff_IsQueuedWhileRulerIsOffline()
    {
        SetOption(false);

        ProposeWar();

        AssertWarProposalQueued();
    }

    private static void SetOption(bool enabled)
    {
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { BlockAiWarDeclarationsOnOfflinePlayers = enabled });
    }

    private void ProposeWar()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(aiKingdomId, out Kingdom aiKingdom));
            Assert.True(Server.ObjectManager.TryGetObject(aiClanId, out Clan aiClan));
            Assert.True(Server.ObjectManager.TryGetObject(playerKingdomId, out Kingdom playerKingdom));

            using (new AllowedThread())
            {
                aiClan._influence = StartingInfluence;
            }

            aiKingdom.AddDecision(new DeclareWarDecision(aiClan, playerKingdom));
        });
    }

    private void AssertWarProposalQueued()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(aiKingdomId, out Kingdom aiKingdom));
            Assert.True(Server.ObjectManager.TryGetObject(aiClanId, out Clan aiClan));
            Assert.True(Server.ObjectManager.TryGetObject(playerKingdomId, out Kingdom playerKingdom));

            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(aiKingdom.UnresolvedDecisions));
            Assert.Same(playerKingdom, decision.FactionToDeclareWarOn);
            Assert.True(aiClan.Influence < StartingInfluence, "a queued proposal pays its influence cost");
        });
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkAddDecision>(),
            message => message.KingdomId == aiKingdomId);
    }

    private string CreateLedClan(string name, string? controllerId)
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject(clanId, out Clan clan));
                Assert.True(instance.ObjectManager.TryGetObject(heroId, out Hero hero));
                Assert.True(instance.ObjectManager.TryGetObject(partyId, out MobileParty party));
                Assert.True(instance.ObjectManager.TryGetObject(characterId, out CharacterObject character));

                using (new AllowedThread())
                {
                    clan.Name = new TextObject(name);
                    hero.Clan = clan;
                    clan.SetLeader(hero);
                    character.HeroObject = hero;
                    hero.PartyBelongedTo = party;
                    party.ActualClan = clan;
                }

                if (controllerId == null) return;
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(
                    new Player(controllerId, heroId, partyId, clanId, characterId)));
            });
        }

        return clanId;
    }

    private void ConfigureClanInKingdom(string clanId, string kingdomId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject(clanId, out Clan clan));
                Assert.True(instance.ObjectManager.TryGetObject(kingdomId, out Kingdom kingdom));

                using (new AllowedThread())
                {
                    clan._kingdom = kingdom;
                    kingdom._rulingClan ??= clan;
                    kingdom._clans ??= new MBList<Clan>();
                    kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                    kingdom._activePolicies ??= new MBList<PolicyObject>();
                    if (!kingdom._clans.Contains(clan)) kingdom._clans.Add(clan);
                }
            });
        }
    }
}
