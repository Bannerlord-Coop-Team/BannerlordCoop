using Common.Messaging;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct AgentHitSound : IEvent
{
    public Agent Victim { get; }
    public Agent SoundOwner { get; }
    public Agent AlreadyPlayedBy { get; }
    public int SoundIndex { get; }
    public Vec3 Position { get; }
    public float ArmorType { get; }

    public AgentHitSound(Agent victim, Agent soundOwner, Agent alreadyPlayedBy,
        int soundIndex, Vec3 position, float armorType)
    {
        Victim = victim;
        SoundOwner = soundOwner;
        AlreadyPlayedBy = alreadyPlayedBy;
        SoundIndex = soundIndex;
        Position = position;
        ArmorType = armorType;
    }
}
