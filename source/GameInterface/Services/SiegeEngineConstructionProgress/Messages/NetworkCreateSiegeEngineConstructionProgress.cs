using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
internal class NetworkCreateSiegeEngineConstructionProgress : ICommand
{
    public string Id { get; }

    public NetworkCreateSiegeEngineConstructionProgress(string id)
    {
        Id = id;
    }
}
