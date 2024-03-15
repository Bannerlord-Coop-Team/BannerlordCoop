using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Message from Client to GI to make change of children
/// </summary>
public record AddNewChildren : ICommand
{
    public string HeroId { get; }
    public string ChildId { get; }

    public AddNewChildren(string heroId, string childId)
    {
        HeroId = heroId;
        ChildId = childId;
    }
}
