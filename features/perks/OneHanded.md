# OneHanded perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## OneHandedWrappedHandles

Choice leaf: `perk.OneHandedWrappedHandles.choice`. Required skill: 25; alternative: `OneHandedBasher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1951`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedWrappedHandles.primary | {VALUE}% handling to one handed weapons. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedWrappedHandles.secondary | {VALUE} one handed skill to infantry troops in your formation. | Captain | 30f | Add | TroopUsageFlags.OneHandedUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 306 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1333 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedBasher

Choice leaf: `perk.OneHandedBasher.choice`. Required skill: 25; alternative: `OneHandedWrappedHandles`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1952`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedBasher.primary | {VALUE}% damage and longer stun duration with shield bashes. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedBasher.secondary | {VALUE}% damage taken by infantry while in shield wall formation. | Captain | -0.04f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 199 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 550 | 595 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1494 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedToBeBlunt

Choice leaf: `perk.OneHandedToBeBlunt.choice`. Required skill: 50; alternative: `OneHandedSwiftStrike`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1953`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedToBeBlunt.primary | {VALUE}% damage with one handed axes and maces. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedToBeBlunt.secondary | {VALUE} daily security to governed settlement. | Governor | 0.5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 283 | 287 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 127 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedSwiftStrike

Choice leaf: `perk.OneHandedSwiftStrike.choice`. Required skill: 50; alternative: `OneHandedToBeBlunt`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1954`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedSwiftStrike.primary | {VALUE}% swing speed with one handed weapons. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedSwiftStrike.secondary | {VALUE} daily militia recruitment in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 169 | 180 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1330 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedCavalry

Choice leaf: `perk.OneHandedCavalry.choice`. Required skill: 75; alternative: `OneHandedShieldBearer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1955`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedCavalry.primary | {VALUE}% damage with one handed weapons while mounted. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedCavalry.secondary | {VALUE}% melee damage by cavalry troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 118 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 212 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedShieldBearer

Choice leaf: `perk.OneHandedShieldBearer.choice`. Required skill: 75; alternative: `OneHandedCavalry`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1956`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedShieldBearer.primary | Removed movement speed penalty of wielding shields. | Personal | 0f | Invalid | TroopUsageFlags.Undefined |
| perk.OneHandedShieldBearer.secondary | {VALUE}% movement speed to infantry in your formation. | Captain | 0.03f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHumanStats](../source-paths/SandboxAgentStatCalculateModel.md) | 939 | 1128 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1512 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedTrainer

Choice leaf: `perk.OneHandedTrainer.choice`. Required skill: 100; alternative: `OneHandedDuelist`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1957`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedTrainer.primary | {VALUE} hit points. | Personal | 2f | Add | TroopUsageFlags.Undefined |
| perk.OneHandedTrainer.secondary | {VALUE}% experience to melee troops in your party after every battle. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 88 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 90 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 30 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedDuelist

Choice leaf: `perk.OneHandedDuelist.choice`. Required skill: 100; alternative: `OneHandedTrainer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1958`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedDuelist.primary | {VALUE}% damage while wielding a one handed weapon without a shield. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedDuelist.secondary | Double the amount of renown gained from tournaments. | Personal | 2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel.GetRenownReward](../source-paths/DefaultTournamentModel.md) | 62 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel.GetRenownReward](../source-paths/DefaultTournamentModel.md) | 64 | 71 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 123 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedShieldWall

Choice leaf: `perk.OneHandedShieldWall.choice`. Required skill: 125; alternative: `OneHandedArrowCatcher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1959`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedShieldWall.primary | {VALUE}% damage to your shield while blocking in wrong direction. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedShieldWall.secondary | Larger shield protection area against projectiles to troops in your formation while in shield wall formation. | Captain | 0.01f | Add | TroopUsageFlags.ShieldUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 572 | 595 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1486 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedArrowCatcher

Choice leaf: `perk.OneHandedArrowCatcher.choice`. Required skill: 125; alternative: `OneHandedShieldWall`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1960`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedArrowCatcher.primary | Larger shield protection area against projectiles. | Personal | 0.01f | Add | TroopUsageFlags.Undefined |
| perk.OneHandedArrowCatcher.secondary | Larger shield protection area against projectiles for troops in your formation. | Captain | 0.01f | Add | TroopUsageFlags.ShieldUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1488 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1490 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedMilitaryTradition

Choice leaf: `perk.OneHandedMilitaryTradition.choice`. Required skill: 150; alternative: `OneHandedCorpsACorps`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1961`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedMilitaryTradition.primary | {VALUE} daily experience to infantry in your party. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |
| perk.OneHandedMilitaryTradition.secondary | {VALUE}% garrison wages in the governed settlement. | Governor | -0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 128 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 48 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 50 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 100 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedCorpsACorps

Choice leaf: `perk.OneHandedCorpsACorps.choice`. Required skill: 150; alternative: `OneHandedMilitaryTradition`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1962`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedCorpsACorps.primary | {VALUE}% of the total experience gained as a bonus to infantry after battles. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedCorpsACorps.secondary | {VALUE} garrison limit in the governed settlement. | Governor | 30f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddGarrisonOwnerPerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 236 | 239 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 100 | 121 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedStandUnited

Choice leaf: `perk.OneHandedStandUnited.choice`. Required skill: 175; alternative: `OneHandedLeadByExample`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1963`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedStandUnited.primary | {VALUE} starting battle morale to troops in your party if you are outnumbered. | PartyLeader | 8f | Add | TroopUsageFlags.Undefined |
| perk.OneHandedStandUnited.secondary | {VALUE}% security provided by troops in the garrison of the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 205 | 232 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedLeadByExample

Choice leaf: `perk.OneHandedLeadByExample.choice`. Required skill: 175; alternative: `OneHandedStandUnited`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1964`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedLeadByExample.primary | {VALUE}% experience to troops in your party after battle. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedLeadByExample.secondary | {VALUE} starting battle morale to troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 102 | 121 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedSteelCoreShields

Choice leaf: `perk.OneHandedSteelCoreShields.choice`. Required skill: 200; alternative: `OneHandedFleetOfFoot`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1965`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedSteelCoreShields.primary | {VALUE}% damage to your shields. | Personal | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedSteelCoreShields.secondary | {VALUE}% damage to shields of infantry troops in your formation. | Captain | -0.1f | AddFactor | TroopUsageFlags.ShieldUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 561 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 564 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedFleetOfFoot

Choice leaf: `perk.OneHandedFleetOfFoot.choice`. Required skill: 200; alternative: `OneHandedSteelCoreShields`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1966`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedFleetOfFoot.primary | {VALUE}% combat movement speed. | Personal | 0.04f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedFleetOfFoot.secondary | {VALUE}% movement speed to infantry in your formation. | Captain | 0.04f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1301 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1513 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedDeadlyPurpose

Choice leaf: `perk.OneHandedDeadlyPurpose.choice`. Required skill: 225; alternative: `OneHandedUnwaveringDefense`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1967`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedDeadlyPurpose.primary | {VALUE}% damage with one handed weapons. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedDeadlyPurpose.secondary | {VALUE}% melee weapon damage by infantry in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 115 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 216 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedUnwaveringDefense

Choice leaf: `perk.OneHandedUnwaveringDefense.choice`. Required skill: 225; alternative: `OneHandedDeadlyPurpose`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1968`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedUnwaveringDefense.primary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.OneHandedUnwaveringDefense.secondary | {VALUE} hit points to infantry in your party. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 35 | 47 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 744 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedPrestige

Choice leaf: `perk.OneHandedPrestige.choice`. Required skill: 250; alternative: `OneHandedChinkInTheArmor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1969`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedPrestige.primary | {VALUE}% damage against shields with one handed weapons. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedPrestige.secondary | {VALUE} party limit. | PartyLeader | 15f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 303 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 305 | 372 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 131 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedChinkInTheArmor

Choice leaf: `perk.OneHandedChinkInTheArmor.choice`. Required skill: 250; alternative: `OneHandedPrestige`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1970`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedChinkInTheArmor.primary | {VALUE}% armor penetration with melee attacks. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedChinkInTheArmor.secondary | {VALUE}% recruitment cost of infantry. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 241 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 243 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## OneHandedWayOfTheSword

Choice leaf: `perk.OneHandedWayOfTheSword.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1971`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.OneHandedWayOfTheSword.primary | {VALUE}% attack speed with one handed weapons for every skill point above 250. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.OneHandedWayOfTheSword.secondary | {VALUE}% damage with one handed weapons for every skill point above 250. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 134 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1331 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1332 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
