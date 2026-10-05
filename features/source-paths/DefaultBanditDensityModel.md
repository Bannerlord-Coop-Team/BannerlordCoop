# DefaultBanditDensityModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultBanditDensityModel.GetMinimumTroopCountForHideoutMission.44.member | ai.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 25;; return 8; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@44-51 |
| DefaultBanditDensityModel.GetMinimumTroopCountForHideoutMission.44.guard-46 | ai.008 | Source predicate is true: !isAssault | Local true-branch returns: return 25; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@44-51 |
| DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan.53.member | ai.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 50;; return 270 - DeserterClan.WarPartyComponents.Count;; return 270; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@53-64 |
| DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan.53.guard-55 | ai.008 | Source predicate is true: clan == DeserterClan | Local true-branch returns: return 50; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@53-64 |
| DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan.53.guard-59 | ai.008 | Source predicate is true: clan.StringId == "looters" &amp;&amp; DeserterClan != null | Local true-branch returns: return 270 - DeserterClan.WarPartyComponents.Count; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@53-64 |
| DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission.66.member | ai.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return num; | Locally assigned targets: num | Calls in this member: party.HasPerk | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@66-74 |
| DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission.66.guard-69 | ai.008 | Source predicate is true: party.HasPerk(DefaultPerks.Tactics.SmallUnitTactics) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@66-74 |
| DefaultBanditDensityModel.IsPositionInsideNavalSafeZone.76.member | ai.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return false; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel@76-79 |
