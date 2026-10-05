# Leadership perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## LeadershipCombatTips

Choice leaf: `perk.LeadershipCombatTips.choice`. Required skill: 25; alternative: `LeadershipRaiseTheMeek`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2218`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipCombatTips.primary | {VALUE} experience per day to all troops in party. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipCombatTips.secondary | {VALUE} to troop tiers when recruiting from same culture. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 32 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 34 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 100 | 109 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 30 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 32 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipRaiseTheMeek

Choice leaf: `perk.LeadershipRaiseTheMeek.choice`. Required skill: 25; alternative: `LeadershipCombatTips`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2219`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipRaiseTheMeek.primary | {VALUE} experience per day to tier 1 and 2 troops. | PartyLeader | 4f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipRaiseTheMeek.secondary | {VALUE} experience per day to each troop in garrison in the governed settlement. | Governor | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel.CalculateTroopXpBonusInternal](../source-paths/DefaultDailyTroopXpBonusModel.md) | 20 | 23 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 36 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 38 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 100 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipFerventAttacker

Choice leaf: `perk.LeadershipFerventAttacker.choice`. Required skill: 50; alternative: `LeadershipStoutDefender`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2220`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipFerventAttacker.primary | {VALUE} starting battle morale when attacking. | PartyLeader | 4f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipFerventAttacker.secondary | {VALUE}% recruitment rate of tier 1, 2 and 3 prisoners. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 28 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipStoutDefender

Choice leaf: `perk.LeadershipStoutDefender.choice`. Required skill: 50; alternative: `LeadershipFerventAttacker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2221`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipStoutDefender.primary | {VALUE} starting battle morale when defending. | PartyLeader | 8f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipStoutDefender.secondary | {VALUE}% recruitment rate of tier 4+ prisoners. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 30 | 51 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 32 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipAuthority

Choice leaf: `perk.LeadershipAuthority.choice`. Required skill: 75; alternative: `LeadershipHeroicLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2222`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipAuthority.primary | {VALUE}% security bonus from the town garrison in the governing settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipAuthority.secondary | {VALUE} party size limit. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 327 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 329 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 209 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 211 | 232 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipHeroicLeader

Choice leaf: `perk.LeadershipHeroicLeader.choice`. Required skill: 75; alternative: `LeadershipAuthority`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2223`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipHeroicLeader.primary | {VALUE} daily loyalty in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipHeroicLeader.secondary | {VALUE}% battle morale penalty to enemies when troops in your formation kill an enemy. | Captain | 0.1f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 127 | 167 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipLoyaltyAndHonor

Choice leaf: `perk.LeadershipLoyaltyAndHonor.choice`. Required skill: 100; alternative: `LeadershipFamousCommander`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2224`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipLoyaltyAndHonor.primary | Tier 3+ troops in your party no longer retreat due to low morale | PartyLeader | 3f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipLoyaltyAndHonor.secondary | {VALUE}% faster non-bandit prisoner recruitment. | PartyLeader | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 34 | 51 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 36 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipFamousCommander

Choice leaf: `perk.LeadershipFamousCommander.choice`. Required skill: 100; alternative: `LeadershipLoyaltyAndHonor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2225`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipFamousCommander.primary | {VALUE}% renown gain from battles. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipFamousCommander.secondary | {VALUE} experience to troops on recruitment. | Personal | 200f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateRenownGain](../source-paths/DefaultBattleRewardModel.md) | 63 | 71 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipPresence

Choice leaf: `perk.LeadershipPresence.choice`. Required skill: 125; alternative: `LeadershipLeaderOfMasses`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2226`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipPresence.primary | {VALUE} security per day while waiting in a town. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipPresence.secondary | No morale penalty for recruiting prisoners of your faction. | PartyLeader | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 273 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 274 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 277 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 58 | 75 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.ShouldPartyRecruitPrisoners](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 95 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipLeaderOfMasses

Choice leaf: `perk.LeadershipLeaderOfMasses.choice`. Required skill: 125; alternative: `LeadershipPresence`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2227`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipLeaderOfMasses.primary | {VALUE} party size for each town you control. | ClanLeader | 5f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipLeaderOfMasses.secondary | {VALUE}% experience from battles shared with the troops in your party. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 344 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 354 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 357 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GenerateSharedXp](../source-paths/DefaultPartyTrainingModel.md) | 114 | 127 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipVeteransRespect

Choice leaf: `perk.LeadershipVeteransRespect.choice`. Required skill: 150; alternative: `LeadershipCitizenMilitia`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2228`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipVeteransRespect.primary | {VALUE} garrison size in the governed settlement. | Governor | 20f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipVeteransRespect.secondary | Bandits can be converted into regular troops. | PartyLeader | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.DoesPartyHaveRequiredPerksForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 131 | 135 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddGarrisonOwnerPerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 237 | 239 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipCitizenMilitia

Choice leaf: `perk.LeadershipCitizenMilitia.choice`. Required skill: 150; alternative: `LeadershipVeteransRespect`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2229`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipCitizenMilitia.primary | {VALUE}% rate of militias will spawn as veteran troops in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipCitizenMilitia.secondary | {VALUE}% morale from victories. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 60 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 62 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateMoraleGainVictory](../source-paths/DefaultBattleRewardModel.md) | 96 | 102 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateMoraleGainVictory](../source-paths/DefaultBattleRewardModel.md) | 98 | 102 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipInspiringLeader

Choice leaf: `perk.LeadershipInspiringLeader.choice`. Required skill: 175; alternative: `LeadershipUpliftingSpirit`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2230`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipInspiringLeader.primary | {VALUE}% influence cost for calling parties to an army. | ArmyCommander | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipInspiringLeader.secondary | {VALUE}% experience to troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetXpFromHit](../source-paths/DefaultCombatXpModel.md) | 53 | 58 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetXpFromHit](../source-paths/DefaultCombatXpModel.md) | 55 | 58 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost](../source-paths/DefaultArmyManagementCalculationModel.md) | 99 | 117 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost](../source-paths/DefaultArmyManagementCalculationModel.md) | 101 | 117 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipUpliftingSpirit

Choice leaf: `perk.LeadershipUpliftingSpirit.choice`. Required skill: 175; alternative: `LeadershipInspiringLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2231`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipUpliftingSpirit.primary | {VALUE} battle morale in siege battles. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipUpliftingSpirit.secondary | {VALUE} party size limit. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 331 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 333 | 372 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipTrustedCommander

Choice leaf: `perk.LeadershipTrustedCommander.choice`. Required skill: 200; alternative: `LeadershipLeadByExample`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2232`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipTrustedCommander.primary | {VALUE}% recruitment rate for ranged prisoners. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipTrustedCommander.secondary | {VALUE}% experience for troops, when they are sent to confront the enemy. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.CalculateXpGainFromBattles](../source-paths/DefaultPartyTrainingModel.md) | 134 | 137 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 44 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipLeadByExample

Choice leaf: `perk.LeadershipLeadByExample.choice`. Required skill: 200; alternative: `LeadershipTrustedCommander`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2233`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipLeadByExample.primary | {VALUE}% recruitment rate for infantry prisoners. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipLeadByExample.secondary | {VALUE}% shared experience for cavalry troops. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GenerateSharedXp](../source-paths/DefaultPartyTrainingModel.md) | 119 | 127 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerRecruitmentCalculationModel.GetConformityChangePerHour](../source-paths/DefaultPrisonerRecruitmentCalculationModel.md) | 40 | 51 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipMakeADifference

Choice leaf: `perk.LeadershipMakeADifference.choice`. Required skill: 225; alternative: `LeadershipGreatLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2234`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipMakeADifference.primary | {VALUE}% battle morale to troops when you kill an enemy in battle. | Personal | 1f | AddFactor | TroopUsageFlags.Undefined |
| perk.LeadershipMakeADifference.secondary | {VALUE}% shared experience for archers. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GenerateSharedXp](../source-paths/DefaultPartyTrainingModel.md) | 123 | 127 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipGreatLeader

Choice leaf: `perk.LeadershipGreatLeader.choice`. Required skill: 225; alternative: `LeadershipMakeADifference`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2235`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipGreatLeader.primary | {VALUE} battle morale to troops at the beginning of a battle. | ArmyCommander | 5f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipGreatLeader.secondary | {VALUE} battle morale to troops that are of same culture as you. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipWePledgeOurSwords

Choice leaf: `perk.LeadershipWePledgeOurSwords.choice`. Required skill: 250; alternative: `LeadershipTalentMagnet`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2236`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipWePledgeOurSwords.primary | {VALUE} companion limit. | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipWePledgeOurSwords.secondary | {VALUE} battle morale at the beginning of the battle for each tier 6 troop in the party up to 10 morale. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.GetCompanionLimit](../source-paths/DefaultClanTierModel.md) | 154 | 163 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.GetCompanionLimit](../source-paths/DefaultClanTierModel.md) | 156 | 163 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipTalentMagnet

Choice leaf: `perk.LeadershipTalentMagnet.choice`. Required skill: 250; alternative: `LeadershipWePledgeOurSwords`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2237`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipTalentMagnet.primary | {VALUE} party size limit. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.LeadershipTalentMagnet.secondary | {VALUE} clan party limit. | ClanLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 335 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 337 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.AddPartyLimitPerkEffects](../source-paths/DefaultClanTierModel.md) | 145 | 149 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.AddPartyLimitPerkEffects](../source-paths/DefaultClanTierModel.md) | 147 | 149 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## LeadershipUltimateLeader

Choice leaf: `perk.LeadershipUltimateLeader.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2238`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.LeadershipUltimateLeader.primary | {VALUE} party size for each leadership point above 250. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 339 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 342 | 372 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
