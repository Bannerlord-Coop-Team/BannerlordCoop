using Common.Messaging;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Messages;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal enum GangLeaderWeaponsAction
{
    EnterTown,
    LeaveTown,
    SurrenderWeapons,
    Bribe,
    Intimidate,
    BeginPersuasion,
    PersuasionSucceeded,
    BeginBattle,
    BattleWon,
    BattleLost,
    DeliverWeapons,
    RefreshProgress,
}

internal readonly struct GangLeaderWeaponsIssueCreated : IEvent
{
    public readonly Issue Issue;

    public GangLeaderWeaponsIssueCreated(Issue issue) => Issue = issue;
}

internal readonly struct GangLeaderWeaponsActionRequested : IEvent
{
    public readonly Quest Quest;
    public readonly GangLeaderWeaponsAction Action;

    public GangLeaderWeaponsActionRequested(Quest quest, GangLeaderWeaponsAction action)
    {
        Quest = quest;
        Action = action;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGangLeaderWeaponsIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly GangLeaderWeaponsCreationFields Fields;

    public NetworkGangLeaderWeaponsIssueCreated(string giverId, int generation, GangLeaderWeaponsCreationFields fields)
    {
        GiverId = giverId;
        Generation = generation;
        Fields = fields;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestGangLeaderWeaponsAction : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly GangLeaderWeaponsAction Action;

    public RequestGangLeaderWeaponsAction(string giverId, int generation, string questId, GangLeaderWeaponsAction action)
    {
        GiverId = giverId;
        Generation = generation;
        QuestId = questId;
        Action = action;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGangLeaderWeaponsState : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly string GuardsPartyId;
    [ProtoMember(5)] public readonly int CollectedAmount;
    [ProtoMember(6)] public readonly bool DodgedGuards;
    [ProtoMember(7)] public readonly bool LowCrime;
    [ProtoMember(8)] public readonly bool HighCrime;
    [ProtoMember(9)] public readonly bool PersuasionTried;
    [ProtoMember(10)] public readonly ItemRosterElement[] ConfiscatedWeapons;
    [ProtoMember(11)] public readonly GangLeaderWeaponsAction Action;

    public NetworkGangLeaderWeaponsState(string giverId, int generation, string questId, string guardsPartyId,
        int collectedAmount, bool dodgedGuards, bool lowCrime, bool highCrime, bool persuasionTried,
        ItemRosterElement[] confiscatedWeapons, GangLeaderWeaponsAction action)
    {
        GiverId = giverId;
        Generation = generation;
        QuestId = questId;
        GuardsPartyId = guardsPartyId;
        CollectedAmount = collectedAmount;
        DodgedGuards = dodgedGuards;
        LowCrime = lowCrime;
        HighCrime = highCrime;
        PersuasionTried = persuasionTried;
        ConfiscatedWeapons = confiscatedWeapons;
        Action = action;
    }
}
