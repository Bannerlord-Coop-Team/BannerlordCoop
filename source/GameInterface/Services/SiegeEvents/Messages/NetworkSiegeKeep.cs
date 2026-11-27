using Common.Messaging;
using GameInterface.Services.Hideouts;
using ProtoBuf;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>Where the keep (lord's hall) stage of a siege assault stands for one player.</summary>
public enum SiegeKeepPhase
{
    /// <summary>The walls fell and the attacking players are choosing their keep troops.</summary>
    Choosing,
    /// <summary>The keep roster is final and the keep battle is starting.</summary>
    Locked,
    /// <summary>No attacking player is left to lead the keep battle.</summary>
    Closed,
}

/// <summary>
/// [Client -&gt; Server] Asks for this player's keep place, or submits the troops it brings into the keep.
/// A query opens the keep stage when the siege is already in the lord's hall but no stage is open yet.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSiegeKeepEntryRequest : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;
    [ProtoMember(2)] public readonly string MapEventId;
    [ProtoMember(3)] public readonly bool QueryOnly;
    [ProtoMember(4)] public readonly HideoutTroopSelectionEntry[] Troops;

    public NetworkSiegeKeepEntryRequest(string requestId, string mapEventId, bool queryOnly,
        HideoutTroopSelectionEntry[] troops)
    {
        RequestId = requestId;
        MapEventId = mapEventId;
        QueryOnly = queryOnly;
        Troops = troops;
    }
}

/// <summary>[Server -&gt; Client] Answers a <see cref="NetworkSiegeKeepEntryRequest"/>.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSiegeKeepEntryReply : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;
    [ProtoMember(2)] public readonly bool Accepted;
    [ProtoMember(3)] public readonly string Reason;
    /// <summary>How many troops besides the player's hero this player may bring.</summary>
    [ProtoMember(4)] public readonly int EscortAllowance;
    [ProtoMember(5)] public readonly int SecondsRemaining;
    /// <summary>The roster is final, so an admitted player goes straight into the keep battle.</summary>
    [ProtoMember(6)] public readonly bool IsLocked;

    public NetworkSiegeKeepEntryReply(string requestId, bool accepted, string reason, int escortAllowance,
        int secondsRemaining, bool isLocked = false)
    {
        RequestId = requestId;
        Accepted = accepted;
        Reason = reason;
        EscortAllowance = escortAllowance;
        SecondsRemaining = secondsRemaining;
        IsLocked = isLocked;
    }
}

/// <summary>[Server -&gt; Client] Tells one walls player where the keep stage of its siege stands.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSiegeKeepPhaseChanged : ICommand
{
    [ProtoMember(1)] public readonly string MapEventId;
    [ProtoMember(2)] public readonly SiegeKeepPhase Phase;
    /// <summary>Whether this player has a place in the keep battle.</summary>
    [ProtoMember(3)] public readonly bool IsSeated;
    [ProtoMember(4)] public readonly int EscortAllowance;
    [ProtoMember(5)] public readonly int SecondsRemaining;

    public NetworkSiegeKeepPhaseChanged(string mapEventId, SiegeKeepPhase phase, bool isSeated,
        int escortAllowance, int secondsRemaining)
    {
        MapEventId = mapEventId;
        Phase = phase;
        IsSeated = isSeated;
        EscortAllowance = escortAllowance;
        SecondsRemaining = secondsRemaining;
    }
}

/// <summary>
/// [Local, server] Every walls mission member reported the defenders falling back to the keep, so the keep stage
/// should open. Its host epoch comes back on <see cref="SiegeKeepStageOpened"/>.
/// </summary>
public readonly struct AuthoritativeSiegeKeepRequested : IEvent
{
    public readonly string MapEventId;
    public readonly int HostEpoch;

    public AuthoritativeSiegeKeepRequested(string mapEventId, int hostEpoch)
    {
        MapEventId = mapEventId;
        HostEpoch = hostEpoch;
    }
}

/// <summary>
/// [Local, server] Whether the keep stage opened on a map event whose walls mission just concluded. When opened, the
/// walls instance ends without a conclusion record, so the keep mission can reuse its id. When not opened, the walls
/// conclude as an attacker victory at the walls' host epoch.
/// </summary>
public readonly struct SiegeKeepStageOpened : IEvent
{
    public readonly string MapEventId;
    public readonly bool Opened;
    public readonly int HostEpoch;

    public SiegeKeepStageOpened(string mapEventId, bool opened, int hostEpoch)
    {
        MapEventId = mapEventId;
        Opened = opened;
        HostEpoch = hostEpoch;
    }
}
