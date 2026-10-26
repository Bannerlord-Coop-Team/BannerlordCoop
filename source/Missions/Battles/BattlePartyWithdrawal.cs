using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Takes a departed player's own party out of the battle on this client, as a withdrawal, not a casualty. At sea the ship
/// replicator calls it once the player's hulls are gone, so the hull removal withdraws their crew first.
/// </summary>
public interface IBattlePartyWithdrawal
{
    /// <summary>
    /// [Game thread] Fades out the departed controller's own-party troops still in the battle, whoever holds them now;
    /// <paramref name="wasHost"/> is whether it hosted the battle when it left.
    /// </summary>
    void WithdrawParty(string controllerId, bool wasHost);
}

/// <inheritdoc cref="IBattlePartyWithdrawal"/>
public class BattlePartyWithdrawal : IBattlePartyWithdrawal
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattlePartyWithdrawal>();

    private readonly ICoopMissionComponent missionComponent;
    private readonly IBattleSession session;
    private readonly IBattleTroopWithdrawal troopWithdrawal;
    private readonly IBattleAgentFader fader;
    private readonly IBattlePlayerPartyResolver playerParties;
    private readonly Action<IReadOnlyCollection<Guid>> authorityChanged;

    public BattlePartyWithdrawal(
        ICoopMissionComponent missionComponent,
        IBattleSession session,
        IBattleTroopWithdrawal troopWithdrawal,
        IBattleAgentFader fader,
        IBattlePlayerPartyResolver playerParties,
        Action<IReadOnlyCollection<Guid>> authorityChanged)
    {
        this.missionComponent = missionComponent;
        this.session = session;
        this.troopWithdrawal = troopWithdrawal;
        this.fader = fader;
        this.playerParties = playerParties;
        this.authorityChanged = authorityChanged;
    }

    // Selects by ownership (not side, BR-051, nor holder: at sea a host change may have re-keyed leftovers before this runs).
    // An unresolved party keeps a host's troops for the adoption and falls back to a non-host's originally assigned agents.
    public void WithdrawParty(string controllerId, bool wasHost)
    {
        if (string.IsNullOrEmpty(controllerId) || session.IsOwn(controllerId)) return;

        var registry = missionComponent.AgentRegistry;
        var controllers = registry.GetControllerIds();
        var troops = controllers.SelectMany(registry.GetAgents).ToArray();

        if (troops.Length == 0) return;
        if (Mission.Current == null) return;

        if (!playerParties.TryGetPlayerParty(controllerId, out var retreaterParty, out var retreaterHero))
        {
            if (wasHost)
            {
                Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of host {Controller}; its troops will be adopted instead of despawned", controllerId);
                return;
            }

            Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of {Controller}; despawning the agents originally assigned to it instead", controllerId);
        }

        int candidates = 0;
        var despawnedRiders = new HashSet<Agent>();
        foreach (var info in troops)
        {
            var agent = info.Agent;
            if (agent == null || agent.IsMount) continue;
            candidates++;

            bool isRetreatersOwn = retreaterParty != null
                ? playerParties.IsOwnPartyAgent(agent, retreaterParty, retreaterHero)
                : info.OriginalOwner == controllerId;
            if (!isRetreatersOwn || !troopWithdrawal.Deregister(agent)) continue;

            fader.FadeOut(agent, hideInstantly: false, hideMount: true);
            despawnedRiders.Add(agent);
        }

        // Include adopted riders' mounts. A departed non-host's remaining horses go with it; a host's other cavalry horses
        // stay registered so the promoted successor adopts them with their riders.
        foreach (var holderId in controllers)
            CleanUpDepartedMounts(holderId, despawnedRiders, allMounts: !wasHost && holderId == controllerId);

        LogWithdrawal(controllerId, wasHost, despawnedRiders.Count, candidates);
    }

    // Reports the withdrawal, and warns when a non-host's troops are still keyed to it but none matched its party. At sea
    // the hull removal withdraws the crew first, so a miss only matters while agents are still keyed to the leaver.
    private void LogWithdrawal(string controllerId, bool wasHost, int despawned, int candidates)
    {
        int stillKeyed = missionComponent.AgentRegistry.GetAgents(controllerId).Count(info => info.Agent != null && !info.Agent.IsMount);
        if (despawned > 0)
            Logger.Information("[BattleSync] Despawned {Count} withdrawn own-party troop(s) of {Role} {Controller}",
                despawned, wasHost ? "host" : "player", controllerId);
        else if (!wasHost && candidates > 0 && stillKeyed > 0)
            Logger.Warning("[BattleSync] Withdrawal of {Controller} matched 0 of its {Count} registered troop(s) — selection found no own-party agents; anything left is keyed to a controller that no longer answers", controllerId, stillKeyed);
    }

    // [Game thread] A horse keyed to a controller that no longer answers would be unkillable everywhere (hits route to its
    // authority), so the withdrawn riders' horses (or, with allMounts, every one of its horses) leave the registry with it.
    private void CleanUpDepartedMounts(string controllerId, HashSet<Agent> despawnedRiders, bool allMounts)
    {
        var registry = missionComponent.AgentRegistry;

        int removed = 0;
        int transferred = 0;
        var changedIds = new List<Guid>();
        foreach (var info in registry.GetAgents(controllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsMount) continue;

            var rider = agent.RiderAgent;
            bool riderWithdrew = rider != null && despawnedRiders.Contains(rider);
            if (!allMounts && !riderWithdrew) continue;

            // A live rider of another owner keeps the horse registered under that owner: every client resolves the same
            // rider, so they converge on one authority, and the horse's damage routing and death broadcast keep answering.
            bool ridden = rider != null && rider.IsActive() && !riderWithdrew;
            if (ridden
                && registry.TryGetAgentInfo(rider, out var riderInfo)
                && riderInfo.CurrentAuthority != controllerId
                && registry.TryTransferAuthority(riderInfo.CurrentAuthority, info.AgentId))
            {
                transferred++;
                changedIds.Add(info.AgentId);
                continue;
            }

            if (!ridden)
                fader.FadeOut(agent, hideInstantly: false, hideMount: true);

            registry.RemoveAgent(info.AgentId);
            removed++;
        }

        if (changedIds.Count > 0) authorityChanged?.Invoke(changedIds);
        if (removed > 0 || transferred > 0)
            Logger.Information("[BattleSync] Cleaned up registered mount(s) of departed {Controller}: {Removed} removed, {Transferred} transferred to their riders' owners",
                controllerId, removed, transferred);
    }
}
