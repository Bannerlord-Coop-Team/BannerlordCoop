using LiteNetLib;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Client.Services.Heroes.Data;

public interface IDeferredHeroRepository
{
    void AddDeferredHero(NetPeer peer, string controllerId, byte[] heroData);

    IEnumerable<TransferredHeroData> GetAllDeferredHeroes();

    void Clear();
}

internal class DeferredHeroRepository : IDeferredHeroRepository
{
    private readonly List<TransferredHeroData> repository = new List<TransferredHeroData>();

    public void AddDeferredHero(NetPeer peer, string controllerId, byte[] heroData)
    {
        var hero = new TransferredHeroData(peer, controllerId, heroData);
        repository.Add(hero);
    }

    public void Clear() => repository.Clear();

    public IEnumerable<TransferredHeroData> GetAllDeferredHeroes() => repository.AsEnumerable();
}
