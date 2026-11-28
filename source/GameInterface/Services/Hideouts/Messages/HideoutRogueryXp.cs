using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Hideouts.Messages;

internal enum HideoutRogueryXpReward
{
    MissionSucceeded,
    MissionFailed,
    ClearedAsGhost,
}

internal readonly struct HideoutRogueryXpEarned : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly HideoutRogueryXpReward Reward;

    public HideoutRogueryXpEarned(MapEvent mapEvent, HideoutRogueryXpReward reward)
    {
        MapEvent = mapEvent;
        Reward = reward;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutRogueryXpEarned : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly HideoutRogueryXpReward Reward;

    public NetworkHideoutRogueryXpEarned(string mapEventId, HideoutRogueryXpReward reward)
    {
        MapEventId = mapEventId;
        Reward = reward;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutGhostBonusGained : ICommand
{
    [ProtoMember(1)]
    public readonly float Xp;

    [ProtoMember(2)]
    public readonly float BaseXp;

    public NetworkHideoutGhostBonusGained(float xp, float baseXp)
    {
        Xp = xp;
        BaseXp = baseXp;
    }
}
