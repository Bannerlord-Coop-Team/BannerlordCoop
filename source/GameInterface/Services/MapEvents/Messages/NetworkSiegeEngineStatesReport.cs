using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Mission host → server: the surviving siege engine states at the end of a walls assault. The server
/// re-runs the vanilla write-back with them, patches live, so the HP writes and broken-engine removals
/// replicate; every client's own local write-back is suppressed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeEngineStatesReport : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public SiegeEngineState[] AttackerEngines { get; }
    [ProtoMember(3)]
    public SiegeEngineState[] DefenderEngines { get; }
    /// <summary>
    /// BR-102: the reporting host's epoch for this battle — the server refuses a report stamped by a stale
    /// hosting generation, so a former host's in-flight report cannot clobber the live siege state.
    /// </summary>
    [ProtoMember(4)]
    public int HostEpoch { get; }

    public NetworkSiegeEngineStatesReport(string mapEventId, SiegeEngineState[] attackerEngines, SiegeEngineState[] defenderEngines, int hostEpoch = 0)
    {
        MapEventId = mapEventId;
        AttackerEngines = attackerEngines;
        DefenderEngines = defenderEngines;
        HostEpoch = hostEpoch;
    }
}
