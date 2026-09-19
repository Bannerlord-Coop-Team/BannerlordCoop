using GameInterface.Services.MapEvents.TroopSupply;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

// Native ambush consumes its supplier before spawning sentries, allies, or the boss. Keep these
// staged origins recoverable after a host change, even though the campaign supplied pointer advanced.
internal sealed class HideoutTroopSupplier : IMissionTroopSupplier
{
    private readonly CoopTroopSupplier source;
    private readonly HideoutStateLedger ledger;
    private readonly List<IAgentOriginBase> pending = new();
    private readonly HashSet<int> staged = new();

    public HideoutTroopSupplier(CoopTroopSupplier source, HideoutStateLedger ledger)
    {
        this.source = source;
        this.ledger = ledger;
    }

    public void Refresh(bool recoverStaged)
    {
        var newlySupplied = source.SupplyTroops(source.NumTroopsNotSupplied).ToArray();
        var origins = recoverStaged ? source.GetAllTroops() : newlySupplied;
        foreach (var origin in origins)
        {
            if (!ledger.WasSpawned(origin.UniqueSeed) && staged.Add(origin.UniqueSeed))
                pending.Add(origin);
        }
        pending.RemoveAll(origin => ledger.WasSpawned(origin.UniqueSeed));
    }

    public void ResetForMigration()
    {
        staged.Clear();
        pending.Clear();
        Refresh(recoverStaged: true);
    }

    public int NumRemovedTroops => source.NumRemovedTroops;
    public int NumTroopsNotSupplied => pending.Count;
    public bool AnyTroopRemainsToBeSupplied => pending.Count > 0;
    public IEnumerable<IAgentOriginBase> GetAllTroops() => source.GetAllTroops();
    public IAgentOriginBase SupplyOneTroop() => SupplyTroops(1).FirstOrDefault();
    public BasicCharacterObject GetGeneralCharacter() => source.GetGeneralCharacter();
    public int GetNumberOfPlayerControllableTroops() => source.GetNumberOfPlayerControllableTroops();
    public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate)
    {
        var result = pending.Take(numberToAllocate).ToArray();
        pending.RemoveRange(0, result.Length);
        return result;
    }

    public IAgentOriginBase TakeHero(BasicCharacterObject hero, bool returningHero = false)
    {
        var index = pending.FindIndex(origin => origin.Troop == hero);
        if (index < 0)
            return returningHero ? source.GetAllTroops().FirstOrDefault(origin => origin.Troop == hero) : null;
        var origin = pending[index];
        pending.RemoveAt(index);
        return origin;
    }
}
