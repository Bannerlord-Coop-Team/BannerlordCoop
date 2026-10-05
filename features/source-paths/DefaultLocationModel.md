# DefaultLocationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultLocationModel.GetSettlementUpgradeLevel.10.member | locations.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return locationEncounter.Settlement.Town.GetWallLevel(); | Locally assigned targets: none located | Calls in this member: locationEncounter.Settlement.Town.GetWallLevel | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@10-13 |
| DefaultLocationModel.GetCivilianSceneLevel.15.member | locations.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return text; | Locally assigned targets: text, upgradeLevelTag | Calls in this member: GetUpgradeLevelTag, settlement.Town.GetWallLevel, upgradeLevelTag.IsEmpty | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@15-27 |
| DefaultLocationModel.GetCivilianSceneLevel.15.guard-18 | locations.008 | Source predicate is true: settlement.IsFortification | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: upgradeLevelTag, text | True-branch calls: GetUpgradeLevelTag, settlement.Town.GetWallLevel, upgradeLevelTag.IsEmpty | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@15-27 |
| DefaultLocationModel.GetCivilianSceneLevel.15.guard-21 | locations.008 | Source predicate is true: !upgradeLevelTag.IsEmpty() | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: text | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@15-27 |
| DefaultLocationModel.GetCivilianUpgradeLevelTag.29.member | locations.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return "";; return text; | Locally assigned targets: text, upgradeLevelTag | Calls in this member: GetUpgradeLevelTag, upgradeLevelTag.IsEmpty | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@29-42 |
| DefaultLocationModel.GetCivilianUpgradeLevelTag.29.guard-31 | locations.008 | Source predicate is true: upgradeLevel == 0 | Local true-branch returns: return ""; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@29-42 |
| DefaultLocationModel.GetCivilianUpgradeLevelTag.29.guard-37 | locations.008 | Source predicate is true: !upgradeLevelTag.IsEmpty() | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: text | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@29-42 |
| DefaultLocationModel.GetUpgradeLevelTag.44.member | locations.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return upgradeLevel switch { 1 =&gt; "level_1", 2 =&gt; "level_2", 3 =&gt; "level_3", _ =&gt; "", }; | Locally assigned targets: _ | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultLocationModel@44-53 |
