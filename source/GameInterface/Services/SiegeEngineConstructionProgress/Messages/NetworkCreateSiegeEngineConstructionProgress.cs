using Common.Messaging;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Messages;

internal class NetworkCreateSiegeEngineConstructionProgress : ICommand
{
    public string Id { get; }

    public NetworkCreateSiegeEngineConstructionProgress(string id)
    {
        Id = id;
    }
}