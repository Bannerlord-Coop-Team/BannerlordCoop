using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanProductLordActionRequested : IEvent
{
    public readonly Hero Giver;
    public readonly ArtisanProductLordAction Action;

    public ArtisanProductLordActionRequested(Hero giver, ArtisanProductLordAction action)
    {
        Giver = giver;
        Action = action;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestArtisanProductLordAction : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly ArtisanProductLordAction Action;

    public RequestArtisanProductLordAction(string giverId, int generation, ArtisanProductLordAction action)
    {
        GiverId = giverId;
        Generation = generation;
        Action = action;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductLordStarted : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly float Difficulty;
    [ProtoMember(5)] public readonly CampaignTime StartedAt;

    public NetworkArtisanProductLordStarted(string giverId, int generation, string controllerId,
        float difficulty, CampaignTime startedAt)
    {
        GiverId = giverId;
        Generation = generation;
        ControllerId = controllerId;
        Difficulty = difficulty;
        StartedAt = startedAt;
    }
}
