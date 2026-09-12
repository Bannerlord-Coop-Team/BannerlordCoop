using SandBox.Conversation.MissionLogics;
using SandBox;
using SandBox.Missions;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Hideout;
using SandBox.Missions.MissionLogics.Hideout.Objectives;
using SandBox.Objects.AnimationPoints;
using SandBox.Objects.AreaMarkers;
using SandBox.Objects.Usables;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.MissionLogics;

namespace Missions.Hideouts;

internal interface ICoopHideoutNativeController
{
    HideoutPhase Phase { get; }
    Agent Boss { get; }
    void Initialize(bool authority, bool migration);
    void TickAuthority(float dt);
    void Apply(HideoutPhase phase, Agent boss, int initialPopulation);
    void ResumeBossBattle();
}

internal sealed class CoopHideoutAssaultController : HideoutMissionController, ICoopHideoutNativeController
{
    private readonly CoopHideoutMissionLogic coop;

    public CoopHideoutAssaultController(CoopHideoutMissionLogic coop, IMissionTroopSupplier[] suppliers)
        : base(suppliers, BattleSideEnum.Attacker, 0, 0) => this.coop = coop;

    public HideoutPhase Phase => _battleResolved ? HideoutPhase.Resolved : _hideoutMissionState switch
    {
        HideoutMissionState.NotDecided => HideoutPhase.Waiting,
        HideoutMissionState.InitialFightBeforeBossFight when Mission.Mode == MissionMode.CutScene => HideoutPhase.Cinematic,
        HideoutMissionState.InitialFightBeforeBossFight => HideoutPhase.Camp,
        HideoutMissionState.WithoutBossFight => HideoutPhase.Camp,
        HideoutMissionState.CutSceneBeforeBossFight => HideoutPhase.Cinematic,
        HideoutMissionState.ConversationBetweenLeaders => HideoutPhase.Conversation,
        HideoutMissionState.BossFightWithDuel => HideoutPhase.Duel,
        _ => HideoutPhase.BossBattle,
    };

    public Agent Boss => _bossAgent;
    public override void OnMissionTick(float dt) { }

    public void Initialize(bool authority, bool migration)
    {
        _firstPhasePlayerSideTroopCount = coop.Attacker.NumTroopsNotSupplied;
        var model = Campaign.Current.Models.BanditDensityModel;
        _firstPhaseEnemyTroopCount = System.Math.Min((int)System.Math.Floor(coop.InitialPopulation *
            model.SpawnPercentageForFirstFightInHideoutMission), model.NumberOfMaximumTroopCountForFirstFightInHideout);
        if (authority && !migration)
        {
            base.OnMissionTick(0f);
            return;
        }
        _isMissionInitialized = true;
        _troopsInitialized = true;
        _areaMarkers.Clear();
        _patrolAreas.Clear();
        _areaMarkers.AddRange(Mission.ActiveMissionObjects.FindAllWithType<CommonAreaMarker>().OrderBy(area => area.AreaIndex));
        _patrolAreas.AddRange(Mission.ActiveMissionObjects.FindAllWithType<PatrolArea>().OrderBy(area => area.AreaIndex));
        Mission.GetMissionBehavior<MissionConversationLogic>().DisableStartConversation(true);
        Mission.DeploymentPlan.MakeDefaultDeploymentPlans();
    }

    public void TickAuthority(float dt)
    {
        if (coop.ShouldResumeBossBattle(Phase))
            ResumeBossBattle();
        base.OnMissionTick(dt);
    }

    public void Apply(HideoutPhase phase, Agent boss, int initialPopulation)
    {
        _bossAgent = boss;
        _enemyTeam = Mission.DefenderTeam;
        _battleResolved = phase == HideoutPhase.Resolved;
        _hideoutMissionState = phase switch
        {
            HideoutPhase.Waiting => HideoutMissionState.NotDecided,
            HideoutPhase.Cinematic => HideoutMissionState.CutSceneBeforeBossFight,
            HideoutPhase.Conversation => HideoutMissionState.ConversationBetweenLeaders,
            HideoutPhase.Duel => HideoutMissionState.BossFightWithDuel,
            HideoutPhase.BossBattle => HideoutMissionState.BossFightWithAll,
            _ => HideoutMissionState.InitialFightBeforeBossFight,
        };
    }

    public void ResumeBossBattle()
    {
        _enemyTeam = Mission.DefenderTeam;
        if (_bossAgent == null && coop.Defender.NumTroopsNotSupplied > 0)
            coop.WithLivingLeader(SpawnBossAndBodyguards);
        OnDuelOver(BattleSideEnum.Attacker);
        OnDuelOver(BattleSideEnum.Defender);
        Mission.SetMissionMode(MissionMode.Battle, false);
        StartBossFightBattleModeInternal();
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        // Native retreats everybody when its one main agent falls. Other players can still fight.
        if (!affectedAgent.IsMainAgent)
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        else
            _clearObjectiveTargetAgents.Remove(affectedAgent);
    }

    public override void OnAgentAlarmedStateChanged(Agent agent, Agent.AIStateFlag flag)
    {
        if (coop.IsAuthority && _defenderAgentObjects.ContainsKey(agent))
            base.OnAgentAlarmedStateChanged(agent, flag);
    }

    public override void OnEndMission() { }

    internal HideoutDefenderState CaptureDefender(Agent agent)
    {
        int pointId = -1;
        bool patrolling = false;
        if (_defenderAgentObjects.TryGetValue(agent, out var used))
        {
            patrolling = used.Machine is PatrolArea;
            pointId = (agent.CurrentlyUsedGameObject as StandingPoint)?.Id.Id ??
                used.Machine.StandingPoints.FirstOrDefault()?.Id.Id ?? -1;
        }
        return new HideoutDefenderState(agent.Origin.UniqueSeed, (int)agent.CurrentWatchState,
            agent.IsAlarmed(), pointId, patrolling);
    }

    internal void RestoreDefender(Agent agent, HideoutDefenderState state)
    {
        var point = Mission.MissionObjects.FirstOrDefault(value => value.Id.Id == state.StandingPointId) as StandingPoint;
        if (point != null && !_defenderAgentObjects.ContainsKey(agent))
            _missionSides[0].InitializeBanditAgent(agent, point, state.Patrolling, _defenderAgentObjects);
    }
}

internal sealed class CoopHideoutAmbushController : HideoutAmbushMissionController, ICoopHideoutNativeController, IMissionAgentSpawnLogic
{
    private readonly CoopHideoutMissionLogic coop;
    private bool locationListenerRegistered;

    public CoopHideoutAmbushController(CoopHideoutMissionLogic coop, IMissionTroopSupplier[] suppliers)
        : base(suppliers, BattleSideEnum.Attacker, 0) => this.coop = coop;

    public HideoutPhase Phase => _battleResolved ? HideoutPhase.Resolved : _currentHideoutMissionState switch
    {
        HideoutMissionState.NotDecided => HideoutPhase.Waiting,
        HideoutMissionState.StealthState when Mission.Mode == MissionMode.CutScene => HideoutPhase.Cinematic,
        HideoutMissionState.StealthState => HideoutPhase.Stealth,
        HideoutMissionState.CallTroopsCutSceneState => HideoutPhase.CallingTroops,
        HideoutMissionState.BattleBeforeBossFight when Mission.Mode == MissionMode.CutScene => HideoutPhase.Cinematic,
        HideoutMissionState.BattleBeforeBossFight => HideoutPhase.Camp,
        HideoutMissionState.CutSceneBeforeBossFight => HideoutPhase.Cinematic,
        HideoutMissionState.ConversationBetweenLeaders => HideoutPhase.Conversation,
        HideoutMissionState.BossFightWithDuel => HideoutPhase.Duel,
        _ => HideoutPhase.BossBattle,
    };

    public Agent Boss => _bossAgent;
    public BattleSideEnum PlayerSide => BattleSideEnum.Attacker;
    public void StartSpawner(BattleSideEnum side) { }
    public void StopSpawner(BattleSideEnum side) { }
    public bool IsSideSpawnEnabled(BattleSideEnum side) => false;
    bool IMissionAgentSpawnLogic.IsSideDepleted(BattleSideEnum side) => coop.IsSideDepleted(side);
    public float GetReinforcementInterval(BattleSideEnum side = BattleSideEnum.None) => 0f;
    public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum side) => _suppliers[(int)side].GetAllTroops();
    public bool GetSpawnHorses(BattleSideEnum side) => false;
    public int GetNumberOfPlayerControllableTroops() => coop.Attacker.GetNumberOfPlayerControllableTroops();
    public override void OnCreated() => Mission.DoesMissionRequireCivilianEquipment = false;
    public override void AfterStart() { }
    public override void OnMissionTick(float dt) { }

    public void Initialize(bool authority, bool migration)
    {
        if (!migration)
            InitializeMission();
        _isMissionInitialized = true;
        _troopsInitialized = true;
        _initialHideoutPopulation = coop.InitialPopulation;
        if (!authority) return;

        _playerTroopCount = coop.Attacker.NumTroopsNotSupplied;
        InitializeTroops();
        if (!locationListenerRegistered)
        {
            CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, PrepareSentries);
            locationListenerRegistered = true;
        }
        if (!migration)
        {
            Mission.GetMissionBehavior<MissionAgentHandler>().SpawnLocationCharacters();
            _remainingSentryCount = _stealthAreaData.Sum(area => area.StealthAreaMarkers.Values.Sum(agents => agents.Count));
            _sentryCount = _remainingSentryCount;
            Mission.GetMissionBehavior<StealthFailCounterMissionLogic>().FailCounterSeconds = 15f;
            _locateTheMainCampObjective = new LocateTheMainCampObjective(Mission);
            _missionObjectiveLogic.StartObjective(_locateTheMainCampObjective);
        }
        else if (Phase == HideoutPhase.CallingTroops)
        {
            coop.WithLivingLeader(ChangeHideoutMissionModeToBattle);
        }
    }

    private void PrepareSentries(Dictionary<string, int> unusedPoints)
    {
        var counts = new Dictionary<string, int>(unusedPoints);
        int available = System.Math.Max(0, _allEnemyTroops.Count - (_overriddenHideoutBossAgentOrigin == null ? 1 : 0));
        counts.TryGetValue("stealth_agent_forced", out int forced);
        forced = System.Math.Min(forced, available);
        counts["stealth_agent_forced"] = forced;
        counts.TryGetValue("stealth_agent", out int optional);
        counts["stealth_agent"] = System.Math.Min(optional, available - forced);
        LocationCharactersAreReadyToSpawn(counts);
    }

    public void TickAuthority(float dt)
    {
        if (coop.ShouldResumeBossBattle(Phase))
            ResumeBossBattle();
        if (Phase is HideoutPhase.Camp or HideoutPhase.BossBattle)
            coop.SpawnOrigins(_playerPriorTroops.Where(origin => !coop.WasSpawned(origin)));
        if (Mission.MainAgent?.IsActive() != true && _waitTimerToChangeStealthModeIntoBattle != null &&
            _waitTimerToChangeStealthModeIntoBattle.Check(Mission.CurrentTime))
        {
            coop.WithLivingLeader(ChangeHideoutMissionModeToBattle);
            _waitTimerToChangeStealthModeIntoBattle = null;
        }
        base.OnMissionTick(dt);
    }

    public void Apply(HideoutPhase phase, Agent boss, int initialPopulation)
    {
        _bossAgent = boss;
        _enemyTeam = Mission.DefenderTeam;
        _initialHideoutPopulation = initialPopulation;
        _battleResolved = phase == HideoutPhase.Resolved;
        _currentHideoutMissionState = phase switch
        {
            HideoutPhase.Waiting => HideoutMissionState.NotDecided,
            HideoutPhase.Stealth => HideoutMissionState.StealthState,
            HideoutPhase.CallingTroops => HideoutMissionState.CallTroopsCutSceneState,
            HideoutPhase.Camp => HideoutMissionState.BattleBeforeBossFight,
            HideoutPhase.Cinematic => HideoutMissionState.CutSceneBeforeBossFight,
            HideoutPhase.Conversation => HideoutMissionState.ConversationBetweenLeaders,
            HideoutPhase.Duel => HideoutMissionState.BossFightWithDuel,
            _ => HideoutMissionState.BossFightWithAll,
        };
    }

    public void ResumeBossBattle()
    {
        _enemyTeam = Mission.DefenderTeam;
        if (_bossAgent == null && (_allEnemyTroops?.Count > 0 || _overriddenHideoutBossAgentOrigin != null))
            coop.WithLivingLeader(SpawnBossAndBodyguards);
        OnDuelOver(BattleSideEnum.Attacker);
        OnDuelOver(BattleSideEnum.Defender);
        Mission.SetMissionMode(MissionMode.Battle, false);
        StartBossFightBattleModeInternal();
    }

    public override void OnObjectUsed(Agent userAgent, UsableMissionObject usedObject)
    {
        if (usedObject is StealthAreaUsePoint && userAgent == Mission.MainAgent)
            coop.RequestCallTroops(usedObject.Id.Id);
    }

    public void CallTroops(int usePointId)
    {
        var area = _stealthAreaData.FirstOrDefault(value => value.StealthAreaUsePoint.Id.Id == usePointId);
        if (area == null || Phase != HideoutPhase.Stealth) return;
        if (area.StealthAreaMarkers.Values.Any(agents => agents.Any(agent => agent.IsActive()))) return;
        var areas = Mission.GetMissionBehavior<CoopHideoutStealthAreaLogic>();
        if (areas == null || !areas.CallReinforcements(Mission.MainAgent, area.StealthAreaUsePoint)) return;
        foreach (var agent in _stealthAreaData.SelectMany(value => value.StealthAreaMarkers.Values).SelectMany(agents => agents))
            coop.RecordHidden(agent);
        base.OnObjectUsed(Mission.MainAgent, area.StealthAreaUsePoint);
    }

    internal void RestoreDefender(Agent agent)
    {
        var component = agent.GetComponent<CampaignAgentComponent>();
        if (component == null)
        {
            component = new CampaignAgentComponent(agent);
            agent.AddComponent(component);
        }
        if (component.AgentNavigator == null)
        {
            component.CreateAgentNavigator();
            SandBoxManager.Instance.AgentBehaviorManager.AddStealthAgentBehaviors(agent);
        }
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (!affectedAgent.IsMainAgent)
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        else
            _clearObjectiveTargetAgents.Remove(affectedAgent);
        foreach (var area in _stealthAreaData)
            foreach (var marker in area.StealthAreaMarkers)
                if (marker.Value.Contains(affectedAgent))
                    area.RemoveAgentFromStealthAreaMarker(marker.Key, affectedAgent);
        _remainingSentryCount = _stealthAreaData.Sum(area => area.StealthAreaMarkers.Values.Sum(agents => agents.Count));
    }

    public override void OnEndMission()
    {
        CampaignEventDispatcher.Instance.RemoveListeners(this);
        Game.Current.EventManager.UnregisterEvent<OnStealthMissionCounterFailedEvent>(OnStealthMissionCounterFailed);
    }
}

internal sealed class CoopHideoutStealthAreaLogic : StealthAreaMissionLogic
{
    // The controller sends the player's request to the host before this native callback can spawn troops.
    public override void OnObjectUsed(Agent userAgent, UsableMissionObject usedObject) { }

    public bool CallReinforcements(Agent userAgent, StealthAreaUsePoint point)
    {
        var area = _stealthAreaData.FirstOrDefault(value => value.StealthAreaUsePoint == point);
        if (area == null || area.IsReinforcementCalled) return false;
        base.OnObjectUsed(userAgent, point);
        return area.IsReinforcementCalled;
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        foreach (var area in _stealthAreaData)
            foreach (var marker in area.StealthAreaMarkers)
                if (marker.Value.Contains(affectedAgent))
                    area.RemoveAgentFromStealthAreaMarker(marker.Key, affectedAgent);
    }
}
