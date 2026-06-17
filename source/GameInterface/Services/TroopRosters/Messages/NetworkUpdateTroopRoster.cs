using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sent from the authority to every client carrying a whole-roster snapshot. The client rebuilds the
/// target roster from this payload, so a partial or out-of-order update can never leave the client
/// roster indexing past the end of an under-populated array.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkUpdateTroopRoster : ICommand
{
    [ProtoMember(1)]
    public string RosterId { get; }

    [ProtoMember(2)]
    public TroopRosterData Data { get; }

    public NetworkUpdateTroopRoster(string rosterId, TroopRosterData data)
    {
        RosterId = rosterId;
        Data = data;
    }
}
