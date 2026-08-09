using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public readonly struct QuestTypeQuestSolutionAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly byte[] FieldsBytes;

    public QuestTypeQuestSolutionAcceptTriggered(Hero owner, string controllerId, byte[] fieldsBytes)
    {
        Owner = owner;
        ControllerId = controllerId;
        FieldsBytes = fieldsBytes;
    }
}

public readonly struct QuestTypeAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly byte[] FieldsBytes;

    public QuestTypeAlternativeAcceptTriggered(Hero owner, string controllerId, byte[] fieldsBytes)
    {
        Owner = owner;
        ControllerId = controllerId;
        FieldsBytes = fieldsBytes;
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
    [ProtoMember(4)]
    public readonly byte[] FieldsBytes;

    public RequestQuestTypeAcceptAlternative(string ownerId, int generation, TroopRosterData sentTroops, byte[] fieldsBytes)
    {
        OwnerId = ownerId;
        Generation = generation;
        SentTroops = sentTroops;
        FieldsBytes = fieldsBytes;
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
    public readonly byte[] FieldsBytes;
    [ProtoMember(4)]
    public readonly TroopRosterData SentTroops;

    public NetworkQuestTypeAlternativeAccepted(string ownerId, string ownerControllerId, byte[] fieldsBytes, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        FieldsBytes = fieldsBytes;
        SentTroops = sentTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestTypeAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkQuestTypeAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
