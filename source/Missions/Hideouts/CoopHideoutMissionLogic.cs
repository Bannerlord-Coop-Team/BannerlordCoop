using Common;
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Missions.Services.Network;
using Missions.Messages;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

internal sealed class CoopHideoutMissionLogic : MissionLogic
{
    private readonly IBattleNetwork network;
    private readonly IMessageBroker broker;
    private readonly IMissionContext missionContext;
    private readonly CoopTroopSupplier defenderSource;
    private readonly CoopTroopSupplier attackerSource;
    private readonly bool direct;
    private readonly HideoutStateLedger ledger = new();
    private readonly HashSet<int> hidden = new();
    private readonly Dictionary<Agent, AgentControllerType> spectatorControllers = new();
    private readonly Dictionary<Agent, Team> spectatorTeams = new();
    private NetworkHideoutState pendingState;
    private CoopBattleController battle;
    private bool initialized;
    private bool reportedSuccessorReady;
    private bool deployed;
    private bool hadAttackers;
    private bool hadDefenders;
    private bool heroEntered;
    private bool duelSurrenderSent;
    private bool recoveringAuthority;
    private int authorityEpoch;
    private int speakerSeed;
    private long revision;
    private float snapshotTimer;
    private HideoutPhase lastPhase;
    private BattleSideEnum winner = BattleSideEnum.None;

    public HideoutTroopSupplier Defender { get; }
    public HideoutTroopSupplier Attacker { get; }
    public ICoopHideoutNativeController Native { get; set; }
    public bool IsAuthority => battle?.Session.IsLocalHost == true;
    public bool HasPhaseSnapshot => initialized && ledger.Latest != null;
    public int InitialPopulation => defenderSource.SideTotalTroops;

    public CoopHideoutMissionLogic(IBattleNetwork network, IMessageBroker broker, IMissionContext missionContext,
        CoopTroopSupplier defenderSource, CoopTroopSupplier attackerSource, bool direct)
    {
        this.network = network;
        this.broker = broker;
        this.missionContext = missionContext;
        this.defenderSource = defenderSource;
        this.attackerSource = attackerSource;
        this.direct = direct;
        Defender = new HideoutTroopSupplier(defenderSource, ledger);
        Attacker = new HideoutTroopSupplier(attackerSource, ledger);
        broker.Subscribe<NetworkHideoutState>(HandleState);
        broker.Subscribe<NetworkHideoutCallTroops>(HandleCallTroops);
    }

    public override void AfterStart() => battle = Mission.GetMissionBehavior<CoopBattleController>();

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (agent.IsHuman)
        {
            heroEntered |= agent.Character == Hero.MainHero?.CharacterObject;
            if (agent.Origin != null) ledger.RecordSpawn(agent.Origin.UniqueSeed);
            hadAttackers |= agent.Team?.Side == BattleSideEnum.Attacker;
            hadDefenders |= agent.Team?.Side == BattleSideEnum.Defender;
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (battle == null || !battle.Session.HasInstance || battle.Session.HostEpoch <= 0) return;
        TryApplyPending();
        // The assignment arrives before the successor's expanded reserve. An old explicit-empty
        // defender reply must not initialize the ambush's permanently cached troop lists.
        if (!ReservesReady(IsAuthority, attackerSource, defenderSource)) return;
        if (!IsAuthority && ledger.Latest == null) return;

        Attacker.Refresh(recoverStaged: false);
        if (IsAuthority)
            Defender.Refresh(recoverStaged: false);

        if (!initialized)
        {
            if (!direct)
            {
                Mission.DeploymentPlan.MakeDefaultDeploymentPlans();
                SpawnHero();
            }
            Native.Initialize(IsAuthority, migration: false);
            initialized = true;
            authorityEpoch = IsAuthority ? battle.Session.HostEpoch : 0;
            if (!IsAuthority)
                ApplyLatest();
        }
        else if (IsAuthority && authorityEpoch != battle.Session.HostEpoch)
        {
            authorityEpoch = battle.Session.HostEpoch;
            recoveringAuthority = true;
            Defender.ResetForMigration();
            Attacker.ResetForMigration();
            Native.Initialize(authority: true, migration: true);
            if (Native.Phase is HideoutPhase.Cinematic or HideoutPhase.Conversation or HideoutPhase.Duel)
            {
                RestoreSpectators();
                Native.ResumeBossBattle();
            }
            recoveringAuthority = false;
        }

        SpawnHero();
        if (ShouldSpawnEscorts(direct, Native.Phase))
            SpawnOrigins(Attacker.SupplyTroops(Attacker.NumTroopsNotSupplied));
        AssignTroopCommand();

        if (!deployed)
        {
            deployed = true;
            if (!battle.Deployment.IsCommitted)
                Mission.OnDeploymentFinished();
        }

        hadAttackers |= HasActive(BattleSideEnum.Attacker);
        hadDefenders |= HasActive(BattleSideEnum.Defender);
        if (IsAuthority)
        {
            if (Native.Phase == HideoutPhase.Duel)
            {
                if (IsSideDepleted(BattleSideEnum.Attacker)) winner = BattleSideEnum.Defender;
                else if (IsSideDepleted(BattleSideEnum.Defender)) winner = BattleSideEnum.Attacker;
                if (winner == BattleSideEnum.Attacker && !duelSurrenderSent)
                {
                    duelSurrenderSent = true;
                    MapEvent.PlayerMapEvent.DoSurrender(BattleSideEnum.Defender);
                }
            }
            Native.TickAuthority(dt);
            if (Native.Phase == HideoutPhase.Resolved && winner == BattleSideEnum.None)
                winner = HasActive(BattleSideEnum.Defender) ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            if (Native.Phase is HideoutPhase.Cinematic or HideoutPhase.Conversation or HideoutPhase.Duel)
                speakerSeed = Mission.MainAgent?.Origin?.UniqueSeed ?? speakerSeed;
            snapshotTimer -= dt;
            if (Native.Phase != lastPhase || snapshotTimer <= 0f)
            {
                lastPhase = Native.Phase;
                snapshotTimer = 0.5f;
                BroadcastState();
            }
        }
        else
        {
            ApplyLatest();
        }
        ApplySpectators();
        if (!reportedSuccessorReady && HasPhaseSnapshot)
        {
            reportedSuccessorReady = true;
            broker.Publish(this, new BattleMissionReady(battle.Session.InstanceId));
        }
    }

    private void SpawnHero()
    {
        var hero = Hero.MainHero;
        if (heroEntered || hero == null || hero.IsWounded || Native.Phase == HideoutPhase.Resolved) return;
        if (Mission.Agents.Any(agent => agent.IsActive() && agent.Character == hero.CharacterObject))
        {
            heroEntered = true;
            return;
        }
        var origin = Attacker.TakeHero(hero.CharacterObject, returningHero: ledger.Latest != null);
        if (origin == null) return;
        if (ledger.Latest?.ActiveHeroSeeds?.Contains(origin.UniqueSeed) == true) return;
        SpawnOrigins(new[] { origin }, returningHero: true);
        heroEntered = true;
        if (!direct && Mission.MainAgent != null)
        {
            Mission.MainAgent.SetClothingColor1(4279111698u);
            Mission.MainAgent.SetClothingColor2(4279111698u);
            using (new TransientEquipmentSyncScope())
                Mission.MainAgent.UpdateSpawnEquipmentAndRefreshVisuals(Hero.MainHero.StealthEquipment);
        }
    }

    public void SpawnOrigins(IEnumerable<IAgentOriginBase> origins, bool returningHero = false)
    {
        foreach (var origin in origins)
        {
            if (!returningHero && ledger.WasSpawned(origin.UniqueSeed)) continue;
            var ally = Mission.Agents.FirstOrDefault(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == BattleSideEnum.Attacker);
            Vec3? position = ally == null ? null : ally.Position + new Vec3(1f, 1f);
            // Native requires a direction whenever an explicit spawn position is supplied.
            Vec2? direction = ally?.GetMovementDirection();
            var agent = Mission.SpawnTroop(origin, true, true, false, false, 0, 0, true, true, position, direction);
            if (!direct && Native.Phase is HideoutPhase.Camp or HideoutPhase.BossBattle)
                agent.Formation?.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }
    }

    public bool WasSpawned(IAgentOriginBase origin) => origin != null && ledger.WasSpawned(origin.UniqueSeed);

    internal void AssignTroopCommand()
    {
        var hero = Mission.MainAgent;
        if (hero == null || !hero.IsActive() || hero.Team != Mission.PlayerTeam) return;
        // Native assigns ownership during its initial spawn, which joining players skip.
        foreach (var agent in Mission.Agents)
            if (agent.Team == Mission.PlayerTeam && agent.Formation != null && agent.Formation.PlayerOwner != hero)
                agent.Formation.PlayerOwner = hero;
    }

    internal static bool ShouldSpawnEscorts(bool direct, HideoutPhase phase)
        => phase != HideoutPhase.Resolved && (direct || phase is HideoutPhase.Camp or HideoutPhase.CallingTroops
            or HideoutPhase.Cinematic or HideoutPhase.Conversation or HideoutPhase.Duel or HideoutPhase.BossBattle);

    internal static bool ReservesReady(bool authority, CoopTroopSupplier attacker, CoopTroopSupplier defender)
        => attacker.IsPopulated && defender.IsPopulated &&
            (!authority || defender.TotalTroops == defender.SideTotalTroops);

    public bool PreserveStealthPosture => Native.Phase is HideoutPhase.Stealth ||
        direct && Native.Phase == HideoutPhase.Camp;

    public bool RestoreAdoptedAgent(Agent agent)
    {
        if (!PreserveStealthPosture) return false;
        agent.SetIsAIPaused(false);
        if (agent.Team?.Side != BattleSideEnum.Defender) return true;
        var state = ledger.Latest?.Defenders?.FirstOrDefault(value => value.Seed == agent.Origin?.UniqueSeed);
        if (state == null) return true;
        (Native as CoopHideoutAssaultController)?.RestoreDefender(agent, state);
        (Native as CoopHideoutAmbushController)?.RestoreDefender(agent);
        agent.SetWatchState((Agent.WatchState)state.WatchState);
        agent.SetAlarmState(state.Alarmed ? Agent.AIStateFlag.Alarmed : (Agent.AIStateFlag)0);
        return true;
    }

    public bool IsSideDepleted(BattleSideEnum side)
    {
        if (Native.Phase == HideoutPhase.Resolved && winner != BattleSideEnum.None)
            return side != winner;
        if (!initialized) return false;
        if (Native.Phase == HideoutPhase.CallingTroops) return false;
        if (Native.Phase == HideoutPhase.Duel)
            return side == BattleSideEnum.Attacker
                ? (Mission.Agents.FirstOrDefault(agent => agent.Origin?.UniqueSeed == speakerSeed) ?? Mission.MainAgent)?.IsActive() != true
                : Native.Boss?.IsActive() != true;
        bool active = HasActive(side);
        if (active) return false;
        return side == BattleSideEnum.Attacker ? hadAttackers : hadDefenders || InitialPopulation > 0;
    }

    private bool HasActive(BattleSideEnum side)
        => Mission.Agents.Any(agent => agent.IsHuman && agent.IsActive() && agent.Team?.Side == side);

    public bool ShouldResumeBossBattle(HideoutPhase phase)
    {
        if (recoveringAuthority) return phase is HideoutPhase.Cinematic or HideoutPhase.Conversation or HideoutPhase.Duel;
        bool leaderAlive = Mission.MainAgent?.IsActive() == true;
        if (leaderAlive || !HasActive(BattleSideEnum.Attacker)) return false;
        return (phase is HideoutPhase.Camp or HideoutPhase.Stealth or HideoutPhase.Cinematic) && IsSideDepleted(BattleSideEnum.Defender);
    }

    public void WithLivingLeader(Action action)
    {
        var original = Mission.MainAgent;
        if (original?.IsActive() == true)
        {
            action();
            return;
        }
        var replacement = Mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == BattleSideEnum.Attacker)
            .OrderByDescending(agent => agent.IsHero).FirstOrDefault();
        if (replacement == null) return;
        try
        {
            Mission.MainAgent = replacement;
            action();
        }
        finally
        {
            Mission.MainAgent = original;
        }
    }

    public void RecordHidden(Agent agent)
    {
        if (agent?.Origin != null)
            hidden.Add(agent.Origin.UniqueSeed);
    }

    public void RequestCallTroops(int usePointId)
    {
        if (battle == null || Native.Phase != HideoutPhase.Stealth) return;
        if (IsAuthority)
            (Native as CoopHideoutAmbushController)?.CallTroops(usePointId);
        else
            network.Send(battle.Session.HostControllerId,
                new NetworkHideoutCallTroops(battle.Session.InstanceId, battle.Session.OwnControllerId, usePointId));
    }

    private void HandleCallTroops(MessagePayload<NetworkHideoutCallTroops> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!IsAuthority || message.InstanceId != battle.Session.InstanceId ||
                !missionContext.ControllersInMission.Contains(message.ControllerId)) return;
            (Native as CoopHideoutAmbushController)?.CallTroops(message.UsePointId);
        });
    }

    private void HandleState(MessagePayload<NetworkHideoutState> payload)
    {
        var state = payload.What;
        GameThread.RunSafe(() =>
        {
            if (battle == null || state.InstanceId != battle.Session.InstanceId) return;
            if (state.HostEpoch > battle.Session.HostEpoch)
            {
                if (pendingState == null || state.HostEpoch > pendingState.HostEpoch ||
                    state.HostEpoch == pendingState.HostEpoch && state.Revision > pendingState.Revision)
                    pendingState = state;
                return;
            }
            if (ledger.TryAccept(state, battle.Session.InstanceId, battle.Session.HostControllerId, battle.Session.HostEpoch))
            {
                if (state.HiddenSeeds != null) hidden.UnionWith(state.HiddenSeeds);
                if (initialized) ApplyLatest();
            }
        });
    }

    private void TryApplyPending()
    {
        if (pendingState == null || pendingState.HostEpoch > battle.Session.HostEpoch) return;
        var state = pendingState;
        pendingState = null;
        if (ledger.TryAccept(state, battle.Session.InstanceId, battle.Session.HostControllerId, battle.Session.HostEpoch) &&
            state.HiddenSeeds != null)
            hidden.UnionWith(state.HiddenSeeds);
    }

    private void ApplyLatest()
    {
        var state = ledger.Latest;
        if (state == null || IsAuthority) return;
        var boss = Mission.Agents.FirstOrDefault(agent => agent.Origin?.UniqueSeed == state.BossSeed);
        Native.Apply(state.Phase, boss, state.InitialPopulation);
        speakerSeed = state.SpeakerSeed;
        winner = state.Winner;
        var mode = state.Phase switch
        {
            HideoutPhase.Stealth => MissionMode.Stealth,
            HideoutPhase.Cinematic or HideoutPhase.Conversation or HideoutPhase.CallingTroops => MissionMode.CutScene,
            _ => direct && state.Phase == HideoutPhase.Camp ? MissionMode.Stealth : MissionMode.Battle,
        };
        if (Mission.Mode != mode)
            Mission.SetMissionMode(mode, false);
        bool enemies = state.Phase is not (HideoutPhase.Cinematic or HideoutPhase.Conversation);
        Mission.AttackerTeam.SetIsEnemyOf(Mission.DefenderTeam, enemies);
        foreach (var agent in Mission.Agents)
            if (agent.IsActive() && agent.Origin != null && hidden.Contains(agent.Origin.UniqueSeed))
                agent.FadeOut(true, true);
        if (state.Phase == HideoutPhase.Resolved && winner != BattleSideEnum.None)
        {
            MapEvent.PlayerMapEvent.SetOverrideWinner(winner);
            Mission.GetMissionBehavior<BattleEndLogic>().ChangeCanCheckForEndCondition(true);
        }
    }

    private void ApplySpectators()
    {
        bool duel = Native.Phase == HideoutPhase.Duel;
        if (!duel)
        {
            RestoreSpectators();
            return;
        }

        int bossSeed = Native.Boss?.Origin?.UniqueSeed ?? ledger.Latest?.BossSeed ?? 0;
        foreach (var agent in Mission.Agents)
        {
            if (!agent.IsActive() || !agent.IsHuman || agent.Origin == null ||
                agent.Origin.UniqueSeed == speakerSeed || agent.Origin.UniqueSeed == bossSeed) continue;
            if (agent.Team != null && agent.Team.Side != BattleSideEnum.None && !spectatorTeams.ContainsKey(agent))
            {
                spectatorTeams.Add(agent, agent.Team);
                agent.SetTeam(Team.Invalid, true);
            }
            if (agent.Controller != AgentControllerType.None && !spectatorControllers.ContainsKey(agent))
            {
                spectatorControllers.Add(agent, agent.Controller);
                agent.Controller = AgentControllerType.None;
            }
        }
    }

    public void RestoreSpectators()
    {
        foreach (var pair in spectatorTeams)
            if (pair.Key.IsActive()) pair.Key.SetTeam(pair.Value, true);
        foreach (var pair in spectatorControllers)
            if (pair.Key.IsActive()) pair.Key.Controller = pair.Value;
        spectatorTeams.Clear();
        spectatorControllers.Clear();
    }

    private NetworkHideoutState CaptureState()
        => new(battle.Session.InstanceId, battle.Session.OwnControllerId, battle.Session.HostEpoch,
            ++revision, Native.Phase, Native.Boss?.Origin?.UniqueSeed ?? 0, speakerSeed,
            ledger.GetSpawnedSeeds(), winner, InitialPopulation, hidden.ToArray(),
            Mission.DefenderTeam.ActiveAgents.Where(agent => agent.IsHuman && agent.Origin != null)
                .Select(agent => (Native as CoopHideoutAssaultController)?.CaptureDefender(agent) ??
                    new HideoutDefenderState(agent.Origin.UniqueSeed, (int)agent.CurrentWatchState,
                        agent.IsAlarmed(), -1, false)).ToArray(),
            Mission.Agents.Where(agent => agent.IsActive() && agent.IsHero && agent.Origin != null)
                .Select(agent => agent.Origin.UniqueSeed).ToArray());

    private void BroadcastState()
    {
        var state = CaptureState();
        ledger.TryAccept(state, state.InstanceId, state.HostControllerId, state.HostEpoch);
        network.SendAll(state);
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (IsAuthority && initialized)
            network.Send(controllerId, CaptureState());
    }

    public void OnStealthFailure()
    {
        if (!IsAuthority || Native.Phase != HideoutPhase.Stealth) return;
        winner = BattleSideEnum.Defender;
        Native.Apply(HideoutPhase.Resolved, Native.Boss, InitialPopulation);
        MapEvent.PlayerMapEvent.SetOverrideWinner(winner);
        Mission.GetMissionBehavior<BattleEndLogic>().ChangeCanCheckForEndCondition(true);
        BroadcastState();
    }

    public override void OnRemoveBehavior()
    {
        broker.Unsubscribe<NetworkHideoutState>(HandleState);
        broker.Unsubscribe<NetworkHideoutCallTroops>(HandleCallTroops);
        base.OnRemoveBehavior();
    }
}
