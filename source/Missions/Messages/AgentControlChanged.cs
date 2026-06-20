using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Host-decided transfer of a party's current authority, broadcast so every node applies the same change to
/// its <see cref="IMissionPartyRegistry"/>. Sent when the host assumes control of a disconnected client's
/// party (<see cref="NewAuthority"/> = the host) and when the original owner rejoins and reclaims it
/// (<see cref="NewAuthority"/> = that owner). The party's OriginalOwner never changes.
/// </summary>
[ProtoContract]
public readonly struct AgentControlChanged : IEvent
{
    [ProtoMember(1)]
    public readonly Guid PartyId;

    [ProtoMember(2)]
    public readonly string NewAuthority;

    public AgentControlChanged(Guid partyId, string newAuthority)
    {
        PartyId = partyId;
        NewAuthority = newAuthority;
    }
}
