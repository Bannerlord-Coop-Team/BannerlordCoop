using System.Linq;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Data;

/// <summary>
/// Pairs one hero's equipment sets sent with a trade against the same sets from before the trade.
/// </summary>
internal class HeroTradeEquipment
{
    public readonly EquipmentElement[][] TradedSets;
    public readonly EquipmentElement[][] PreTradeSets;

    public HeroTradeEquipment(EquipmentElement[][] tradedSets, EquipmentElement[][] preTradeSets)
    {
        TradedSets = tradedSets;
        PreTradeSets = preTradeSets;
    }

    /// <summary>
    /// Empties up to amount of traded slots that gained the element during the trade and returns how many were emptied.
    /// </summary>
    public int TakeGained(EquipmentElement element, int amount)
    {
        int toTake = System.Math.Min(CountGained(element), amount);
        if (toTake <= 0) return 0;

        int removed = 0;
        for (int set = 0; set < TradedSets.Length && removed < toTake; set++)
        {
            var traded = TradedSets[set];
            var preTrade = PreTradeSets[set];

            for (int i = 0; i < traded.Length && removed < toTake; i++)
            {
                if (!traded[i].IsEqualTo(element)) continue;
                if (i < preTrade.Length && preTrade[i].IsEqualTo(element)) continue;

                traded[i] = EquipmentElement.Invalid;
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// How many more slots hold the element after the trade than before it, negative when the trade emptied slots.
    /// </summary>
    public int CountGained(EquipmentElement element) => CountOf(TradedSets, element) - CountOf(PreTradeSets, element);

    // Counts the slots holding the element across every set
    private static int CountOf(EquipmentElement[][] sets, EquipmentElement element) =>
        sets.Sum(slots => slots.Count(slot => slot.IsEqualTo(element)));
}
