using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Barters.Messages;

internal enum PeaceBarterTermType
{
    Gold,
    Item,
    Fief,
    TransferPrisoner,
    ReleasePrisoner,
}

internal enum PeaceConversationContext
{
    MapParty,
    Location,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct PeaceBarterTerm
{
    [ProtoMember(1)]
    public readonly int Type;
    [ProtoMember(2)]
    public readonly string OwnerHeroId;
    [ProtoMember(3)]
    public readonly string ObjectId;
    [ProtoMember(4)]
    public readonly string ItemModifierId;
    [ProtoMember(5)]
    public readonly bool ItemModifierNull;
    [ProtoMember(6)]
    public readonly int Amount;

    public PeaceBarterTerm(
        PeaceBarterTermType type,
        string ownerHeroId,
        string objectId,
        string itemModifierId,
        bool itemModifierNull,
        int amount)
    {
        Type = (int)type;
        OwnerHeroId = ownerHeroId;
        ObjectId = objectId;
        ItemModifierId = itemModifierId;
        ItemModifierNull = itemModifierNull;
        Amount = amount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPeaceBarter : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;
    [ProtoMember(2)]
    public readonly string ContextId;
    [ProtoMember(3)]
    public readonly PeaceBarterTerm[] Terms;
    [ProtoMember(4)]
    public readonly int Context;
    [ProtoMember(5)]
    public readonly string RequestId;

    public NetworkRequestPeaceBarter(
        string targetHeroId,
        PeaceConversationContext context,
        string contextId,
        PeaceBarterTerm[] terms,
        string requestId = null)
    {
        TargetHeroId = targetHeroId;
        ContextId = contextId;
        Terms = terms ?? Array.Empty<PeaceBarterTerm>();
        Context = (int)context;
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPeaceBarterResult : ICommand
{
    [ProtoMember(1)]
    public readonly string ContextId;
    [ProtoMember(2)]
    public readonly bool Accepted;
    [ProtoMember(3)]
    public readonly int PlayerGold;
    [ProtoMember(4)]
    public readonly string Reason;
    [ProtoMember(5)]
    public readonly string RequestId;

    public NetworkPeaceBarterResult(
        string contextId,
        bool accepted,
        int playerGold,
        string reason = null,
        string requestId = null)
    {
        ContextId = contextId;
        Accepted = accepted;
        PlayerGold = playerGold;
        Reason = reason;
        RequestId = requestId;
    }
}
