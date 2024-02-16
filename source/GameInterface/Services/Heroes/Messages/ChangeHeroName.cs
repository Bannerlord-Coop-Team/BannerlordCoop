using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Heroes.Messages;
public record ChangeHeroName : IEvent
{
    public ChangeHeroName(HeroChangeNameData data)
    {
        Data = data;
    }

    public HeroChangeNameData Data { get; }
}
