using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Event notifying that new party created on server has been synced across all clients
/// </summary>
public record NewPartySynced : IEvent
{
}
