using Common.Messaging;
using TaleWorlds.Core;

internal record ItemSlotsCreated(Equipment instance) : IEvent
{
    public Equipment instance { get; } = instance;

}