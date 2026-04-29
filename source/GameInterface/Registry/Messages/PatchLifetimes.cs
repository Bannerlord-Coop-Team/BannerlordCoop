using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Registry.Messages;

public struct PatchLifetimes : ICommand
{
}

public struct LifetimesPatched : IEvent
{
}
