using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct PlayerDivorceRequested : IEvent
{
    public readonly Hero Actor;
    public readonly Hero Spouse;

    public PlayerDivorceRequested(Hero actor, Hero spouse)
    {
        Actor = actor;
        Spouse = spouse;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestPlayerDivorce : ICommand
{
    [ProtoMember(1)]
    public readonly string ActorId;

    [ProtoMember(2)]
    public readonly string SpouseId;

    public RequestPlayerDivorce(string actorId, string spouseId)
    {
        ActorId = actorId;
        SpouseId = spouseId;
    }
}
