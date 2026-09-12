using Common.Messaging;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>One server-authoritative siege-engine slot generation.</summary>
[ProtoContract(SkipConstructor = true)]
public record SiegeEngineSlotRevision
{
    [ProtoMember(1)]
    public string ContainerId { get; }
    [ProtoMember(2)]
    public bool IsRanged { get; }
    [ProtoMember(3)]
    public int Index { get; }
    [ProtoMember(4)]
    public long Revision { get; }

    public SiegeEngineSlotRevision(string containerId, bool isRanged, int index, long revision)
    {
        ContainerId = containerId;
        IsRanged = isRanged;
        Index = index;
        Revision = revision;
    }
}

/// <summary>
/// Seeds a newly-entered client with revisions for slots mutated during this server process. Untouched slots
/// are revision zero on both sides and are omitted.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSyncSiegeEngineSlotRevisions : IEvent
{
    [ProtoMember(1)]
    public string RevisionEpoch { get; }
    [ProtoMember(2)]
    public SiegeEngineSlotRevision[] Slots { get; }

    public NetworkSyncSiegeEngineSlotRevisions(string revisionEpoch, SiegeEngineSlotRevision[] slots)
    {
        RevisionEpoch = revisionEpoch;
        Slots = slots ?? Array.Empty<SiegeEngineSlotRevision>();
    }
}
