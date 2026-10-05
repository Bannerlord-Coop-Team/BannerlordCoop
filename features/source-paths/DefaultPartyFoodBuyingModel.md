# DefaultPartyFoodBuyingModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.member | ai.005 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: void/event path | Locally assigned targets: itemElement, itemElementsPrice, num, settlementComponent, num2, i, elementCopyAtIndex, flag, itemPrice, itemValue, num3, num4, num5 | Calls in this member: settlement.ItemRoster.GetElementCopyAtIndex, settlementComponent.GetItemPrice | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-26 | ai.005 | Source predicate is true: elementCopyAtIndex.Amount &lt;= 0 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-31 | ai.005 | Source predicate is true: !(elementCopyAtIndex.EquipmentElement.Item.IsFood &#124;&#124; flag) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-37 | ai.005 | Source predicate is true: !(itemPrice &lt; 120 &#124;&#124; flag) &#124;&#124; mobileParty.PartyTradeGold &lt; itemPrice | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-44 | ai.005 | Source predicate is true: num5 &gt; 0f | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num2, itemElementsPrice, num | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-46 | ai.005 | Source predicate is true: MBRandom.RandomFloat * (num + num5) &gt;= num | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num2, itemElementsPrice | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
| DefaultPartyFoodBuyingModel.FindItemToBuy.16.guard-54 | ai.005 | Source predicate is true: num2 != -1 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: itemElement | True-branch calls: settlement.ItemRoster.GetElementCopyAtIndex | TaleWorlds.CampaignSystem.GameComponents.DefaultPartyFoodBuyingModel@16-58 |
