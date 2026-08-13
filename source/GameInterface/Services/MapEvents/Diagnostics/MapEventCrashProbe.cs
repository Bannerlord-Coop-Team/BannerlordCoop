using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Diagnostics;

internal static class MapEventCrashProbe
{
    private const int Capacity = 32;
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(MapEventCrashProbe));
    private static readonly Slot[] Slots = CreateSlots();
    private static long sequence;
    private static long lastLogUtcTicks;
    private static int activationLogged;

    internal static void Record(string operation)
    {
        Record(operation, null, null, -1, -1);
    }

    internal static void RecordMapEvent(string operation, MapEvent mapEvent, int trackerState = -1)
    {
        string mapEventId = null;
        string componentType = null;
        int state = -1;
        int battleState = -1;

        try
        {
            mapEventId = mapEvent?.StringId;
            componentType = mapEvent?.Component?.GetType().FullName;
            if (mapEvent != null)
            {
                state = (int)mapEvent.State;
                battleState = (int)mapEvent.BattleState;
            }
        }
        catch (Exception exception)
        {
            componentType = $"capture-failed:{exception.GetType().Name}";
        }

        Record(operation, mapEventId, null, trackerState, -1, componentType, state, battleState);
    }

    internal static void RecordParty(string operation, MobileParty mobileParty, bool cachedHasMapEvent)
    {
        string partyId = null;
        string mapEventId = null;

        try
        {
            partyId = mobileParty?.StringId;
        }
        catch (Exception exception)
        {
            mapEventId = $"capture-failed:{exception.GetType().Name}";
        }

        Record(operation, mapEventId, partyId, -1, cachedHasMapEvent ? 1 : 0);
    }

    internal static void RecordException(string operation, Exception exception)
    {
        Record(operation, null, exception?.GetType().FullName, -1, -1);
    }

    [CrashInformationCollector.CrashInformationProvider]
    private static CrashInformationCollector.CrashInformation GetCrashInformation()
    {
        var snapshots = new List<Snapshot>(Capacity);
        foreach (Slot slot in Slots)
        {
            Snapshot snapshot = slot.Capture();
            if (snapshot.Sequence > 0)
                snapshots.Add(snapshot);
        }

        snapshots.Sort((left, right) => right.Sequence.CompareTo(left.Sequence));
        var values = new List<(string, string)>(snapshots.Count + 1)
        {
            ("Marker", "[MapEventCrashProbe]"),
        };

        for (int index = 0; index < snapshots.Count; index++)
        {
            Snapshot snapshot = snapshots[index];
            values.Add((
                "Boundary" + index.ToString("00", CultureInfo.InvariantCulture),
                snapshot.Format()));
        }

        return new CrashInformationCollector.CrashInformation(
            "BannerlordCoop MapEvent crash probe",
            new MBReadOnlyList<(string, string)>(values));
    }

    private static void Record(
        string operation,
        string mapEventId,
        string partyId,
        int trackerState,
        int cachedHasMapEvent,
        string componentType = null,
        int state = -1,
        int battleState = -1)
    {
        if (Interlocked.Exchange(ref activationLogged, 1) == 0)
            Logger.Information("[MapEventCrashProbe] active; the last {Capacity} campaign boundaries will be included in crash metadata", Capacity);

        long next = Interlocked.Increment(ref sequence);
        long utcTicks = DateTime.UtcNow.Ticks;
        Slots[(int)(next % Capacity)].TryWrite(
            next,
            operation,
            mapEventId,
            partyId,
            componentType,
            state,
            battleState,
            trackerState,
            cachedHasMapEvent,
            utcTicks);

        long previousLogTicks = Volatile.Read(ref lastLogUtcTicks);
        if (utcTicks - previousLogTicks >= TimeSpan.TicksPerSecond &&
            Interlocked.CompareExchange(ref lastLogUtcTicks, utcTicks, previousLogTicks) == previousLogTicks)
        {
            Logger.Warning(
                "[MapEventCrashProbe] seq={Sequence} operation={Operation} mapEvent={MapEventId} party={PartyId} tracker={TrackerState} cachedHasMapEvent={CachedHasMapEvent}",
                next,
                operation,
                mapEventId ?? "null",
                partyId ?? "null",
                trackerState,
                cachedHasMapEvent);
        }
    }

    private static Slot[] CreateSlots()
    {
        var slots = new Slot[Capacity];
        for (int index = 0; index < slots.Length; index++)
            slots[index] = new Slot();

        return slots;
    }

    private sealed class Slot
    {
        private long committedSequence;
        private int writing;
        private string operation;
        private string mapEventId;
        private string partyId;
        private string componentType;
        private int state;
        private int battleState;
        private int trackerState;
        private int cachedHasMapEvent;
        private int threadId;
        private long utcTicks;

        internal void TryWrite(
            long nextSequence,
            string nextOperation,
            string nextMapEventId,
            string nextPartyId,
            string nextComponentType,
            int nextState,
            int nextBattleState,
            int nextTrackerState,
            int nextCachedHasMapEvent,
            long nextUtcTicks)
        {
            if (Interlocked.CompareExchange(ref writing, 1, 0) != 0) return;

            Volatile.Write(ref committedSequence, 0);
            operation = nextOperation;
            mapEventId = nextMapEventId;
            partyId = nextPartyId;
            componentType = nextComponentType;
            state = nextState;
            battleState = nextBattleState;
            trackerState = nextTrackerState;
            cachedHasMapEvent = nextCachedHasMapEvent;
            threadId = Thread.CurrentThread.ManagedThreadId;
            utcTicks = nextUtcTicks;
            Volatile.Write(ref committedSequence, nextSequence);
            Volatile.Write(ref writing, 0);
        }

        internal Snapshot Capture()
        {
            if (Volatile.Read(ref writing) != 0) return default;

            long before = Volatile.Read(ref committedSequence);
            if (before == 0) return default;

            var snapshot = new Snapshot(
                before,
                operation,
                mapEventId,
                partyId,
                componentType,
                state,
                battleState,
                trackerState,
                cachedHasMapEvent,
                threadId,
                utcTicks);

            return Volatile.Read(ref writing) == 0 && before == Volatile.Read(ref committedSequence)
                ? snapshot
                : default;
        }
    }

    private readonly struct Snapshot
    {
        internal Snapshot(
            long sequenceValue,
            string operationValue,
            string mapEventIdValue,
            string partyIdValue,
            string componentTypeValue,
            int stateValue,
            int battleStateValue,
            int trackerStateValue,
            int cachedHasMapEventValue,
            int threadIdValue,
            long utcTicksValue)
        {
            Sequence = sequenceValue;
            Operation = operationValue;
            MapEventId = mapEventIdValue;
            PartyId = partyIdValue;
            ComponentType = componentTypeValue;
            State = stateValue;
            BattleState = battleStateValue;
            TrackerState = trackerStateValue;
            CachedHasMapEvent = cachedHasMapEventValue;
            ThreadId = threadIdValue;
            UtcTicks = utcTicksValue;
        }

        internal long Sequence { get; }
        private string Operation { get; }
        private string MapEventId { get; }
        private string PartyId { get; }
        private string ComponentType { get; }
        private int State { get; }
        private int BattleState { get; }
        private int TrackerState { get; }
        private int CachedHasMapEvent { get; }
        private int ThreadId { get; }
        private long UtcTicks { get; }

        internal string Format()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "seq={0} utc={1:HH:mm:ss.fff} thread={2} operation={3} mapEvent={4} party={5} component={6} state={7} battleState={8} tracker={9} cachedHasMapEvent={10}",
                Sequence,
                new DateTime(UtcTicks, DateTimeKind.Utc),
                ThreadId,
                Operation ?? "null",
                MapEventId ?? "null",
                PartyId ?? "null",
                ComponentType ?? "null",
                State,
                BattleState,
                TrackerState,
                CachedHasMapEvent);
        }
    }
}
