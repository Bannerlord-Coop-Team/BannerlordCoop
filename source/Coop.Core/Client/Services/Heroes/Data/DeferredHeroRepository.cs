using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Client.Services.Heroes.Data;

public interface IDeferredHeroRepository
{
    void AddDeferredHero(NetworkNewPlayerHeroCreated message);

    IEnumerable<NetworkNewPlayerHeroCreated> GetAllDeferredHeroes();

    void Clear();
}

internal class DeferredHeroRepository : IDeferredHeroRepository
{
    private readonly List<NetworkNewPlayerHeroCreated> repository = new List<NetworkNewPlayerHeroCreated>();

    public void AddDeferredHero(NetworkNewPlayerHeroCreated message)
    {
        repository.Add(message);
    }

    public void Clear() => repository.Clear();

    public IEnumerable<NetworkNewPlayerHeroCreated> GetAllDeferredHeroes() => repository.AsEnumerable();
}
