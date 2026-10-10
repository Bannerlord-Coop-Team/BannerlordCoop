using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClientSelectHeir : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, int> HeirIdApparents;

    [ProtoMember(2)]
    public readonly bool AppointClanLeader;

    [ProtoMember(3)]
    public readonly bool WaitingForHeirSelection;

    [ProtoMember(4)]
    public readonly bool SelectionRejected;

    public NetworkClientSelectHeir(
        Dictionary<string, int> heirIdApparents,
        bool appointClanLeader = false,
        bool waitingForHeirSelection = false,
        bool selectionRejected = false)
    {
        HeirIdApparents = heirIdApparents;
        AppointClanLeader = appointClanLeader;
        WaitingForHeirSelection = waitingForHeirSelection;
        SelectionRejected = selectionRejected;
    }
}
