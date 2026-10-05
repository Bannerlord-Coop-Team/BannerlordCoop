# Crossbow perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## CrossbowPiercer

Choice leaf: `perk.CrossbowPiercer.choice`. Required skill: 25; alternative: `CrossbowMarksmen`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2032`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowPiercer.primary | Your crossbow attacks ignore armors below 20. | Personal | 20f | Add | TroopUsageFlags.Undefined |
| perk.CrossbowPiercer.secondary | {VALUE}% recruitment cost of ranged troops. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 260 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 262 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowMarksmen

Choice leaf: `perk.CrossbowMarksmen.choice`. Required skill: 25; alternative: `CrossbowPiercer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2033`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowMarksmen.primary | {VALUE}% faster aiming with crossbows. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowMarksmen.secondary | {VALUE}% starting battle morale to ranged troops in your party. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1442 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowUnhorser

Choice leaf: `perk.CrossbowUnhorser.choice`. Required skill: 50; alternative: `CrossbowWindWinder`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2034`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowUnhorser.primary | {VALUE}% crossbow damage to mounts. | Personal | 0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowUnhorser.secondary | {VALUE}% damage against mounts to crossbow troops in your formation. | Captain | 0.2f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 265 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 266 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowWindWinder

Choice leaf: `perk.CrossbowWindWinder.choice`. Required skill: 50; alternative: `CrossbowUnhorser`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2035`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowWindWinder.primary | {VALUE}% reload speed with crossbows. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowWindWinder.secondary | {VALUE}% crossbow reload speed to troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1436 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1439 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowDonkeysSwiftness

Choice leaf: `perk.CrossbowDonkeysSwiftness.choice`. Required skill: 75; alternative: `CrossbowSheriff`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2036`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowDonkeysSwiftness.primary | {VALUE}% accuracy loss while moving. | Personal | -0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowDonkeysSwiftness.secondary | {VALUE} crossbow skill to troops in your formation. | Captain | 30f | Add | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 278 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1441 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowSheriff

Choice leaf: `perk.CrossbowSheriff.choice`. Required skill: 75; alternative: `CrossbowDonkeysSwiftness`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2037`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowSheriff.primary | {VALUE}% headshot damage with crossbows. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowSheriff.secondary | {VALUE}% crossbow damage to infantry by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 270 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 274 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowPeasantLeader

Choice leaf: `perk.CrossbowPeasantLeader.choice`. Required skill: 100; alternative: `CrossbowRenownMarksmen`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2038`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowPeasantLeader.primary | {VALUE}% battle morale to tier 1 to 3 troops | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowPeasantLeader.secondary | {VALUE}% garrisoned ranged troop wages in the governed settlement. | Governor | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 134 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 164 | 190 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 167 | 190 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowRenownMarksmen

Choice leaf: `perk.CrossbowRenownMarksmen.choice`. Required skill: 100; alternative: `CrossbowPeasantLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2039`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowRenownMarksmen.primary | {VALUE} daily experience to ranged troops in your party. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |
| perk.CrossbowRenownMarksmen.secondary | {VALUE}% security provided by ranged troops in the garrison of the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 227 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 229 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 68 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 70 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 100 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowFletcher

Choice leaf: `perk.CrossbowFletcher.choice`. Required skill: 125; alternative: `CrossbowPuncture`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2040`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowFletcher.primary | {VALUE} bolts per quiver. | Personal | 4f | Add | TroopUsageFlags.Undefined |
| perk.CrossbowFletcher.secondary | {VALUE} bolts per quiver to troops in your party. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 163 | 202 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 164 | 202 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 166 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowPuncture

Choice leaf: `perk.CrossbowPuncture.choice`. Required skill: 125; alternative: `CrossbowFletcher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2041`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowPuncture.primary | {VALUE}% armor penetration with crossbows. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowPuncture.secondary | {VALUE}% armor penetration with crossbows by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.CrossbowUser |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowLooseAndMove

Choice leaf: `perk.CrossbowLooseAndMove.choice`. Required skill: 150; alternative: `CrossbowDeftHands`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2042`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowLooseAndMove.primary | Equipped crossbows do not slow you down. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.CrossbowLooseAndMove.secondary | {VALUE}% movement speed to ranged troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Ranged |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHumanStats](../source-paths/SandboxAgentStatCalculateModel.md) | 924 | 1128 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1379 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowDeftHands

Choice leaf: `perk.CrossbowDeftHands.choice`. Required skill: 150; alternative: `CrossbowLooseAndMove`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2043`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowDeftHands.primary | {VALUE}% resistance to getting staggered while reloading your crossbow. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowDeftHands.secondary | {VALUE}% resistance to getting staggered while reloading crossbows to troops in your formation. | Captain | 0.5f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1018 | 1029 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1021 | 1029 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowCounterFire

Choice leaf: `perk.CrossbowCounterFire.choice`. Required skill: 175; alternative: `CrossbowMountedCrossbowman`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2044`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowCounterFire.primary | {VALUE}% projectile damage taken while equipped with a crossbow. | Personal | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowCounterFire.secondary | {VALUE}% damage taken from projectiles by your troops. | Captain | -0.03f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 530 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 531 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowMountedCrossbowman

Choice leaf: `perk.CrossbowMountedCrossbowman.choice`. Required skill: 175; alternative: `CrossbowCounterFire`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2045`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowMountedCrossbowman.primary | You can reload any crossbow on horseback. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.CrossbowMountedCrossbowman.secondary | {VALUE}% experience gained to ranged troops in your party. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 105 | 121 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeAgentStats](../source-paths/SandboxAgentStatCalculateModel.md) | 74 | 91 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowSteady

Choice leaf: `perk.CrossbowSteady.choice`. Required skill: 200; alternative: `CrossbowLongShots`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2046`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowSteady.primary | {VALUE}% accuracy penalty with crossbows while mounted. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowSteady.secondary | {VALUE}% tariff gain in the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromTariffs](../source-paths/DefaultClanFinanceModel.md) | 439 | 452 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1433 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1434 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowLongShots

Choice leaf: `perk.CrossbowLongShots.choice`. Required skill: 200; alternative: `CrossbowSteady`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2047`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowLongShots.primary | {VALUE}% more zoom with crossbows. | Personal | 1f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowLongShots.secondary | {VALUE} daily militia recruitment in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 172 | 180 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetMaxCameraZoom](../source-paths/SandboxAgentStatCalculateModel.md) | 554 | 563 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowHammerBolts

Choice leaf: `perk.CrossbowHammerBolts.choice`. Required skill: 225; alternative: `CrossbowPavise`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2048`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowHammerBolts.primary | Crossbows can now dismount and ignore {VALUE}% dismount resistance on attacks against cavalry. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowHammerBolts.secondary | {VALUE}% damage with crossbows by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 276 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CanWeaponDismount](../source-paths/SandboxAgentApplyDamageModel.md) | 757 | 768 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 881 | 892 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 883 | 892 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowPavise

Choice leaf: `perk.CrossbowPavise.choice`. Required skill: 225; alternative: `CrossbowHammerBolts`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2049`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowPavise.primary | {VALUE}% chance of blocking projectiles from behind with a shield on your back. | Personal | 0.75f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowPavise.secondary | {VALUE}% accuracy to ballistas in the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 306 | 332 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.IsDamageIgnored](../source-paths/SandboxAgentApplyDamageModel.md) | 31 | 38 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.IsDamageIgnored](../source-paths/SandboxAgentApplyDamageModel.md) | 33 | 38 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowTerror

Choice leaf: `perk.CrossbowTerror.choice`. Required skill: 250; alternative: `CrossbowBoltenGuard`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2050`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowTerror.primary | {VALUE}% chance of increasing the siege bombardment casualties per hit by 1. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowTerror.secondary | {VALUE}% morale loss to enemy due to crossbow kills by troops in your formation. | Captain | 0.25f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetColleteralDamageCasualties](../source-paths/DefaultSiegeEventModel.md) | 258 | 263 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowBoltenGuard

Choice leaf: `perk.CrossbowBoltenGuard.choice`. Required skill: 250; alternative: `CrossbowTerror`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2051`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowBoltenGuard.primary | {VALUE}% wages of tier 4+ ranged troops. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowBoltenGuard.secondary | {VALUE} hit points to ranged troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 118 | 204 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 733 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## CrossbowMightyPull

Choice leaf: `perk.CrossbowMightyPull.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2052`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.CrossbowMightyPull.primary | {VALUE}% reload speed with crossbows for every skill point above 200. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.CrossbowMightyPull.secondary | {VALUE}% damage with crossbows for every skill point above 200. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 278 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1443 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
