using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Issues;

// Real DefaultTradeItemPriceFactorModel NREs on this harness's bare test Town/ItemCategory (no real supply/
// demand data). Returns the item's own Value directly - deterministic, and lets tests manufacture controllable
// price divergence across peers. Shared across multiple quest types' E2E tests.
internal class StubTradeItemPriceFactorModel : TradeItemPriceFactorModel
{
    public override float GetTradePenalty(ItemObject item, MobileParty clientParty, PartyBase merchant, bool isSelling, float inStore, float supply, float demand) => 0f;

    public override float GetBasePriceFactor(ItemCategory itemCategory, float inStoreValue, float supply, float demand, bool isSelling, int transferValue) => 1f;

    public override int GetPrice(EquipmentElement itemRosterElement, MobileParty clientParty, PartyBase merchant, bool isSelling, float inStoreValue, float supply, float demand) =>
        itemRosterElement.Item?.Value ?? 0;

    public override int GetTheoreticalMaxItemMarketValue(ItemObject item) => item.Value;
}
