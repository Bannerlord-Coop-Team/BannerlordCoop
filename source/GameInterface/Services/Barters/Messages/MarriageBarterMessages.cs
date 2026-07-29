using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Barters.Messages;

internal enum MarriageBarterTermType
{
    Gold,
    Item,
    Fief,
    Prisoner,
}

internal enum MarriageConversationContext
{
    MapParty,
    Location,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAuthorizeMarriageBarter : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly string CounterpartyHeroId;
    [ProtoMember(3)]
    public readonly int Context;
    [ProtoMember(4)]
    public readonly string ContextId;
    [ProtoMember(5)]
    public readonly string HeroBeingProposedToId;
    [ProtoMember(6)]
    public readonly string ProposingHeroId;

    public NetworkAuthorizeMarriageBarter(
        string requestId,
        string counterpartyHeroId,
        MarriageConversationContext context,
        string contextId,
        string heroBeingProposedToId,
        string proposingHeroId)
    {
        RequestId = requestId;
        CounterpartyHeroId = counterpartyHeroId;
        Context = (int)context;
        ContextId = contextId;
        HeroBeingProposedToId = heroBeingProposedToId;
        ProposingHeroId = proposingHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCancelMarriageBarterAuthorization : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    public NetworkCancelMarriageBarterAuthorization(string requestId)
    {
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct MarriageBarterTerm
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

    public MarriageBarterTerm(
        MarriageBarterTermType type,
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
internal readonly struct NetworkRequestMarriageBarter : ICommand
{
    [ProtoMember(1)]
    public readonly string CounterpartyHeroId;
    [ProtoMember(2)]
    public readonly int Context;
    [ProtoMember(3)]
    public readonly string ContextId;
    [ProtoMember(4)]
    public readonly string HeroBeingProposedToId;
    [ProtoMember(5)]
    public readonly string ProposingHeroId;
    [ProtoMember(6)]
    public readonly MarriageBarterTerm[] Terms;
    [ProtoMember(7)]
    public readonly string RequestId;

    public NetworkRequestMarriageBarter(
        string counterpartyHeroId,
        MarriageConversationContext context,
        string contextId,
        string heroBeingProposedToId,
        string proposingHeroId,
        MarriageBarterTerm[] terms,
        string requestId = null)
    {
        CounterpartyHeroId = counterpartyHeroId;
        Context = (int)context;
        ContextId = contextId;
        HeroBeingProposedToId = heroBeingProposedToId;
        ProposingHeroId = proposingHeroId;
        Terms = terms ?? Array.Empty<MarriageBarterTerm>();
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageBarterResult : ICommand
{
    [ProtoMember(1)]
    public readonly string CounterpartyHeroId;
    [ProtoMember(2)]
    public readonly string HeroBeingProposedToId;
    [ProtoMember(3)]
    public readonly string ProposingHeroId;
    [ProtoMember(4)]
    public readonly bool Accepted;
    [ProtoMember(5)]
    public readonly int PlayerGold;
    [ProtoMember(6)]
    public readonly string Reason;
    [ProtoMember(7)]
    public readonly string RequestId;

    public NetworkMarriageBarterResult(
        string counterpartyHeroId,
        string heroBeingProposedToId,
        string proposingHeroId,
        bool accepted,
        int playerGold,
        string reason = null,
        string requestId = null)
    {
        CounterpartyHeroId = counterpartyHeroId;
        HeroBeingProposedToId = heroBeingProposedToId;
        ProposingHeroId = proposingHeroId;
        Accepted = accepted;
        PlayerGold = playerGold;
        Reason = reason;
        RequestId = requestId;
    }
}
