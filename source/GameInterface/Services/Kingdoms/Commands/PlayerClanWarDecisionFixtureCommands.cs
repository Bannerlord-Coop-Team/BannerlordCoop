#if DEBUG
using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

internal static class PlayerClanWarDecisionFixtureCommands
{
    private static PlayerClanWarDecisionFixture ActiveFixture;

    [CommandLineArgumentFunction("stage_player_clan_war_fixture", "coop.debug.kingdom")]
    public static string Stage(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (ActiveFixture != null) return "A player-clan war decision fixture is already active.";
        if (args.Count != 3)
        {
            return "Usage: coop.debug.kingdom.stage_player_clan_war_fixture <controllerId> <kingdomId> <targetKingdomId>";
        }
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IKingdomMembershipState>(out var membershipState))
        {
            return "Unable to resolve player, object, or kingdom membership services.";
        }
        if (!playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObject(player.ClanId, out Clan playerClan))
        {
            return $"Unable to resolve the registered player clan for controller {args[0]}.";
        }
        if (!objectManager.TryGetObject(args[1], out Kingdom kingdom) ||
            !objectManager.TryGetObject(args[2], out Kingdom targetKingdom))
        {
            return "Unable to resolve the fixture kingdoms.";
        }
        if (kingdom == targetKingdom || kingdom.IsAtWarWith(targetKingdom))
        {
            return "Fixture kingdoms must be distinct and at peace.";
        }
        if (kingdom.RulingClan == null)
        {
            return $"Kingdom {kingdom.StringId} has no ruling clan to restore.";
        }
        if (playerClan.Kingdom?.RulingClan == playerClan)
        {
            return $"Player clan {playerClan.StringId} currently rules its kingdom and cannot be moved safely.";
        }

        Clan proposerClan = kingdom.Clans.FirstOrDefault(clan =>
            clan != playerClan && !clan.IsUnderMercenaryService && !clan.IsPlayerClan());
        if (proposerClan == null)
        {
            return $"Kingdom {kingdom.StringId} has no eligible AI vassal clan.";
        }

        Dictionary<Clan, float> influenceSnapshots = kingdom.Clans
            .ToDictionary(clan => clan, clan => clan.Influence);
        influenceSnapshots[playerClan] = playerClan.Influence;

        var fixture = new PlayerClanWarDecisionFixture(
            args[0],
            playerClan,
            playerClan.Kingdom,
            kingdom,
            kingdom.RulingClan,
            proposerClan,
            targetKingdom,
            kingdom.LastKingdomDecisionConclusionDate,
            influenceSnapshots);
        ActiveFixture = fixture;

        membershipState.MoveClanToKingdom(
            fixture.PreviousKingdom,
            kingdom,
            playerClan,
            publishCollectionChanges: true);
        KingdomCollectionSync.SetRulingClan(kingdom, playerClan, publish: true);
        fixture.Decision = new DeclareWarDecision(proposerClan, targetKingdom);
        kingdom.AddDecision(fixture.Decision, ignoreInfluenceCost: true);
        if (!kingdom.UnresolvedDecisions.Contains(fixture.Decision))
        {
            RestoreCore(membershipState);
            return "Fixture failed because the player-clan decision bypassed the voting queue.";
        }

        return FormatStatus("staged");
    }

    [CommandLineArgumentFunction("timeout_player_clan_war_fixture", "coop.debug.kingdom")]
    public static string Timeout(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.timeout_player_clan_war_fixture";
        if (ActiveFixture?.Decision == null) return "No player-clan war decision fixture is active.";
        if (!ActiveFixture.Kingdom.UnresolvedDecisions.Contains(ActiveFixture.Decision))
        {
            return "The fixture decision is no longer pending.";
        }
        if (!ContainerProvider.TryResolve<IKingdomDecisionVoteManager>(out var voteManager) ||
            voteManager is not KingdomDecisionVoteManager concreteVoteManager ||
            !concreteVoteManager.TryPrepareNoWarTimeoutOutcome(
                ActiveFixture.Decision,
                ActiveFixture.AiSupporterClan))
        {
            return "Unable to prepare reversible AI no-war support for the timeout.";
        }

        ActiveFixture.AiSupportPrepared = true;
        ActiveFixture.Decision.TriggerTime = CampaignTime.HoursFromNow(-1);
        CoopKingdomDecisionProposalBehaviorPatch.ProcessDecisionDuringHourlySweep(
            ActiveFixture.Kingdom,
            ActiveFixture.Decision);
        if (ActiveFixture.Kingdom.UnresolvedDecisions.Contains(ActiveFixture.Decision))
        {
            return "The hourly sweep did not resolve the fixture decision after its vote timed out.";
        }
        if (ActiveFixture.Kingdom.IsAtWarWith(ActiveFixture.TargetKingdom))
        {
            return "The fixture unexpectedly declared war; the disposable runtime must be discarded.";
        }

        return FormatStatus("timed-out");
    }

    [CommandLineArgumentFunction("player_clan_war_fixture_status", "coop.debug.kingdom")]
    public static string Status(List<string> args)
    {
        if (args.Count == 0)
        {
            return ActiveFixture == null ? "fixture=inactive" : FormatStatus("active");
        }
        if (args.Count == 2)
        {
            return FormatLocalRulerStatus(args[0], args[1]);
        }
        return "Usage: coop.debug.kingdom.player_clan_war_fixture_status [controllerId kingdomId]";
    }

    [CommandLineArgumentFunction("restore_player_clan_war_fixture", "coop.debug.kingdom")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.restore_player_clan_war_fixture";
        if (ActiveFixture == null) return "fixture=inactive";
        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var membershipState))
        {
            return "Unable to resolve the kingdom membership service.";
        }
        if (ActiveFixture.Kingdom.IsAtWarWith(ActiveFixture.TargetKingdom))
        {
            return "fixture=not-restored; unexpected war transition requires discarding the disposable runtime";
        }

        RestoreCore(membershipState);
        return "fixture=restored";
    }

    private static string FormatStatus(string state)
    {
        var fixture = ActiveFixture;
        bool pending = fixture.Kingdom.UnresolvedDecisions.Contains(fixture.Decision);
        bool atWar = fixture.Kingdom.IsAtWarWith(fixture.TargetKingdom);
        bool triggerPast = fixture.Decision?.TriggerTime.IsPast ?? false;
        string rulerId = fixture.Kingdom.RulingClan?.StringId ?? "none";
        return $"fixture={state}; controller={fixture.ControllerId}; kingdom={fixture.Kingdom.StringId}; " +
            $"target={fixture.TargetKingdom.StringId}; playerClan={fixture.PlayerClan.StringId}; " +
            $"ruler={rulerId}; aiSupporter={fixture.AiSupporterClan.StringId}; pending={pending}; " +
            $"aiSupportPrepared={fixture.AiSupportPrepared}; rulerVote=missing; " +
            $"atWar={atWar}; triggerPast={triggerPast}";
    }

    private static void RestoreCore(IKingdomMembershipState membershipState)
    {
        var fixture = ActiveFixture;
        if (fixture == null) return;

        if (fixture.Decision != null && fixture.Kingdom.UnresolvedDecisions.Contains(fixture.Decision))
        {
            fixture.Kingdom.RemoveDecision(fixture.Decision);
        }
        KingdomCollectionSync.SetRulingClan(
            fixture.Kingdom,
            fixture.PreviousRulingClan,
            publish: true);
        membershipState.MoveClanToKingdom(
            fixture.PlayerClan.Kingdom,
            fixture.PreviousKingdom,
            fixture.PlayerClan,
            publishCollectionChanges: true);
        foreach (var influenceSnapshot in fixture.InfluenceSnapshots)
        {
            influenceSnapshot.Key.Influence = influenceSnapshot.Value;
        }
        fixture.Kingdom.LastKingdomDecisionConclusionDate = fixture.PreviousDecisionConclusionDate;
        ActiveFixture = null;
    }

    private static string FormatLocalRulerStatus(string controllerId, string kingdomId)
    {
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObject(player.ClanId, out Clan playerClan) ||
            !objectManager.TryGetObject(kingdomId, out Kingdom kingdom))
        {
            return "Unable to resolve the local player-clan ruler status.";
        }

        string rulerId = kingdom.RulingClan?.StringId ?? "none";
        return $"controller={controllerId}; kingdom={kingdom.StringId}; " +
            $"playerClan={playerClan.StringId}; ruler={rulerId}; " +
            $"playerIsRuler={kingdom.RulingClan == playerClan}";
    }

    private sealed class PlayerClanWarDecisionFixture
    {
        public string ControllerId { get; }
        public Clan PlayerClan { get; }
        public Kingdom PreviousKingdom { get; }
        public Kingdom Kingdom { get; }
        public Clan PreviousRulingClan { get; }
        public Clan AiSupporterClan { get; }
        public Kingdom TargetKingdom { get; }
        public CampaignTime PreviousDecisionConclusionDate { get; }
        public IReadOnlyDictionary<Clan, float> InfluenceSnapshots { get; }
        public DeclareWarDecision Decision { get; set; }
        public bool AiSupportPrepared { get; set; }

        public PlayerClanWarDecisionFixture(
            string controllerId,
            Clan playerClan,
            Kingdom previousKingdom,
            Kingdom kingdom,
            Clan previousRulingClan,
            Clan aiSupporterClan,
            Kingdom targetKingdom,
            CampaignTime previousDecisionConclusionDate,
            IReadOnlyDictionary<Clan, float> influenceSnapshots)
        {
            ControllerId = controllerId;
            PlayerClan = playerClan;
            PreviousKingdom = previousKingdom;
            Kingdom = kingdom;
            PreviousRulingClan = previousRulingClan;
            AiSupporterClan = aiSupporterClan;
            TargetKingdom = targetKingdom;
            PreviousDecisionConclusionDate = previousDecisionConclusionDate;
            InfluenceSnapshots = influenceSnapshots;
        }
    }
}
#endif
