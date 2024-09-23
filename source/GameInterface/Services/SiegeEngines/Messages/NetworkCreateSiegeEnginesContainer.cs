using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;
internal class NetworkCreateSiegeEnginesContainer : ICommand
{
    public string Id { get; }

    public NetworkCreateSiegeEnginesContainer(string id)
    {
        Id = id;
    }
}
