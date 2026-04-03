using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server approving and commanding a party encounter to start on the client
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkStartPartyEncounter : ICommand
{
    [ProtoMember(1)]
    public string AttackerPartyId;
    [ProtoMember(2)]
    public string DefenderPartyId;

    public NetworkStartPartyEncounter(NetworkRequestStartPartyEncounter payload)
    {
        AttackerPartyId = payload.AttackerPartyId;
        DefenderPartyId = payload.DefenderPartyId;
    }
}
