# TwoHanded perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## TwoHandedStrongGrip

Choice leaf: `perk.TwoHandedStrongGrip.choice`. Required skill: 25; alternative: `TwoHandedWoodChopper`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1972`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedStrongGrip.primary | {VALUE}% handling to two handed weapons. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedStrongGrip.secondary | {VALUE} two handed skill to infantry troops in your formation. | Captain | 30f | Add | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.TwoHandedUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 310 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1340 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedWoodChopper

Choice leaf: `perk.TwoHandedWoodChopper.choice`. Required skill: 25; alternative: `TwoHandedStrongGrip`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1973`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedWoodChopper.primary | {VALUE}% damage to shields with two handed weapons. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedWoodChopper.secondary | {VALUE}% damage against shields by troops in your formation. | Captain | 0.15f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 140 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 141 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedOnTheEdge

Choice leaf: `perk.TwoHandedOnTheEdge.choice`. Required skill: 50; alternative: `TwoHandedHeadBasher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1974`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedOnTheEdge.primary | {VALUE}% swing speed with two handed weapons. | Personal | 0.03f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedOnTheEdge.secondary | {VALUE}% swing speed to infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1322 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1337 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedHeadBasher

Choice leaf: `perk.TwoHandedHeadBasher.choice`. Required skill: 50; alternative: `TwoHandedOnTheEdge`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1975`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedHeadBasher.primary | {VALUE}% damage with two handed axes and maces. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedHeadBasher.secondary | {VALUE}% damage by infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 147 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 367 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedShowOfStrength

Choice leaf: `perk.TwoHandedShowOfStrength.choice`. Required skill: 75; alternative: `TwoHandedBaptisedInBlood`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1976`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedShowOfStrength.primary | Two handed weapons that can knockdown ignore {VALUE}% knockdown resistance on swing attacks. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedShowOfStrength.secondary | {VALUE}% recruitment cost of infantry. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 245 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 247 | 287 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockDownPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 942 | 958 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockDownPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 944 | 958 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedBaptisedInBlood

Choice leaf: `perk.TwoHandedBaptisedInBlood.choice`. Required skill: 75; alternative: `TwoHandedShowOfStrength`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1977`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedBaptisedInBlood.primary | {VALUE} experience to infantry in your party for each enemy you kill with a two handed weapon. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.TwoHandedBaptisedInBlood.secondary | {VALUE}% experience to melee troops in your party after every battle. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 92 | 121 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedBeastSlayer

Choice leaf: `perk.TwoHandedBeastSlayer.choice`. Required skill: 100; alternative: `TwoHandedShieldBreaker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1978`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedBeastSlayer.primary | {VALUE}% damage to mounts with two handed weapons. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedBeastSlayer.secondary | {VALUE}% damage to mounts by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 151 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 152 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedShieldBreaker

Choice leaf: `perk.TwoHandedShieldBreaker.choice`. Required skill: 100; alternative: `TwoHandedBeastSlayer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1979`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedShieldBreaker.primary | {VALUE}% damage to shields with two handed weapons. | Personal | 0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedShieldBreaker.secondary | {VALUE}% damage against shields by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 142 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 143 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedBerserker

Choice leaf: `perk.TwoHandedBerserker.choice`. Required skill: 125; alternative: `TwoHandedConfidence`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1980`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedBerserker.primary | {VALUE}% damage with two handed weapons while you have less than half of your hit points. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedBerserker.secondary | {VALUE}% garrison wages in the governed settlement. | Governor | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 129 | 204 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 156 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedConfidence

Choice leaf: `perk.TwoHandedConfidence.choice`. Required skill: 125; alternative: `TwoHandedBerserker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1981`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedConfidence.primary | {VALUE}% damage with two handed weapons while you have more than 90% of your hit points. | Personal | 0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedConfidence.secondary | {VALUE}% build speed to military projects in the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 133 | 160 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 160 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedProjectileDeflection

Choice leaf: `perk.TwoHandedProjectileDeflection.choice`. Required skill: 150; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1982`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedProjectileDeflection.primary | You can deflect projectiles with two handed swords by blocking. | Personal | 0f | Invalid | TroopUsageFlags.Undefined |
| perk.TwoHandedProjectileDeflection.secondary | {VALUE}% experience to garrison troops in the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 115 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel.CalculateTroopXpBonusInternal](../source-paths/DefaultDailyTroopXpBonusModel.md) | 21 | 23 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeAgentStats](../source-paths/SandboxAgentStatCalculateModel.md) | 78 | 91 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedTerror

Choice leaf: `perk.TwoHandedTerror.choice`. Required skill: 175; alternative: `TwoHandedHope`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1983`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedTerror.primary | {VALUE}% battle morale effect to enemy troops with your two handed kills. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedTerror.secondary | {VALUE} prisoner limit. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 213 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 215 | 230 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedHope

Choice leaf: `perk.TwoHandedHope.choice`. Required skill: 175; alternative: `TwoHandedTerror`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1984`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedHope.primary | {VALUE}% battle morale effect to friendly troops with your two handed kills. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedHope.secondary | {VALUE} party limit. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 307 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 309 | 372 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedRecklessCharge

Choice leaf: `perk.TwoHandedRecklessCharge.choice`. Required skill: 200; alternative: `TwoHandedThickHides`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1985`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedRecklessCharge.primary | {VALUE}% damage bonus from speed with two handed weapons while on foot. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedRecklessCharge.secondary | {VALUE}% damage and movement speed to infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 368 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1514 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedThickHides

Choice leaf: `perk.TwoHandedThickHides.choice`. Required skill: 200; alternative: `TwoHandedRecklessCharge`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1986`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedThickHides.primary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.TwoHandedThickHides.secondary | {VALUE} hit points to troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 31 | 47 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 728 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedBladeMaster

Choice leaf: `perk.TwoHandedBladeMaster.choice`. Required skill: 225; alternative: `TwoHandedVandal`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1987`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedBladeMaster.primary | {VALUE}% damage with two handed weapons. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedBladeMaster.secondary | {VALUE}% attack speed to infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 162 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1323 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1325 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedVandal

Choice leaf: `perk.TwoHandedVandal.choice`. Required skill: 225; alternative: `TwoHandedBladeMaster`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1988`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedVandal.primary | {VALUE}% armor penetration with your attacks. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedVandal.secondary | {VALUE}% damage against destructible objects by troops in your formation. | Captain | 0.2f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 394 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TwoHandedWayOfTheGreatAxe

Choice leaf: `perk.TwoHandedWayOfTheGreatAxe.choice`. Required skill: 250; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1989`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TwoHandedWayOfTheGreatAxe.primary | {VALUE}% attack speed with two handed weapons for every skill point above 250. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.TwoHandedWayOfTheGreatAxe.secondary | {VALUE}% damage with two handed weapons for every skill point above 250. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 164 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1338 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1339 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
