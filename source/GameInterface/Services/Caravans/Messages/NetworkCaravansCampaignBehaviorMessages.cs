using Common.Messaging;
using GameInterface.Services.Caravans.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Caravans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravansKingdomDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string DestroyedKingdomId;

    public NetworkCaravansKingdomDestroyed(string destroyedKingdomId)
    {
        DestroyedKingdomId = destroyedKingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravanPartyDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkCaravanPartyDestroyed(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
