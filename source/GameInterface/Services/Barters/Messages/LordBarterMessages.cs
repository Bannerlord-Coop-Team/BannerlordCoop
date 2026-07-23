using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Barters.Messages;

internal enum LordBarterKind
{
    Generic,
    SafePassage,
    JoinKingdomAsClan,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAuthorizeLordBarter : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;
    [ProtoMember(2)] public readonly string TargetHeroId;
    [ProtoMember(3)] public readonly string ContextId;
    [ProtoMember(4)] public readonly int Context;
    [ProtoMember(5)] public readonly int Kind;

    public NetworkAuthorizeLordBarter(
        string requestId,
        string targetHeroId,
        PeaceConversationContext context,
        string contextId,
        LordBarterKind kind)
    {
        RequestId = requestId;
        TargetHeroId = targetHeroId;
        ContextId = contextId;
        Context = (int)context;
        Kind = (int)kind;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCancelLordBarterAuthorization : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;

    public NetworkCancelLordBarterAuthorization(string requestId)
    {
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestLordBarter : ICommand
{
    [ProtoMember(1)] public readonly string TargetHeroId;
    [ProtoMember(2)] public readonly string ContextId;
    [ProtoMember(3)] public readonly PeaceBarterTerm[] Terms;
    [ProtoMember(4)] public readonly int Context;
    [ProtoMember(5)] public readonly int Kind;
    [ProtoMember(6)] public readonly string RequestId;

    public NetworkRequestLordBarter(
        string targetHeroId,
        PeaceConversationContext context,
        string contextId,
        LordBarterKind kind,
        PeaceBarterTerm[] terms,
        string requestId)
    {
        TargetHeroId = targetHeroId;
        ContextId = contextId;
        Terms = terms ?? Array.Empty<PeaceBarterTerm>();
        Context = (int)context;
        Kind = (int)kind;
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLordBarterResult : ICommand
{
    [ProtoMember(1)] public readonly string ContextId;
    [ProtoMember(2)] public readonly bool Accepted;
    [ProtoMember(3)] public readonly int PlayerGold;
    [ProtoMember(4)] public readonly string Reason;
    [ProtoMember(5)] public readonly string RequestId;

    public NetworkLordBarterResult(string contextId, bool accepted, int playerGold, string reason, string requestId)
    {
        ContextId = contextId;
        Accepted = accepted;
        PlayerGold = playerGold;
        Reason = reason;
        RequestId = requestId;
    }
}
