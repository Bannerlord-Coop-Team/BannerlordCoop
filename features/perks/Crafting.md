# Crafting perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## IronYield

Choice leaf: `perk.IronYield.choice`. Required skill: 25; alternative: `CharcoalYield`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2115`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.IronYield.primary | You can produce crude iron more efficiently by obtaining three units of crude iron from one unit of iron ore. | Personal | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetRefiningFormulas](../source-paths/DefaultSmithingModel.md) | 103 | 118 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharcoalYield

Choice leaf: `perk.CharcoalYield.choice`. Required skill: 25; alternative: `IronYield`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2116`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharcoalYield.primary | You can use a more efficient method of charcoal production that produces three units of charcoal from two units of hardwood. | Personal | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetRefiningFormulas](../source-paths/DefaultSmithingModel.md) | 95 | 118 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## SteelMaker

Choice leaf: `perk.SteelMaker.choice`. Required skill: 50; alternative: `CuriousSmelter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2117`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.SteelMaker.primary | You can refine two units of iron into one unit of steel, and one unit of crude iron as by-product. | Personal | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetRefiningFormulas](../source-paths/DefaultSmithingModel.md) | 106 | 118 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CuriousSmelter

Choice leaf: `perk.CuriousSmelter.choice`. Required skill: 50; alternative: `SteelMaker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2118`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CuriousSmelter.primary | {VALUE}% learning rate of new part designs when smelting. | Personal | 1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetPartResearchGainForSmeltingItem](../source-paths/DefaultSmithingModel.md) | 402 | 407 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetPartResearchGainForSmeltingItem](../source-paths/DefaultSmithingModel.md) | 404 | 407 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## SteelMaker2

Choice leaf: `perk.SteelMaker2.choice`. Required skill: 75; alternative: `CuriousSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2119`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.SteelMaker2.primary | You can refine two units of steel into one unit of fine steel, and one unit of crude iron as by-product. | Personal | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetRefiningFormulas](../source-paths/DefaultSmithingModel.md) | 110 | 118 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CuriousSmith

Choice leaf: `perk.CuriousSmith.choice`. Required skill: 75; alternative: `SteelMaker2`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2120`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CuriousSmith.primary | {VALUE}% learning rate of new part designs when smithing. | Personal | 1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetPartResearchGainForSmithingItem](../source-paths/DefaultSmithingModel.md) | 412 | 421 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetPartResearchGainForSmithingItem](../source-paths/DefaultSmithingModel.md) | 414 | 421 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ExperiencedSmith

Choice leaf: `perk.ExperiencedSmith.choice`. Required skill: 100; alternative: `SteelMaker3`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2121`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ExperiencedSmith.primary | {VALUE}% greater chance of creating Fine weapons. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ExperiencedSmith.secondary | Successful crafting orders of notables increase your relation by {VALUE} with them. | Personal | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 231 | 264 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 236 | 264 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## SteelMaker3

Choice leaf: `perk.SteelMaker3.choice`. Required skill: 100; alternative: `ExperiencedSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2122`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.SteelMaker3.primary | You can refine two units of fine steel into one unit of Thamaskene steel,{newline}and one unit of crude iron as by-product. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.SteelMaker3.secondary | {VALUE} relationships with lords and ladies for successful crafting orders. | Personal | 4f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetRefiningFormulas](../source-paths/DefaultSmithingModel.md) | 114 | 118 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PracticalRefiner

Choice leaf: `perk.PracticalRefiner.choice`. Required skill: 125; alternative: `PracticalSmelter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2123`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PracticalRefiner.primary | {VALUE}% stamina spent while refining. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForRefining](../source-paths/DefaultSmithingModel.md) | 143 | 148 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForRefining](../source-paths/DefaultSmithingModel.md) | 145 | 148 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PracticalSmelter

Choice leaf: `perk.PracticalSmelter.choice`. Required skill: 125; alternative: `PracticalRefiner`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2124`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PracticalSmelter.primary | {VALUE}% stamina spent while smelting. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForSmelting](../source-paths/DefaultSmithingModel.md) | 163 | 168 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForSmelting](../source-paths/DefaultSmithingModel.md) | 165 | 168 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## VigorousSmith

Choice leaf: `perk.VigorousSmith.choice`. Required skill: 150; alternative: `StrongSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2125`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.VigorousSmith.primary | {VALUE} Vigor attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StrongSmith

Choice leaf: `perk.StrongSmith.choice`. Required skill: 150; alternative: `VigorousSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2126`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StrongSmith.primary | {VALUE} Control attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PracticalSmith

Choice leaf: `perk.PracticalSmith.choice`. Required skill: 175; alternative: `ArtisanSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2127`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PracticalSmith.primary | {VALUE}% stamina spent while smithing. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForSmithing](../source-paths/DefaultSmithingModel.md) | 153 | 158 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetEnergyCostForSmithing](../source-paths/DefaultSmithingModel.md) | 155 | 158 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ArtisanSmith

Choice leaf: `perk.ArtisanSmith.choice`. Required skill: 175; alternative: `PracticalSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2128`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ArtisanSmith.primary | {VALUE}% trade penalty when selling smithing weapons. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 42 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 44 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MasterSmith

Choice leaf: `perk.MasterSmith.choice`. Required skill: 200; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2129`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MasterSmith.primary | {VALUE}% greater chance of creating masterwork weapons. | Personal | 0.075f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 238 | 264 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 247 | 264 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## WeaponMasterSmith

Choice leaf: `perk.WeaponMasterSmith.choice`. Required skill: 225; alternative: `EnduringSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2130`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.WeaponMasterSmith.primary | {VALUE} Focus Point to One Handed and Two Handed. | Personal | 1f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EnduringSmith

Choice leaf: `perk.EnduringSmith.choice`. Required skill: 225; alternative: `WeaponMasterSmith`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2131`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EnduringSmith.primary | {VALUE} Endurance attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CraftingSharpenedEdge

Choice leaf: `perk.CraftingSharpenedEdge.choice`. Required skill: 250; alternative: `CraftingSharpenedTip`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2132`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CraftingSharpenedEdge.primary | {VALUE}% swing damage of crafted weapons. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CraftingSharpenedTip

Choice leaf: `perk.CraftingSharpenedTip.choice`. Required skill: 250; alternative: `CraftingSharpenedEdge`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2133`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CraftingSharpenedTip.primary | {VALUE}% thrust damage of crafted weapons. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LegendarySmith

Choice leaf: `perk.LegendarySmith.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2134`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LegendarySmith.primary | {VALUE}% greater chance of creating Legendary weapons, chance increases by 1% for every 5 skill points above 275. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 249 | 264 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.GetModifierQualityProbabilities](../source-paths/DefaultSmithingModel.md) | 260 | 264 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
