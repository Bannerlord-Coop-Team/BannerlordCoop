using Common.Messaging;
using Common.PacketHandlers;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Command to destroy a party
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDestroyParty : ICommand
{
    [ProtoMember(1)]
    public PartyDestructionData Data { get; }

    public NetworkDestroyParty(PartyDestructionData data)
    {
        Data = data;
    }
}