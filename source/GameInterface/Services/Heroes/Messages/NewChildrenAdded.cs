using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Notify from GI to Server that a new child has poppedd
/// </summary>
public record NewChildrenAdded : ICommand
{
    public string HeroId { get; }
    public string ChildId { get; }

    public NewChildrenAdded(string heroId, string childId)
    {
        HeroId = heroId;
        ChildId = childId;
    }
}
