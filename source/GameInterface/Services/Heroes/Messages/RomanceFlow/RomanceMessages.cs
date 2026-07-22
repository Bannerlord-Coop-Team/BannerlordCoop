using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Messages.RomanceFlow;

internal readonly struct RomanticStateChangeRequested : IEvent
{
    public readonly Hero Person1;
    public readonly Hero Person2;
    public readonly Romance.RomanceLevelEnum RequestedLevel;
    public readonly int ProgressToNextLevel;
    public readonly float LastVisit;
    public readonly float ScoreFromPersuasion;

    public RomanticStateChangeRequested(
        Hero person1,
        Hero person2,
        Romance.RomanceLevelEnum requestedLevel,
        int progressToNextLevel,
        float lastVisit,
        float scoreFromPersuasion)
    {
        Person1 = person1;
        Person2 = person2;
        RequestedLevel = requestedLevel;
        ProgressToNextLevel = progressToNextLevel;
        LastVisit = lastVisit;
        ScoreFromPersuasion = scoreFromPersuasion;
    }
}

internal readonly struct RomanceStatesChanged : IEvent
{
}

internal readonly struct MarriageActionRequested : IEvent
{
    public readonly Hero FirstHero;
    public readonly Hero SecondHero;

    public MarriageActionRequested(Hero firstHero, Hero secondHero)
    {
        FirstHero = firstHero;
        SecondHero = secondHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRomanceStateChange : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;
    [ProtoMember(2)]
    public readonly int RequestedLevel;
    [ProtoMember(3)]
    public readonly int ProgressToNextLevel;
    [ProtoMember(4)]
    public readonly float LastVisit;
    [ProtoMember(5)]
    public readonly float ScoreFromPersuasion;

    public NetworkRequestRomanceStateChange(
        string targetHeroId,
        Romance.RomanceLevelEnum requestedLevel,
        int progressToNextLevel,
        float lastVisit,
        float scoreFromPersuasion)
    {
        TargetHeroId = targetHeroId;
        RequestedLevel = (int)requestedLevel;
        ProgressToNextLevel = progressToNextLevel;
        LastVisit = lastVisit;
        ScoreFromPersuasion = scoreFromPersuasion;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRomanceStateSync : ICommand
{
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSyncRomanceStates : ICommand
{
    [ProtoMember(1)]
    public readonly RomanceStateData[] States;

    public NetworkSyncRomanceStates(RomanceStateData[] states)
    {
        States = states;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RomanceStateData
{
    [ProtoMember(1)]
    public readonly string Person1Id;
    [ProtoMember(2)]
    public readonly string Person2Id;
    [ProtoMember(3)]
    public readonly int Level;
    [ProtoMember(4)]
    public readonly int ProgressToNextLevel;
    [ProtoMember(5)]
    public readonly float LastVisit;
    [ProtoMember(6)]
    public readonly float ScoreFromPersuasion;

    public RomanceStateData(
        string person1Id,
        string person2Id,
        Romance.RomanceLevelEnum level,
        int progressToNextLevel,
        float lastVisit,
        float scoreFromPersuasion)
    {
        Person1Id = person1Id;
        Person2Id = person2Id;
        Level = (int)level;
        ProgressToNextLevel = progressToNextLevel;
        LastVisit = lastVisit;
        ScoreFromPersuasion = scoreFromPersuasion;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRomanceMarriage : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;

    public NetworkRequestRomanceMarriage(string targetHeroId)
    {
        TargetHeroId = targetHeroId;
    }
}

internal enum RomanceBarterTermType
{
    Gold,
    Item,
    Fief,
    Prisoner,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RomanceBarterTerm
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

    public RomanceBarterTerm(
        RomanceBarterTermType type,
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
internal readonly struct NetworkRequestRomanceMarriageBarter : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;
    [ProtoMember(2)]
    public readonly RomanceBarterTerm[] Terms;

    public NetworkRequestRomanceMarriageBarter(string targetHeroId, RomanceBarterTerm[] terms)
    {
        TargetHeroId = targetHeroId;
        Terms = terms;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRomanceMarriageBarterAccepted : ICommand
{
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRomanceRequestRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string Reason;

    public NetworkRomanceRequestRejected(string reason)
    {
        Reason = reason;
    }
}
