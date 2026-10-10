using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using Missions.Agents;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Peer-side death application for a coop battle: when an owner reports one of its agents died
/// (<see cref="NetworkBattleAgentDied"/>), kill our puppet of it and deregister.
/// </summary>
public interface IPuppetDeathApplier : IDisposable
{
    /// <summary>
    /// [Game thread] Retry deaths awaiting puppet registration or acceptance by the native agent.
    /// </summary>
    void DrainPendingDeaths();
}

/// <inheritdoc cref="IPuppetDeathApplier"/>
public class PuppetDeathApplier : IPuppetDeathApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetDeathApplier>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IPuppetMountStateRepairer puppetMountStateRepairer;
    private readonly Dictionary<Guid, NetworkBattleAgentDied> pendingDeaths =
        new Dictionary<Guid, NetworkBattleAgentDied>();
    private readonly HashSet<Guid> completedDeaths = new HashSet<Guid>();

    public PuppetDeathApplier(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ICasualtyAttributionMap casualties,
        IPuppetMountStateRepairer puppetMountStateRepairer)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.casualties = casualties;
        this.puppetMountStateRepairer = puppetMountStateRepairer;

        messageBroker.Subscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);
        pendingDeaths.Clear();
        completedDeaths.Clear();
    }

    private void Handle_NetworkBattleAgentDied(MessagePayload<NetworkBattleAgentDied> payload)
    {
        Logger.Information(
            "[BattleDeath] Received death broadcast: agentId={AgentId} wounded={Wounded} " +
            "affectorAgentId={AffectorAgentId} damage={Damage} deathAction={DeathAction}",
            payload.What.AgentId,
            payload.What.Wounded,
            payload.What.AffectorAgentId,
            payload.What.InflictedDamage,
            payload.What.DeathAction);

        GameThread.RunSafe(() =>
        {
            if (!TryApplyDeath(payload.What))
            {
                pendingDeaths[payload.What.AgentId] = payload.What;
                string reason = Mission.Current == null ? "mission-unavailable" : "puppet-unregistered-or-active";
                Logger.Information(
                    "[BattleDeath] Deferring death: agentId={AgentId} reason={Reason} pendingDeaths={PendingDeaths}",
                    payload.What.AgentId,
                    reason,
                    pendingDeaths.Count);
            }
            else
            {
                pendingDeaths.Remove(payload.What.AgentId);
            }
        });
    }

    public void DrainPendingDeaths()
    {
        if (pendingDeaths.Count == 0) return;

        var deaths = new List<NetworkBattleAgentDied>(pendingDeaths.Values);
        foreach (var death in deaths)
        {
            if (TryApplyDeath(death))
                pendingDeaths.Remove(death.AgentId);
        }
    }

    private bool TryApplyDeath(NetworkBattleAgentDied death)
    {
        if (completedDeaths.Contains(death.AgentId)) return true;

        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(death.AgentId, out var info)) return false;
        Mission mission = Mission.Current;
        if (mission == null) return false;

        Agent agent = info.Agent;
        if (agent != null && agent.Mission != mission) return false;
        int agentIndex = -1;
        float healthBefore = -1f;
        float healthAfter = healthBefore;
        bool activeBefore = false;
        bool activeAfter = false;
        object mortalityBefore = null;
        object mortalityAfter = null;
        bool disableDying = mission.DisableDying;
        MissionMode missionMode = mission.Mode;
        int appliedDamage = death.InflictedDamage;
        bool appliedDeath = false;
        if (agent != null && agent.Mission == mission)
        {
            agentIndex = agent.Index;
            healthBefore = agent.Health;
            healthAfter = healthBefore;
            activeBefore = agent.IsActive();
            activeAfter = activeBefore;
            appliedDeath = activeBefore;
        }
        if (appliedDeath)
        {
            mortalityBefore = agent.CurrentMortalityState;
            // Native guards still allow Die below one health; otherwise avoid repeating hit effects.
            if (healthBefore >= 1f
                && (agent.CurrentMortalityState == Agent.MortalityState.Immortal
                    || disableDying
                    || missionMode == MissionMode.Conversation
                    || missionMode == MissionMode.CutScene))
            {
                return false;
            }

            Agent mount = agent.MountAgent;
            if (!pendingDeaths.ContainsKey(death.AgentId))
                LogMountState("before", agent, mount);

            Agent affectorAgent = null;
            if (death.AffectorAgentId != Guid.Empty
                && registry.TryGetAgentInfo(death.AffectorAgentId, out var affectorInfo))
            {
                affectorAgent = affectorInfo.Agent;
            }

            var blow = CreateReplicatedBlow(death, affectorAgent?.Index ?? -1);
            var killingBlow = death.DeathAction >= 0
                ? CreateReplicatedKillingBlow(blow, death.DeathAction)
                : default;
            // HandleBlow ignores zero-damage blows even when an active agent already has zero health.
            blow.InflictedDamage = Math.Max(1, Math.Max(blow.InflictedDamage, (int)Math.Ceiling(agent.Health)));
            appliedDamage = blow.InflictedDamage;
            var agentState = death.Wounded ? AgentState.Unconscious : AgentState.Killed;

            BattleSpawnGate.RunWithReplicatedDeath(
                agent,
                affectorAgent,
                killingBlow,
                agentState,
                () =>
                {
                    using (new AllowedThread())
                    {
                        agent.RegisterBlow(blow, default);
                    }
                });

            healthAfter = agent.Health;
            activeAfter = agent.IsActive();
            mortalityAfter = agent.CurrentMortalityState;
            if (activeAfter)
            {
                if (!pendingDeaths.ContainsKey(death.AgentId))
                {
                    Logger.Warning(
                        "[BattleDeath] Retaining rejected death for retry: agentId={AgentId} " +
                        "health={Health:0.0} mortality={Mortality} disableDying={DisableDying} missionMode={MissionMode}",
                        death.AgentId, healthAfter, mortalityAfter, disableDying, missionMode);
                }
                return false;
            }

            puppetMountStateRepairer.RepairAfterRiderDeath(mount);
            LogMountState("after", agent, mount);
        }

        // Deregister after the game-thread kill. Removing on the poll thread before the queued apply would
        // make the registry lookup fail and leave the puppet alive but unregistered.
        bool deregistered = registry.RemoveAgent(death.AgentId);
        if (agent == null)
        {
            Logger.Warning(
                "[BattleDeath] Registered death target has no native puppet: agentId={AgentId} " +
                "authority={Authority} movementIdentity={Scope}/{MovementId} deregistered={Deregistered}",
                death.AgentId,
                info.CurrentAuthority,
                info.MovementScopeId,
                info.MovementId,
                deregistered);
        }
        else if (appliedDeath)
        {
            Logger.Information(
                "[BattleDeath] Applied replicated death: agentId={AgentId} authority={Authority} " +
                "agentIndex={AgentIndex} wounded={Wounded} affectorAgentId={AffectorAgentId} " +
                "damage={Damage} deathAction={DeathAction} healthBefore={HealthBefore:0.0} " +
                "healthAfter={HealthAfter:0.0} activeBefore={ActiveBefore} activeAfter={ActiveAfter} " +
                "mortalityBefore={MortalityBefore} mortalityAfter={MortalityAfter} " +
                "disableDying={DisableDying} missionMode={MissionMode} deregistered={Deregistered}",
                death.AgentId,
                info.CurrentAuthority,
                agentIndex,
                death.Wounded,
                death.AffectorAgentId,
                appliedDamage,
                death.DeathAction,
                healthBefore,
                healthAfter,
                activeBefore,
                activeAfter,
                mortalityBefore,
                mortalityAfter,
                disableDying,
                missionMode,
                deregistered);
        }
        else
        {
            Logger.Information(
                "[BattleDeath] Puppet was already nonliving when its death arrived: agentId={AgentId} " +
                "authority={Authority} agentIndex={AgentIndex} health={Health:0.0} active={Active} " +
                "deregistered={Deregistered}",
                death.AgentId,
                info.CurrentAuthority,
                agentIndex,
                healthBefore,
                activeBefore,
                deregistered);
        }

        if (!deregistered)
        {
            Logger.Warning(
                "[BattleDeath] Failed to deregister puppet after death replay: agentId={AgentId} " +
                "authority={Authority}",
                death.AgentId,
                info.CurrentAuthority);
        }
        casualties.Forget(death.AgentId);
        completedDeaths.Add(death.AgentId);
        return true;
    }

    private static void LogMountState(string phase, Agent rider, Agent mount)
    {
        if (mount == null) return;

        CommonAIComponent commonAi = mount.CommonAIComponent;
        int reservedRiderIndex = commonAi?.ReservedRiderAgentIndex ?? -1;
        Agent reservedRider = reservedRiderIndex >= 0
            ? Mission.Current.FindAgentWithIndex(reservedRiderIndex)
            : null;

        Logger.Information(
            "[DeathDiag] Mounted puppet death {Phase}: riderIndex={RiderIndex}, mountIndex={MountIndex}, " +
            "mountRiderIndex={MountRiderIndex}, mountActive={MountActive}, hasCommonAi={HasCommonAi}, " +
            "reservedRiderIndex={ReservedRiderIndex}, reservedRiderPresent={ReservedRiderPresent}, " +
            "reservedRiderActive={ReservedRiderActive}",
            phase,
            rider.Index,
            mount.Index,
            mount.RiderAgent?.Index ?? -1,
            mount.IsActive(),
            commonAi != null,
            reservedRiderIndex,
            reservedRider != null,
            reservedRider?.IsActive() ?? false);
    }

    private static Blow CreateReplicatedBlow(NetworkBattleAgentDied message, int ownerId)
    {
        var blow = new Blow(ownerId)
        {
            InflictedDamage = message.InflictedDamage,
            VictimBodyPart = message.VictimBodyPart,
        };
        // Death broadcasts carry no weapon. Slot zero makes vanilla dereference a mount's missing equipment.
        blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
        return blow;
    }

    private static KillingBlow CreateReplicatedKillingBlow(Blow blow, int deathAction)
    {
        return new KillingBlow(blow, Vec3.Zero, Vec3.Zero, deathAction, 0);
    }
}
