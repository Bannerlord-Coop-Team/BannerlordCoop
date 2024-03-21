using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Heroes.Messages;
public record HeroAddSpecialItem : ICommand
{
    public string HeroId { get; }
    public string ItemObject { get; }

    public HeroAddSpecialItem(string heroId, string itemObject)
    {
        HeroId = heroId;
        ItemObject = itemObject;
    }
}
