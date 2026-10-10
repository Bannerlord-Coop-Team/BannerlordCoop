using Common;
using Common.Util;
using GameInterface.Services.BesiegerCamps;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngineConstructionProgressService;
using GameInterface.Services.SiegeEngines;
using GameInterface.Services.SiegeEngines.Patches;
using GameInterface.Services.SiegeEnginesConstructionProgress.Patches;
using GameInterface.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEvents;

internal readonly struct SiegeEventGraphSnapshot
{
    public string SiegeEventId { get; }
    public uint SiegeEventHandle { get; }
    public uint SettlementHandle { get; }
    public string BesiegerCampId { get; }
    public uint BesiegerCampHandle { get; }
    public uint LeaderPartyHandle { get; }
    public string AttackerSiegeEnginesId { get; }
    public uint AttackerSiegeEnginesHandle { get; }
    public string DefenderSiegeEnginesId { get; }
    public uint DefenderSiegeEnginesHandle { get; }
    public long SiegeStartTimeTicks { get; }
    public string BesiegerStrategyId { get; }
    public int BesiegerTroopsKilled { get; }
    public uint[] BesiegerPartyHandles { get; }
    public SiegeEngineGraphSnapshot[] AttackerEngines { get; }
    public SiegeEngineGraphSnapshot[] DefenderEngines { get; }

    public bool IsComplete =>
        !string.IsNullOrEmpty(SiegeEventId) &&
        SiegeEventHandle != 0 &&
        SettlementHandle != 0 &&
        !string.IsNullOrEmpty(BesiegerCampId) &&
        BesiegerCampHandle != 0 &&
        LeaderPartyHandle != 0 &&
        !string.IsNullOrEmpty(AttackerSiegeEnginesId) &&
        AttackerSiegeEnginesHandle != 0 &&
        !string.IsNullOrEmpty(DefenderSiegeEnginesId) &&
        DefenderSiegeEnginesHandle != 0;

    public bool HasState => BesiegerPartyHandles != null;

    public SiegeEventGraphSnapshot(
        string siegeEventId,
        uint siegeEventHandle,
        uint settlementHandle,
        string besiegerCampId,
        uint besiegerCampHandle,
        uint leaderPartyHandle,
        string attackerSiegeEnginesId,
        uint attackerSiegeEnginesHandle,
        string defenderSiegeEnginesId,
        uint defenderSiegeEnginesHandle,
        long siegeStartTimeTicks = 0,
        string besiegerStrategyId = null,
        int besiegerTroopsKilled = 0,
        uint[] besiegerPartyHandles = null,
        SiegeEngineGraphSnapshot[] attackerEngines = null,
        SiegeEngineGraphSnapshot[] defenderEngines = null)
    {
        SiegeEventId = siegeEventId;
        SiegeEventHandle = siegeEventHandle;
        SettlementHandle = settlementHandle;
        BesiegerCampId = besiegerCampId;
        BesiegerCampHandle = besiegerCampHandle;
        LeaderPartyHandle = leaderPartyHandle;
        AttackerSiegeEnginesId = attackerSiegeEnginesId;
        AttackerSiegeEnginesHandle = attackerSiegeEnginesHandle;
        DefenderSiegeEnginesId = defenderSiegeEnginesId;
        DefenderSiegeEnginesHandle = defenderSiegeEnginesHandle;
        SiegeStartTimeTicks = siegeStartTimeTicks;
        BesiegerStrategyId = besiegerStrategyId;
        BesiegerTroopsKilled = besiegerTroopsKilled;
        BesiegerPartyHandles = besiegerPartyHandles;
        AttackerEngines = attackerEngines;
        DefenderEngines = defenderEngines;
    }
}

public enum SiegeEngineGraphLocation
{
    Preparation,
    Reserve,
    Ranged,
    Melee,
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SiegeEngineGraphSnapshot
{
    [ProtoMember(1)]
    public string Id { get; }
    [ProtoMember(2)]
    public uint Handle { get; }
    [ProtoMember(3)]
    public string EngineTypeId { get; }
    [ProtoMember(4)]
    public float Progress { get; }
    [ProtoMember(5)]
    public float RedeploymentProgress { get; }
    [ProtoMember(6)]
    public float Hitpoints { get; }
    [ProtoMember(7)]
    public float MaxHitpoints { get; }
    [ProtoMember(8)]
    public SiegeEngineGraphLocation Location { get; }
    [ProtoMember(9)]
    public int Index { get; }

    public SiegeEngineGraphSnapshot(
        string id,
        uint handle,
        string engineTypeId,
        float progress,
        float redeploymentProgress,
        float hitpoints,
        float maxHitpoints,
        SiegeEngineGraphLocation location,
        int index = -1)
    {
        Id = id;
        Handle = handle;
        EngineTypeId = engineTypeId;
        Progress = progress;
        RedeploymentProgress = redeploymentProgress;
        Hitpoints = hitpoints;
        MaxHitpoints = maxHitpoints;
        Location = location;
        Index = index;
    }
}

internal interface ISiegeEventGraphSynchronizer
{
    bool TryCapture(
        SiegeEvent siegeEvent,
        out SiegeEventGraphSnapshot snapshot,
        MobileParty fallbackLeaderParty = null);
    bool TryApply(SiegeEventGraphSnapshot snapshot);
}

internal sealed class SiegeEventGraphSynchronizer : ISiegeEventGraphSynchronizer
{
    private readonly IObjectManager objectManager;
    private readonly SiegeEventRegistry siegeEventRegistry;
    private readonly BesiegerCampRegistry besiegerCampRegistry;
    private readonly SiegeEnginesContainerRegistry siegeEnginesContainerRegistry;
    private readonly SiegeEngineConstructionProgressRegistry siegeEngineConstructionProgressRegistry;

    public SiegeEventGraphSynchronizer(
        IObjectManager objectManager,
        SiegeEventRegistry siegeEventRegistry,
        BesiegerCampRegistry besiegerCampRegistry,
        SiegeEnginesContainerRegistry siegeEnginesContainerRegistry,
        SiegeEngineConstructionProgressRegistry siegeEngineConstructionProgressRegistry)
    {
        this.objectManager = objectManager;
        this.siegeEventRegistry = siegeEventRegistry;
        this.besiegerCampRegistry = besiegerCampRegistry;
        this.siegeEnginesContainerRegistry = siegeEnginesContainerRegistry;
        this.siegeEngineConstructionProgressRegistry = siegeEngineConstructionProgressRegistry;
    }

    public bool TryCapture(
        SiegeEvent siegeEvent,
        out SiegeEventGraphSnapshot snapshot,
        MobileParty fallbackLeaderParty = null)
    {
        snapshot = default;

        var settlement = siegeEvent?.BesiegedSettlement;
        var camp = siegeEvent?.BesiegerCamp;
        var leaderParty = camp?.LeaderParty ?? fallbackLeaderParty;
        var attackerEngines = camp?.SiegeEngines;
        var defenderEngines = settlement?.SiegeEngines;

        if (!objectManager.TryGetIdWithLogging(siegeEvent, out var siegeEventId)
            || !objectManager.TryGetHandleWithLogging(siegeEvent, out var siegeEventHandle)
            || !objectManager.TryGetHandleWithLogging(settlement, out var settlementHandle)
            || !objectManager.TryGetIdWithLogging(camp, out var campId)
            || !objectManager.TryGetHandleWithLogging(camp, out var campHandle)
            || !objectManager.TryGetHandleWithLogging(leaderParty, out var leaderPartyHandle)
            || !objectManager.TryGetIdWithLogging(attackerEngines, out var attackerEnginesId)
            || !objectManager.TryGetHandleWithLogging(attackerEngines, out var attackerEnginesHandle)
            || !objectManager.TryGetIdWithLogging(defenderEngines, out var defenderEnginesId)
            || !objectManager.TryGetHandleWithLogging(defenderEngines, out var defenderEnginesHandle)
            || !TryCaptureParties(camp, leaderParty, out var besiegerPartyHandles)
            || !TryCaptureEngines(attackerEngines, out var attackerEngineStates)
            || !TryCaptureEngines(defenderEngines, out var defenderEngineStates)) return false;

        snapshot = new SiegeEventGraphSnapshot(
            siegeEventId,
            siegeEventHandle,
            settlementHandle,
            campId,
            campHandle,
            leaderPartyHandle,
            attackerEnginesId,
            attackerEnginesHandle,
            defenderEnginesId,
            defenderEnginesHandle,
            siegeEvent.SiegeStartTime.NumTicks,
            camp.SiegeStrategy?.StringId,
            camp.NumberOfTroopsKilledOnSide,
            besiegerPartyHandles,
            attackerEngineStates,
            defenderEngineStates);
        return true;
    }

    public bool TryApply(SiegeEventGraphSnapshot snapshot)
    {
        if (!snapshot.IsComplete
            || !objectManager.TryGetObjectWithLogging<Settlement>(snapshot.SettlementHandle, out var settlement)
            || !objectManager.TryGetObjectWithLogging<MobileParty>(snapshot.LeaderPartyHandle, out var leaderParty)) return false;

        bool hasSiegeEvent = objectManager.TryGetObject<SiegeEvent>(snapshot.SiegeEventHandle, out var siegeEvent);
        bool hasCamp = objectManager.TryGetObject<BesiegerCamp>(snapshot.BesiegerCampHandle, out var camp);
        bool hasAttackerEngines = objectManager.TryGetObject<SiegeEnginesContainer>(
            snapshot.AttackerSiegeEnginesHandle, out var attackerEngines);
        bool hasDefenderEngines = objectManager.TryGetObject<SiegeEnginesContainer>(
            snapshot.DefenderSiegeEnginesHandle, out var defenderEngines);
        bool needsRecovery = !hasSiegeEvent || !hasCamp || !hasAttackerEngines || !hasDefenderEngines;
        if (needsRecovery && !snapshot.HasState) return false;

        var attackerSnapshots = snapshot.AttackerEngines ??
            (snapshot.HasState ? Array.Empty<SiegeEngineGraphSnapshot>() : null);
        var defenderSnapshots = snapshot.DefenderEngines ??
            (snapshot.HasState ? Array.Empty<SiegeEngineGraphSnapshot>() : null);

        MobileParty[] besiegerParties = null;
        var attackerEngineTypes = new Dictionary<string, SiegeEngineType>();
        var defenderEngineTypes = new Dictionary<string, SiegeEngineType>();
        if (snapshot.HasState
            && (!TryResolveParties(snapshot.BesiegerPartyHandles, leaderParty, out besiegerParties)
                || !TryResolveEngineTypes(attackerSnapshots, out attackerEngineTypes)
                || !TryResolveEngineTypes(defenderSnapshots, out defenderEngineTypes))) return false;

        bool createdSiegeEvent = false;
        bool createdCamp = false;
        bool createdAttackerEngines = false;
        bool createdDefenderEngines = false;
        var createdProgresses = new List<(SiegeEngineConstructionProgress Instance, string Id)>();

        bool registered = objectManager.RunRegistrationTransaction(() =>
            TryGetOrCreate(snapshot.SiegeEventId, snapshot.SiegeEventHandle, out siegeEvent, out createdSiegeEvent)
            && TryGetOrCreate(snapshot.BesiegerCampId, snapshot.BesiegerCampHandle, out camp, out createdCamp)
            && TryGetOrCreate(snapshot.AttackerSiegeEnginesId, snapshot.AttackerSiegeEnginesHandle,
                out attackerEngines, out createdAttackerEngines)
            && TryGetOrCreate(snapshot.DefenderSiegeEnginesId, snapshot.DefenderSiegeEnginesHandle,
                out defenderEngines, out createdDefenderEngines)
            && TryRegisterProgresses(attackerSnapshots, createdProgresses)
            && TryRegisterProgresses(defenderSnapshots, createdProgresses));
        if (!registered) return false;

        if (createdSiegeEvent) siegeEventRegistry.OnClientCreated(siegeEvent, snapshot.SiegeEventId);
        if (createdCamp) besiegerCampRegistry.OnClientCreated(camp, snapshot.BesiegerCampId);
        if (createdAttackerEngines)
            siegeEnginesContainerRegistry.OnClientCreated(attackerEngines, snapshot.AttackerSiegeEnginesId);
        if (createdDefenderEngines)
            siegeEnginesContainerRegistry.OnClientCreated(defenderEngines, snapshot.DefenderSiegeEnginesId);
        foreach (var progress in createdProgresses)
        {
            siegeEngineConstructionProgressRegistry.OnClientCreated(progress.Instance, progress.Id);
        }

        using (new AllowedThread())
        {
            if (siegeEvent.BesiegedSettlement != settlement)
            {
                ReflectionUtils.SetPrivateField(
                    typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement), siegeEvent, settlement);
            }

            if (siegeEvent.BesiegerCamp != camp)
            {
                ReflectionUtils.SetPrivateField(
                    typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp), siegeEvent, camp);
            }

            if (settlement.SiegeEvent != siegeEvent) settlement.SiegeEvent = siegeEvent;
            if (camp.SiegeEvent != siegeEvent) camp.SiegeEvent = siegeEvent;
            if (camp._leaderParty != leaderParty) camp._leaderParty = leaderParty;
            if (leaderParty._besiegerCamp != camp)
            {
                leaderParty._besiegerCamp = camp;
                leaderParty.Party?.SetVisualAsDirty();
            }
            if (camp.SiegeEngines != attackerEngines) camp.SiegeEngines = attackerEngines;
            if (settlement.SiegeEngines != defenderEngines) settlement.SiegeEngines = defenderEngines;
            SiegeEnginesContainerShellPatches.InitializeShell(attackerEngines, BattleSideEnum.Attacker);
            SiegeEnginesContainerShellPatches.InitializeShell(defenderEngines, BattleSideEnum.Defender);
            if (snapshot.HasState)
            {
                siegeEvent.SiegeStartTime = new CampaignTime(snapshot.SiegeStartTimeTicks);
                camp._faction = leaderParty.MapFaction;
                camp.NumberOfTroopsKilledOnSide = snapshot.BesiegerTroopsKilled;

                if (!string.IsNullOrEmpty(snapshot.BesiegerStrategyId))
                {
                    var strategy = MBObjectManager.Instance.GetObject<SiegeStrategy>(snapshot.BesiegerStrategyId);
                    if (strategy != null) camp.SiegeStrategy = strategy;
                }

                var previousParties = camp._besiegerParties.ToArray();
                camp._besiegerParties.Clear();
                foreach (var party in previousParties)
                {
                    if (besiegerParties.Contains(party) || party?._besiegerCamp != camp) continue;

                    party._besiegerCamp = null;
                    party.Party?.SetVisualAsDirty();
                }

                foreach (var party in besiegerParties)
                {
                    if (party._besiegerCamp != camp)
                    {
                        party._besiegerCamp = camp;
                        party.Party?.SetVisualAsDirty();
                    }
                    if (!camp._besiegerParties.Contains(party)) camp._besiegerParties.Add(party);
                }

                ApplyEngineState(attackerEngines, attackerSnapshots, attackerEngineTypes);
                ApplyEngineState(defenderEngines, defenderSnapshots, defenderEngineTypes);
            }

            var siegeEvents = Campaign.Current?.SiegeEventManager?._siegeEvents;
            if (siegeEvents != null && !siegeEvents.Contains(siegeEvent)) siegeEvents.Add(siegeEvent);
        }

        settlement.Party?.SetVisualAsDirty();
        return true;
    }

    private bool TryCaptureParties(BesiegerCamp camp, MobileParty leaderParty, out uint[] handles)
    {
        var parties = new List<MobileParty>();
        if (camp?._besiegerParties != null) parties.AddRange(camp._besiegerParties);
        if (leaderParty != null && !parties.Contains(leaderParty)) parties.Add(leaderParty);

        handles = new uint[parties.Count];
        for (int i = 0; i < parties.Count; i++)
        {
            if (!objectManager.TryGetHandleWithLogging(parties[i], out handles[i])) return false;
        }

        return true;
    }

    private bool TryCaptureEngines(SiegeEnginesContainer container, out SiegeEngineGraphSnapshot[] snapshots)
    {
        var result = new List<SiegeEngineGraphSnapshot>();
        if (container?.SiegePreparations != null
            && !TryCaptureEngine(container.SiegePreparations, SiegeEngineGraphLocation.Preparation, -1, result))
        {
            snapshots = null;
            return false;
        }

        for (int i = 0; i < (container?.DeployedRangedSiegeEngines?.Length ?? 0); i++)
        {
            var engine = container.DeployedRangedSiegeEngines[i];
            if (engine != null && !TryCaptureEngine(engine, SiegeEngineGraphLocation.Ranged, i, result))
            {
                snapshots = null;
                return false;
            }
        }

        for (int i = 0; i < (container?.DeployedMeleeSiegeEngines?.Length ?? 0); i++)
        {
            var engine = container.DeployedMeleeSiegeEngines[i];
            if (engine != null && !TryCaptureEngine(engine, SiegeEngineGraphLocation.Melee, i, result))
            {
                snapshots = null;
                return false;
            }
        }

        foreach (var engine in container?.ReservedSiegeEngines ?? Enumerable.Empty<SiegeEngineConstructionProgress>())
        {
            if (!TryCaptureEngine(engine, SiegeEngineGraphLocation.Reserve, -1, result))
            {
                snapshots = null;
                return false;
            }
        }

        snapshots = result.ToArray();
        return true;
    }

    private bool TryCaptureEngine(
        SiegeEngineConstructionProgress engine,
        SiegeEngineGraphLocation location,
        int index,
        ICollection<SiegeEngineGraphSnapshot> snapshots)
    {
        if (!objectManager.TryGetIdWithLogging(engine, out var id)
            || !objectManager.TryGetHandleWithLogging(engine, out var handle)
            || string.IsNullOrEmpty(engine.SiegeEngine?.StringId)) return false;

        snapshots.Add(new SiegeEngineGraphSnapshot(
            id,
            handle,
            engine.SiegeEngine.StringId,
            engine.Progress,
            engine.RedeploymentProgress,
            engine.Hitpoints,
            engine.MaxHitPoints,
            location,
            index));
        return true;
    }

    private bool TryResolveParties(uint[] handles, MobileParty leaderParty, out MobileParty[] parties)
    {
        if (handles == null)
        {
            parties = new[] { leaderParty };
            return true;
        }

        parties = new MobileParty[handles.Length];
        for (int i = 0; i < handles.Length; i++)
        {
            if (!objectManager.TryGetObjectWithLogging(handles[i], out parties[i])) return false;
        }

        if (!parties.Contains(leaderParty)) parties = parties.Append(leaderParty).ToArray();
        return true;
    }

    private static bool TryResolveEngineTypes(
        SiegeEngineGraphSnapshot[] snapshots,
        out Dictionary<string, SiegeEngineType> engineTypes)
    {
        engineTypes = new Dictionary<string, SiegeEngineType>();
        if (snapshots == null) return true;

        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrEmpty(snapshot.Id)
                || snapshot.Handle == 0
                || string.IsNullOrEmpty(snapshot.EngineTypeId)
                || engineTypes.ContainsKey(snapshot.Id)) return false;

            var engineType = MBObjectManager.Instance.GetObject<SiegeEngineType>(snapshot.EngineTypeId);
            if (engineType == null) return false;
            engineTypes.Add(snapshot.Id, engineType);
        }

        return true;
    }

    private bool TryRegisterProgresses(
        SiegeEngineGraphSnapshot[] snapshots,
        ICollection<(SiegeEngineConstructionProgress Instance, string Id)> created)
    {
        if (snapshots == null) return true;
        foreach (var snapshot in snapshots)
        {
            if (objectManager.TryGetObject<SiegeEngineConstructionProgress>(snapshot.Handle, out _)) continue;

            var progress = ObjectHelper.SkipConstructor<SiegeEngineConstructionProgress>();
            if (!objectManager.AddExisting(snapshot.Id, progress, snapshot.Handle)) return false;
            created.Add((progress, snapshot.Id));
        }

        return true;
    }

    private void ApplyEngineState(
        SiegeEnginesContainer container,
        SiegeEngineGraphSnapshot[] snapshots,
        IReadOnlyDictionary<string, SiegeEngineType> engineTypes)
    {
        if (snapshots == null) return;

        for (int i = container.DeployedRangedSiegeEngines.Length - 1; i >= 0; i--)
        {
            if (container.DeployedRangedSiegeEngines[i] != null)
                SiegeEnginesContainerPatches.RunRemoveDeployedSiegeEngine(container, i, isRanged: true, moveToReserve: false);
        }
        for (int i = container.DeployedMeleeSiegeEngines.Length - 1; i >= 0; i--)
        {
            if (container.DeployedMeleeSiegeEngines[i] != null)
                SiegeEnginesContainerPatches.RunRemoveDeployedSiegeEngine(container, i, isRanged: false, moveToReserve: false);
        }
        while (container.ReservedSiegeEngines.Count > 0)
        {
            SiegeEnginesContainerPatches.RunRemovedSiegeEngineFromReserve(
                container, container.ReservedSiegeEngines[0]);
        }
        container._removedSiegeEngines.Clear();
        ReflectionUtils.SetPrivateField(
            typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations),
            container, null);

        foreach (var snapshot in snapshots)
        {
            objectManager.TryGetObject<SiegeEngineConstructionProgress>(snapshot.Handle, out var progress);
            if (progress.SiegeEngine != engineTypes[snapshot.Id])
            {
                ReflectionUtils.SetPrivateField(
                    typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.SiegeEngine),
                    progress, engineTypes[snapshot.Id]);
            }

            SiegeEngineProgressPatches.RunSetHitpoints(progress, snapshot.Hitpoints, snapshot.MaxHitpoints);
            SiegeEngineProgressPatches.RunSetProgress(progress, isRedeployment: false, snapshot.Progress);
            SiegeEngineProgressPatches.RunSetProgress(progress, isRedeployment: true, snapshot.RedeploymentProgress);

            if (snapshot.Location == SiegeEngineGraphLocation.Preparation)
            {
                ReflectionUtils.SetPrivateField(
                    typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations),
                    container, progress);
            }
            else if (snapshot.Location == SiegeEngineGraphLocation.Reserve)
            {
                SiegeEnginesContainerPatches.RunAddPrebuiltEngineToReserve(container, progress);
            }
            else
            {
                SiegeEnginesContainerPatches.RunDeploySiegeEngineAtIndex(container, progress, snapshot.Index);
            }

            if (progress.IsConstructed && progress.SiegeEngine.IsRanged && progress.RangedSiegeEngine == null)
            {
                var side = SiegeContainerLookup.FindOwnerSide(container);
                if (side != null) progress.SetRangedSiegeEngine(new RangedSiegeEngine(progress.SiegeEngine, side));
            }
        }
    }

    private bool TryGetOrCreate<T>(string id, uint handle, out T instance, out bool created) where T : class
    {
        created = false;
        if (objectManager.TryGetObject<T>(handle, out instance)) return true;

        instance = ObjectHelper.SkipConstructor<T>();
        if (!objectManager.AddExisting(id, instance, handle))
        {
            instance = null;
            return false;
        }

        created = true;
        return true;
    }
}
