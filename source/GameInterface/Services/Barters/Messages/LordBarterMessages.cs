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
    [ProtoMember(6)] public readonly string TargetKingdomId;

    public NetworkAuthorizeLordBarter(
        string requestId,
        string targetHeroId,
        PeaceConversationContext context,
        string contextId,
        LordBarterKind kind,
        string targetKingdomId = null)
    {
        RequestId = requestId;
        TargetHeroId = targetHeroId;
        ContextId = contextId;
        Context = (int)context;
        Kind = (int)kind;
        TargetKingdomId = targetKingdomId;
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
    [ProtoMember(7)] public readonly DefectionPersuasionOutcome[] PersuasionOutcomes;

    public NetworkRequestLordBarter(
        string targetHeroId,
        PeaceConversationContext context,
        string contextId,
        LordBarterKind kind,
        PeaceBarterTerm[] terms,
        string requestId,
        DefectionPersuasionOutcome[] persuasionOutcomes = null)
    {
        TargetHeroId = targetHeroId;
        ContextId = contextId;
        Terms = terms ?? Array.Empty<PeaceBarterTerm>();
        Context = (int)context;
        Kind = (int)kind;
        RequestId = requestId;
        PersuasionOutcomes = persuasionOutcomes ?? Array.Empty<DefectionPersuasionOutcome>();
    }
}

/// <summary>
/// One successful persuasion attempt behind a lord defection.
/// </summary>
/// <remarks>
/// Only the outcome enums travel. The server derives the XP itself: the skill
/// (<c>DefaultSkills.Charm</c>) and difficulty (<c>PersuasionDifficulty.Medium</c>) are hardcoded in
/// vanilla's defection_successful_on_consequence, so no number a client sends is ever trusted.
/// The mini-game cannot be moved server-side - its rolls run inside the client's ConversationManager
/// and vanilla's option table reads Hero.MainHero / Hero.OneToOneConversationHero - so the outcomes
/// themselves are the irreducible trust surface.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
internal readonly struct DefectionPersuasionOutcome
{
    [ProtoMember(1)] public readonly int Result;           // PersuasionOptionResult
    [ProtoMember(2)] public readonly int ArgumentStrength; // PersuasionArgumentStrength

    public DefectionPersuasionOutcome(int result, int argumentStrength)
    {
        Result = result;
        ArgumentStrength = argumentStrength;
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
