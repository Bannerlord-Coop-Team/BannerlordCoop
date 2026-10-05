# Polearm perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## PolearmPikeman

Choice leaf: `perk.PolearmPikeman.choice`. Required skill: 25; alternative: `PolearmCavalry`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1990`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmPikeman.primary | {VALUE}% damage with polearms on foot. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmPikeman.secondary | {VALUE}% damage by infantry troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 174 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 369 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmCavalry

Choice leaf: `perk.PolearmCavalry.choice`. Required skill: 25; alternative: `PolearmPikeman`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1991`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmCavalry.primary | {VALUE}% damage with polearms while mounted. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmCavalry.secondary | {VALUE}% damage by cavalry troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.Mounted |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 170 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 377 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmBraced

Choice leaf: `perk.PolearmBraced.choice`. Required skill: 50; alternative: `PolearmKeepAtBay`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1992`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmBraced.primary | Polearms that can dismount ignore {VALUE}% dismount resistance on attacks against cavalry. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmBraced.secondary | {VALUE}% damage by infantry in your formation against cavalry. | Captain | 0.1f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 372 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 877 | 892 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 879 | 892 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmKeepAtBay

Choice leaf: `perk.PolearmKeepAtBay.choice`. Required skill: 50; alternative: `PolearmBraced`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1993`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmKeepAtBay.primary | Polearms ignore {VALUE}% knockback resistance on thrust attacks against footmen. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmKeepAtBay.secondary | {VALUE} militia recruitment in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 170 | 180 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockBackPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 903 | 909 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockBackPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 905 | 909 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmSwiftSwing

Choice leaf: `perk.PolearmSwiftSwing.choice`. Required skill: 75; alternative: `PolearmCleanThrust`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1994`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmSwiftSwing.primary | {VALUE}% swing speed with polearms. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmSwiftSwing.secondary | {VALUE}% swing speed to infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1324 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1345 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmCleanThrust

Choice leaf: `perk.PolearmCleanThrust.choice`. Required skill: 75; alternative: `PolearmSwiftSwing`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1995`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmCleanThrust.primary | {VALUE}% thrust damage with polearms. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmCleanThrust.secondary | {VALUE} polearm skill to infantry in your formation. | Captain | 30f | Add | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.PolearmUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 179 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 314 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmFootwork

Choice leaf: `perk.PolearmFootwork.choice`. Required skill: 100; alternative: `PolearmHardKnock`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1996`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmFootwork.primary | {VALUE}% combat movement speed with polearms. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmFootwork.secondary | {VALUE}% movement speed to infantry in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1344 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1515 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmHardKnock

Choice leaf: `perk.PolearmHardKnock.choice`. Required skill: 100; alternative: `PolearmFootwork`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1997`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmHardKnock.primary | Polearms that can knockdown ignore {VALUE}% knockdown resistance on thrust attacks. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmHardKnock.secondary | {VALUE} hit points to infantry in your party. | PartyLeader | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockDownPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 952 | 958 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetKnockDownPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 954 | 958 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 741 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmSteadKiller

Choice leaf: `perk.PolearmSteadKiller.choice`. Required skill: 125; alternative: `PolearmLancer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1998`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmSteadKiller.primary | {VALUE}% damage to mounts with polearms. | Personal | 0.7f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmSteadKiller.secondary | {VALUE}% damage to mounts with polearms by infantry in your formation. | Captain | 0.3f | AddFactor | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.PolearmUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 184 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 187 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmLancer

Choice leaf: `perk.PolearmLancer.choice`. Required skill: 125; alternative: `PolearmSteadKiller`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:1999`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmLancer.primary | {VALUE}% damage bonus from speed with polearms while mounted. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmLancer.secondary | {VALUE}% damage bonus from speed with polearms by troops in your formation. | Captain | 0.3f | AddFactor | TroopUsageFlags.PolearmUser |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmSkewer

Choice leaf: `perk.PolearmSkewer.choice`. Required skill: 150; alternative: `PolearmGuards`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2000`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmSkewer.primary | {VALUE}% chance of your lance staying couched after a kill. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmSkewer.secondary | {VALUE} daily security in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 285 | 287 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.DecidePassiveAttackCollisionReaction](../source-paths/SandboxAgentApplyDamageModel.md) | 1083 | 1093 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.DecidePassiveAttackCollisionReaction](../source-paths/SandboxAgentApplyDamageModel.md) | 1085 | 1093 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmGuards

Choice leaf: `perk.PolearmGuards.choice`. Required skill: 150; alternative: `PolearmSkewer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2001`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmGuards.primary | {VALUE}% damage when you hit an enemy in the head with a polearm. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmGuards.secondary | {VALUE}% experience gain to garrisoned cavalry in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 118 | 121 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 192 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmStandardBearer

Choice leaf: `perk.PolearmStandardBearer.choice`. Required skill: 175; alternative: `PolearmPhalanx`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2002`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmStandardBearer.primary | {VALUE}% battle morale loss to troops in your formation. | Captain | -0.2f | AddFactor | TroopUsageFlags.None |
| perk.PolearmStandardBearer.secondary | {VALUE}% wages to garrisoned infantry in the governed settlement. | Governor | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 132 | 204 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmPhalanx

Choice leaf: `perk.PolearmPhalanx.choice`. Required skill: 175; alternative: `PolearmStandardBearer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2003`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmPhalanx.primary | {VALUE} melee weapon skills to troops in your party while in shield wall formation. | PartyLeader | 30f | Add | TroopUsageFlags.Undefined |
| perk.PolearmPhalanx.secondary | {VALUE}% damage with polearms by troops in your formation. | Captain | 0.03f | AddFactor | TroopUsageFlags.PolearmUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 194 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 299 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmHardyFrontline

Choice leaf: `perk.PolearmHardyFrontline.choice`. Required skill: 200; alternative: `PolearmDrills`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2004`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmHardyFrontline.primary | {VALUE} hit points to troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |
| perk.PolearmHardyFrontline.secondary | {VALUE}% recruitment cost of infantry. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 249 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 251 | 287 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 729 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmDrills

Choice leaf: `perk.PolearmDrills.choice`. Required skill: 200; alternative: `PolearmHardyFrontline`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2005`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmDrills.primary | {VALUE}% rate of militias will spawn as veteran troops in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |
| perk.PolearmDrills.secondary | {VALUE} bonus daily experience to troops in your party. | PartyLeader | 0.1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 64 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 66 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 44 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 46 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmSureFooted

Choice leaf: `perk.PolearmSureFooted.choice`. Required skill: 225; alternative: `PolearmUnstoppableForce`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2006`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmSureFooted.primary | {VALUE}% charge damage taken. | Personal | -0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmSureFooted.secondary | {VALUE}% charge damage taken by troops in your formation. | Captain | -0.3f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 579 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 583 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmUnstoppableForce

Choice leaf: `perk.PolearmUnstoppableForce.choice`. Required skill: 225; alternative: `PolearmSureFooted`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2007`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmUnstoppableForce.primary | Triple couch lance damage against shields. | Personal | 3f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmUnstoppableForce.secondary | {VALUE}% damage bonus from speed with polearms to cavalry in your formation. | Captain | 0.3f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.PolearmUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculatePassiveAttackDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1064 | 1070 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculatePassiveAttackDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1066 | 1070 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmCounterweight

Choice leaf: `perk.PolearmCounterweight.choice`. Required skill: 250; alternative: `PolearmSharpenTheTip`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2008`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmCounterweight.primary | {VALUE}% handling of swingable polearms. | Personal | 0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmCounterweight.secondary | {VALUE} polearm skill to troops in your formation. | Captain | 20f | Add | TroopUsageFlags.PolearmUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 315 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1350 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmSharpenTheTip

Choice leaf: `perk.PolearmSharpenTheTip.choice`. Required skill: 250; alternative: `PolearmCounterweight`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2009`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmSharpenTheTip.primary | {VALUE}% damage with thrust attacks made with polearms. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmSharpenTheTip.secondary | {VALUE}% damage with thrust attacks by infantry troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.OnFoot &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 180 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 220 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## PolearmWayOfTheSpear

Choice leaf: `perk.PolearmWayOfTheSpear.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2010`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.PolearmWayOfTheSpear.primary | {VALUE}% attack speed with polearms for every skill point above 250. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.PolearmWayOfTheSpear.secondary | {VALUE}% damage with polearms for every skill point above 250. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 195 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1346 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1347 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
