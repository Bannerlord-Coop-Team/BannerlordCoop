using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Registry.Messages;
public record ClearAllRegistries : ICommand
{
}

public record AllRegistriesCleared : IEvent
{
}
