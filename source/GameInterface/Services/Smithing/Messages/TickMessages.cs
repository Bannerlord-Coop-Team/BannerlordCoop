using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct HourTicked : IEvent { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHourlyTick : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, int> HeroIdCraftingRecords;

    public NetworkHourlyTick(Dictionary<string, int> heroIdCraftingRecords)
    {
        HeroIdCraftingRecords = heroIdCraftingRecords;
    }
}