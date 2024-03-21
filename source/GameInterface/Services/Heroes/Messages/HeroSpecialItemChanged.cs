using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Messages;
public record HeroSpecialItemChanged : ICommand
{
    public string HeroId { get; }
    public string ItemObject { get; }

    public HeroSpecialItemChanged(string heroId, string specialItem)
    {
        HeroId = heroId;
        ItemObject = specialItem;
    }
}
