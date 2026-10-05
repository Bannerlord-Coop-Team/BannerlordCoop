# Roguery perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## RogueryNoRestForTheWicked

Choice leaf: `perk.RogueryNoRestForTheWicked.choice`. Required skill: 25; alternative: `RoguerySweetTalker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2177`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryNoRestForTheWicked.primary | {VALUE}% experience gain for bandits in your party. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryNoRestForTheWicked.secondary | {VALUE}% raid speed. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 108 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 110 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 93 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel.CalculateHitDamage](../source-paths/DefaultRaidModel.md) | 52 | 58 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel.CalculateHitDamage](../source-paths/DefaultRaidModel.md) | 54 | 58 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RoguerySweetTalker

Choice leaf: `perk.RoguerySweetTalker.choice`. Required skill: 25; alternative: `RogueryNoRestForTheWicked`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2178`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RoguerySweetTalker.primary | {VALUE}% chance for convincing bandits to leave in peace with barter. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RoguerySweetTalker.secondary | {VALUE}% prisoner escape chance in the governed settlement. | Governor | -0.2f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryTwoFaced

Choice leaf: `perk.RogueryTwoFaced.choice`. Required skill: 50; alternative: `RogueryDeepPockets`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2179`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryTwoFaced.primary | {VALUE}% increased chance for sneaking into towns | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryTwoFaced.secondary | No morale loss from converting bandit prisoners. | PartyLeader | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel.CalculateDisguiseDetectionProbability](../source-paths/DefaultDisguiseDetectionModel.md) | 26 | 31 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel.CalculateDisguiseDetectionProbability](../source-paths/DefaultDisguiseDetectionModel.md) | 28 | 31 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 66 | 75 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryDeepPockets

Choice leaf: `perk.RogueryDeepPockets.choice`. Required skill: 50; alternative: `RogueryTwoFaced`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2180`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryDeepPockets.primary | Double the amount of betting allowed in tournaments. | Personal | 2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryDeepPockets.secondary | {VALUE}% bandit troop wages. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 107 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 111 | 204 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryInBestLight

Choice leaf: `perk.RogueryInBestLight.choice`. Required skill: 75; alternative: `RogueryKnowHow`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2181`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryInBestLight.primary | {VALUE} extra troop from village notables when successfully forced for volunteers. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.RogueryInBestLight.secondary | {VALUE}% faster recovery from raids for your villages. | ClanLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior.village_force_volunteers_ended_successfully_on_consequence](../source-paths/VillageHostileActionCampaignBehavior.md) | 605 | 624 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryKnowHow

Choice leaf: `perk.RogueryKnowHow.choice`. Required skill: 75; alternative: `RogueryInBestLight`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2182`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryKnowHow.primary | {VALUE}% more loot from defeated villagers and caravans. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryKnowHow.secondary | {VALUE} security per day in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 279 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 281 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootItemChancesForWinnerParties](../source-paths/DefaultBattleRewardModel.md) | 308 | 321 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootItemChancesForWinnerParties](../source-paths/DefaultBattleRewardModel.md) | 310 | 321 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryPromises

Choice leaf: `perk.RogueryPromises.choice`. Required skill: 100; alternative: `RogueryManhunter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2183`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryPromises.primary | {VALUE}% food consumption for bandit units in your party. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryPromises.secondary | {VALUE}% recruitment rate for bandit prisoners in your party. | PartyLeader | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 49 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 51 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 52 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 46 | 51 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 48 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryManhunter

Choice leaf: `perk.RogueryManhunter.choice`. Required skill: 100; alternative: `RogueryPromises`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2184`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryManhunter.primary | {VALUE}% better deals with ransom broker for regular troops. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryManhunter.secondary | {VALUE} prisoner limit. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 221 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 223 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRansomValueCalculationModel.PrisonerRansomValue](../source-paths/DefaultRansomValueCalculationModel.md) | 35 | 55 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRansomValueCalculationModel.PrisonerRansomValue](../source-paths/DefaultRansomValueCalculationModel.md) | 37 | 55 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryScarface

Choice leaf: `perk.RogueryScarface.choice`. Required skill: 125; alternative: `RogueryWhiteLies`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2185`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryScarface.primary | {VALUE}% chance for bandits, villagers and caravans to surrender. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryScarface.secondary | {VALUE}% chance per day to increase relation with a notable by 1 in the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel.GetSurrenderChance](../source-paths/DefaultEncounterModel.md) | 329 | 334 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel.GetSurrenderChance](../source-paths/DefaultEncounterModel.md) | 331 | 334 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel.GetBribeChance](../source-paths/DefaultEncounterModel.md) | 377 | 379 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryWhiteLies

Choice leaf: `perk.RogueryWhiteLies.choice`. Required skill: 125; alternative: `RogueryScarface`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2186`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryWhiteLies.primary | {VALUE}% crime rating decrease rate. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryWhiteLies.secondary | {VALUE}% chance to get 1 relation per day with a random notable in the governed settlement. | Governor | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCrimeModel.GetDailyCrimeRatingChange](../source-paths/DefaultCrimeModel.md) | 92 | 94 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RoguerySmugglerConnections

Choice leaf: `perk.RoguerySmugglerConnections.choice`. Required skill: 150; alternative: `RogueryPartnersInCrime`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2187`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RoguerySmugglerConnections.primary | You can trade in towns while in disguise. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.RoguerySmugglerConnections.secondary | {VALUE}% trade penalty when you are trading with a faction you have crime rating against. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 135 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 137 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementAccessModel.CanMainHeroTrade](../source-paths/DefaultSettlementAccessModel.md) | 518 | 529 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementAccessModel.CanMainHeroTrade](../source-paths/DefaultSettlementAccessModel.md) | 522 | 529 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryPartnersInCrime

Choice leaf: `perk.RogueryPartnersInCrime.choice`. Required skill: 150; alternative: `RoguerySmugglerConnections`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2188`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryPartnersInCrime.primary | Surrendering bandit parties can be recruited. | PartyLeader | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryPartnersInCrime.secondary | {VALUE}% damage by bandit troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel.CanPlayerForceBanditsToJoin](../source-paths/DefaultEncounterModel.md) | 497 | 501 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel.CanPlayerForceBanditsToJoin](../source-paths/DefaultEncounterModel.md) | 499 | 501 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 410 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryOneOfTheFamily

Choice leaf: `perk.RogueryOneOfTheFamily.choice`. Required skill: 175; alternative: `RoguerySaltTheEarth`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2189`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryOneOfTheFamily.primary | {VALUE} bonus Vigor and Control skills to bandit units in your party | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.RogueryOneOfTheFamily.secondary | {VALUE} recruitment slot when recruiting from gang leaders. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexCanPartyRecruitFromHeroInternal](../source-paths/DefaultVolunteerModel.md) | 65 | 85 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexCanPartyRecruitFromHeroInternal](../source-paths/DefaultVolunteerModel.md) | 73 | 85 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexCanPartyRecruitFromHeroInternal](../source-paths/DefaultVolunteerModel.md) | 81 | 85 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 280 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 282 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RoguerySaltTheEarth

Choice leaf: `perk.RoguerySaltTheEarth.choice`. Required skill: 175; alternative: `RogueryOneOfTheFamily`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2190`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RoguerySaltTheEarth.primary | {VALUE}% more loot when villagers comply to your hostile actions. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RoguerySaltTheEarth.secondary | {VALUE}% tariff revenue in the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromTariffs](../source-paths/DefaultClanFinanceModel.md) | 440 | 452 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryCarver

Choice leaf: `perk.RogueryCarver.choice`. Required skill: 200; alternative: `RogueryRansomBroker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2191`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryCarver.primary | {VALUE}% damage with civilian weapons. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryCarver.secondary | {VALUE}% one handed damage by troops under your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OneHandedUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 133 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 329 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryRansomBroker

Choice leaf: `perk.RogueryRansomBroker.choice`. Required skill: 200; alternative: `RogueryCarver`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2192`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryRansomBroker.primary | {VALUE}% better deals for heroes from ransom brokers. | PartyLeader | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryRansomBroker.secondary | {VALUE}% escape chance for hero prisoners. | PartyLeader | -0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRansomValueCalculationModel.PrisonerRansomValue](../source-paths/DefaultRansomValueCalculationModel.md) | 40 | 55 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultRansomValueCalculationModel.PrisonerRansomValue](../source-paths/DefaultRansomValueCalculationModel.md) | 42 | 55 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryArmsDealer

Choice leaf: `perk.RogueryArmsDealer.choice`. Required skill: 225; alternative: `RogueryDirtyFighting`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2193`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryArmsDealer.primary | {VALUE}% sell price penalty for weapons. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryArmsDealer.secondary | {VALUE}% militia per day in the besieged governed settlement. | Governor | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 114 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 176 | 180 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryDirtyFighting

Choice leaf: `perk.RogueryDirtyFighting.choice`. Required skill: 225; alternative: `RogueryArmsDealer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2194`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryDirtyFighting.primary | {VALUE}% stun duration for kicking. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryDirtyFighting.secondary | {VALUE} random food item will be smuggled to the besieged governed settlement. | Governor | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel.CalculateTownFoodChangeInternal](../source-paths/DefaultSettlementFoodModel.md) | 80 | 97 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1304 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryDashAndSlash

Choice leaf: `perk.RogueryDashAndSlash.choice`. Required skill: 250; alternative: `RogueryFleetFooted`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2195`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryDashAndSlash.primary | {VALUE}% damage bonus from speed while on foot. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryDashAndSlash.secondary | {VALUE}% two handed weapon damage by troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.TwoHandedUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 163 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryFleetFooted

Choice leaf: `perk.RogueryFleetFooted.choice`. Required skill: 250; alternative: `RogueryDashAndSlash`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2196`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryFleetFooted.primary | {VALUE}% combat movement speed while no weapons or shields are equipped. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.RogueryFleetFooted.secondary | {VALUE}% escape chance when imprisoned by mobile parties. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1507 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RogueryRogueExtraordinaire

Choice leaf: `perk.RogueryRogueExtraordinaire.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2197`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RogueryRogueExtraordinaire.primary | {VALUE}% loot amount for every skill point above 200. | Personal | 0.01f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootItemChancesForWinnerParties](../source-paths/DefaultBattleRewardModel.md) | 303 | 321 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootItemChancesForWinnerParties](../source-paths/DefaultBattleRewardModel.md) | 305 | 321 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
