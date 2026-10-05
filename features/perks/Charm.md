# Charm perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## CharmVirile

Choice leaf: `perk.CharmVirile.choice`. Required skill: 25; alternative: `CharmSelfPromoter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2198`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmVirile.primary | {VALUE}% more likely to have children. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmVirile.secondary | {VALUE}% daily chance to get +1 relation with a random notable in the governed settlement while a continuous project is active. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero](../source-paths/DefaultPregnancyModel.md) | 41 | 46 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero](../source-paths/DefaultPregnancyModel.md) | 43 | 46 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmSelfPromoter

Choice leaf: `perk.CharmSelfPromoter.choice`. Required skill: 25; alternative: `CharmVirile`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2199`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmSelfPromoter.primary | {VALUE} renown when a tournament is won. | Personal | 3f | Add | TroopUsageFlags.Undefined |
| perk.CharmSelfPromoter.secondary | {VALUE} morale while defending in a besieged settlement. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 169 | 190 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 171 | 190 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel.GetRenownReward](../source-paths/DefaultTournamentModel.md) | 66 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel.GetRenownReward](../source-paths/DefaultTournamentModel.md) | 68 | 71 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmOratory

Choice leaf: `perk.CharmOratory.choice`. Required skill: 50; alternative: `CharmWarlord`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2200`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmOratory.primary | {VALUE} renown and influence for each issue resolved | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.CharmOratory.secondary | {VALUE} relationship with a random notable of your kingdom when an enemy lord is defeated. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmWarlord

Choice leaf: `perk.CharmWarlord.choice`. Required skill: 50; alternative: `CharmOratory`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2201`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmWarlord.primary | {VALUE}% influence gain from battles. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmWarlord.secondary | {VALUE} relationship with a random lord of your kingdom when an enemy lord is defeated. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateInfluenceGain](../source-paths/DefaultBattleRewardModel.md) | 81 | 85 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmForgivableGrievances

Choice leaf: `perk.CharmForgivableGrievances.choice`. Required skill: 75; alternative: `CharmMeaningfulFavors`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2202`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmForgivableGrievances.primary | {VALUE}% chance of avoiding critical failure on persuasion. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmForgivableGrievances.secondary | {VALUE}% daily chance to increase relations with a random lord or notable with negative relations with you when you are in a settlement. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmMeaningfulFavors

Choice leaf: `perk.CharmMeaningfulFavors.choice`. Required skill: 75; alternative: `CharmForgivableGrievances`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2203`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmMeaningfulFavors.primary | {VALUE}% chance for double persuasion success. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmMeaningfulFavors.secondary | {VALUE}% daily chance to increase relations with powerful notables in the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.GetChances](../source-paths/DefaultPersuasionModel.md) | 60 | 65 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.GetChances](../source-paths/DefaultPersuasionModel.md) | 62 | 65 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmInBloom

Choice leaf: `perk.CharmInBloom.choice`. Required skill: 100; alternative: `CharmYoungAndRespectful`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2204`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmInBloom.primary | {VALUE}% relationship gain with the opposing gender. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmInBloom.secondary | {VALUE}% daily chance to increase relations with a random notable of opposed sex in the governed settlement. | Governor | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 173 | 191 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 175 | 191 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmYoungAndRespectful

Choice leaf: `perk.CharmYoungAndRespectful.choice`. Required skill: 100; alternative: `CharmInBloom`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2205`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmYoungAndRespectful.primary | {VALUE}% relationship gain with the same gender. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmYoungAndRespectful.secondary | {VALUE}% daily chance to increase relations with a random notable of same sex in the governed settlement. | Governor | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 178 | 191 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 180 | 191 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmFirebrand

Choice leaf: `perk.CharmFirebrand.choice`. Required skill: 125; alternative: `CharmFlexibleEthics`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2206`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmFirebrand.primary | {VALUE}% influence cost to initiate kingdom decisions. | ClanLeader | -0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmFirebrand.secondary | {VALUE} recruitment slot from rural notables. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetPerkEffectsOnKingdomDecisionInfluenceCost](../source-paths/DefaultDiplomacyModel.md) | 843 | 847 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetPerkEffectsOnKingdomDecisionInfluenceCost](../source-paths/DefaultDiplomacyModel.md) | 845 | 847 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 34 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 36 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmFlexibleEthics

Choice leaf: `perk.CharmFlexibleEthics.choice`. Required skill: 125; alternative: `CharmFirebrand`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2207`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmFlexibleEthics.primary | {VALUE}% influence cost when voting for kingdom proposals made by others. | Personal | -0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmFlexibleEthics.secondary | {VALUE} recruitment slot from urban notables. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 38 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 40 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmEffortForThePeople

Choice leaf: `perk.CharmEffortForThePeople.choice`. Required skill: 150; alternative: `CharmSlickNegotiator`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2208`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmEffortForThePeople.primary | {VALUE} relation with the nearest settlement owner clan when you clear a hideout. +1 town loyalty if it is your clan. | Personal | 3f | Add | TroopUsageFlags.Undefined |
| perk.CharmEffortForThePeople.secondary | {VALUE}% barter penalty with lords of same culture. | Personal | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 55 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 57 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 77 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 79 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmSlickNegotiator

Choice leaf: `perk.CharmSlickNegotiator.choice`. Required skill: 150; alternative: `CharmEffortForThePeople`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2209`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmSlickNegotiator.primary | {VALUE}% hiring costs of mercenary troops. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmSlickNegotiator.secondary | {VALUE}% barter penalty with lords of different cultures. | Personal | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 279 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 281 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 60 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 62 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 82 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 84 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmGoodNatured

Choice leaf: `perk.CharmGoodNatured.choice`. Required skill: 175; alternative: `CharmTribute`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2210`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmGoodNatured.primary | {VALUE}% influence return when a supported proposal fails to pass. | Personal | 1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmGoodNatured.secondary | {VALUE} extra relationship when you increase relationship with merciful lords. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 182 | 191 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 184 | 191 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmTribute

Choice leaf: `perk.CharmTribute.choice`. Required skill: 175; alternative: `CharmGoodNatured`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2211`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmTribute.primary | {VALUE}% relationship bonus when you pay more than minimum amount in barters. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmTribute.secondary | {VALUE} extra relationship when you increase relationship with cruel lords. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.CalculateOverpayRelationIncreaseCosts](../source-paths/DefaultBarterModel.md) | 38 | 43 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.CalculateOverpayRelationIncreaseCosts](../source-paths/DefaultBarterModel.md) | 40 | 43 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 186 | 191 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.GetRelationIncreaseFactor](../source-paths/DefaultDiplomacyModel.md) | 188 | 191 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmMoralLeader

Choice leaf: `perk.CharmMoralLeader.choice`. Required skill: 200; alternative: `CharmNaturalLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2212`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmMoralLeader.primary | {VALUE} persuasion success required against characters of your own culture. | Personal | -1f | Add | TroopUsageFlags.Undefined |
| perk.CharmMoralLeader.secondary | {VALUE} relation with settlement notables when a project is completed in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.CalculatePersuasionGoalValue](../source-paths/DefaultPersuasionModel.md) | 148 | 159 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.CalculatePersuasionGoalValue](../source-paths/DefaultPersuasionModel.md) | 150 | 159 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmNaturalLeader

Choice leaf: `perk.CharmNaturalLeader.choice`. Required skill: 200; alternative: `CharmMoralLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2213`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmNaturalLeader.primary | {VALUE} persuasion success required against characters of different cultures. | Personal | -1f | Add | TroopUsageFlags.Undefined |
| perk.CharmNaturalLeader.secondary | {VALUE}% experience gain for companions. | ClanLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultGenericXpModel.GetXpMultiplier](../source-paths/DefaultGenericXpModel.md) | 10 | 15 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.CalculatePersuasionGoalValue](../source-paths/DefaultPersuasionModel.md) | 152 | 159 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.CalculatePersuasionGoalValue](../source-paths/DefaultPersuasionModel.md) | 154 | 159 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmPublicSpeaker

Choice leaf: `perk.CharmPublicSpeaker.choice`. Required skill: 225; alternative: `CharmParade`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2214`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmPublicSpeaker.primary | {VALUE}% renown gain from battles. | PartyLeader | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmPublicSpeaker.secondary | {VALUE}% effect from forums, marketplaces and festivals. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateRenownGain](../source-paths/DefaultBattleRewardModel.md) | 57 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateRenownGain](../source-paths/DefaultBattleRewardModel.md) | 59 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel.GetBuildingEffect](../source-paths/DefaultBuildingEffectModel.md) | 35 | 38 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmParade

Choice leaf: `perk.CharmParade.choice`. Required skill: 225; alternative: `CharmPublicSpeaker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2215`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmParade.primary | {VALUE} loyalty bonus to settlement while waiting in the settlement. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.CharmParade.secondary | {VALUE}% daily chance to gain +1 relationship with a random lord in the same army. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 145 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 147 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 151 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 153 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 158 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 160 | 167 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 165 | 167 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmCamaraderie

Choice leaf: `perk.CharmCamaraderie.choice`. Required skill: 250; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2216`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmCamaraderie.primary | Double the relation gain for helping lords in battle. | Personal | 2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CharmCamaraderie.secondary | {VALUE} companion limit | ClanLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetPlayerGainedRelationAmount](../source-paths/DefaultBattleRewardModel.md) | 41 | 46 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetPlayerGainedRelationAmount](../source-paths/DefaultBattleRewardModel.md) | 43 | 46 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.GetCompanionLimit](../source-paths/DefaultClanTierModel.md) | 158 | 163 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.GetCompanionLimit](../source-paths/DefaultClanTierModel.md) | 160 | 163 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CharmImmortalCharm

Choice leaf: `perk.CharmImmortalCharm.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2217`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CharmImmortalCharm.primary | {VALUE} influence per day. | Personal | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanPoliticsModel.CalculateInfluenceChangeInternal](../source-paths/DefaultClanPoliticsModel.md) | 41 | 94 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanPoliticsModel.CalculateInfluenceChangeInternal](../source-paths/DefaultClanPoliticsModel.md) | 43 | 94 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
