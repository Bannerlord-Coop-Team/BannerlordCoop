using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Messages;

public readonly struct QuestTypeQuestSolutionAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public QuestTypeQuestSolutionAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

public readonly struct QuestTypeAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly TroopRoster SelectedTroops;
    public readonly PartyScreenLogic Screen;

    public QuestTypeAlternativeAcceptTriggered(Hero owner, string controllerId)
        : this(owner, controllerId, null)
    {
    }

    public QuestTypeAlternativeAcceptTriggered(Hero owner, string controllerId, TroopRoster selectedTroops)
        : this(owner, controllerId, selectedTroops, null)
    {
    }

    public QuestTypeAlternativeAcceptTriggered(Hero owner, string controllerId, TroopRoster selectedTroops,
        PartyScreenLogic screen)
    {
        Owner = owner;
        ControllerId = controllerId;
        SelectedTroops = selectedTroops;
        Screen = screen;
    }
}

public readonly struct QuestAlternativeTroopsTransferredLocally : IEvent
{
    public readonly TroopRoster Roster;
    public readonly TroopRoster Before;
    public readonly TroopRoster After;

    public QuestAlternativeTroopsTransferredLocally(TroopRoster roster, TroopRoster before, TroopRoster after)
    {
        Roster = roster;
        Before = before;
        After = after;
    }
}

public readonly struct QuestAlternativeTroopSelectionClosed : IEvent
{
    public readonly TroopRoster Roster;

    public QuestAlternativeTroopSelectionClosed(TroopRoster roster) => Roster = roster;
}

public readonly struct QuestAlternativeTroopSelectionReset : IEvent
{
    public readonly TroopRoster Roster;
    public readonly PartyScreenLogic Screen;
    public readonly TroopRoster SelectedTroops;

    public QuestAlternativeTroopSelectionReset(TroopRoster roster, PartyScreenLogic screen, TroopRoster selectedTroops = null)
    {
        Roster = roster;
        Screen = screen;
        SelectedTroops = selectedTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestTypeAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;

    public RequestQuestTypeAcceptQuest(string ownerId, int generation)
    {
        OwnerId = ownerId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestTypeQuestAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly byte[] FieldsBytes;

    public NetworkQuestTypeQuestAccepted(string ownerId, string ownerControllerId, byte[] fieldsBytes)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        FieldsBytes = fieldsBytes;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestTypeAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;
    [ProtoMember(3)]
    public readonly TroopRosterData SentTroops;

    public RequestQuestTypeAcceptAlternative(string ownerId, int generation, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        Generation = generation;
        SentTroops = sentTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestTypeAlternativeAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly AlternativeSolutionVanillaState State;
    [ProtoMember(4)]
    public readonly byte[] FieldsBytes;
    [ProtoMember(5)]
    public readonly TroopRosterData SentTroops;

    public NetworkQuestTypeAlternativeAccepted(string ownerId, string ownerControllerId, AlternativeSolutionVanillaState state, byte[] fieldsBytes, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        State = state;
        FieldsBytes = fieldsBytes;
        SentTroops = sentTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestTypeAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly bool IsAlternative;

    public NetworkQuestTypeAcceptRejected(string ownerId, bool isAlternative)
    {
        OwnerId = ownerId;
        IsAlternative = isAlternative;
    }
}
