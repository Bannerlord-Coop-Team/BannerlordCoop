using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCreateParty : ICommand
{
    [ProtoMember(1)]
    public PartyCreationData Data { get; }

    public NetworkCreateParty(PartyCreationData data)
    {
        Data = data;
    }
}
