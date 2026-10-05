# DefaultCharacterStatsModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultCharacterStatsModel.WoundedHitPointLimit.13.member | progression.006 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 20; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@13-16 |
| DefaultCharacterStatsModel.GetTier.18.member | progression.006 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 0;; return MathF.Min(MathF.Max(MathF.Ceiling(((float)character.Level - 5f) / 5f), 0), Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier); | Locally assigned targets: none located | Calls in this member: MathF.Min, MathF.Max, MathF.Ceiling | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@18-25 |
| DefaultCharacterStatsModel.GetTier.18.guard-20 | progression.006 | Source predicate is true: character.IsHero | Local true-branch returns: return 0; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@18-25 |
| DefaultCharacterStatsModel.MaxHitpoints.27.member | progression.006 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return bonuses; | Locally assigned targets: bonuses, num | Calls in this member: ExplainedNumber, PerkHelper.AddPerkBonusForCharacter, character.HeroObject.PartyBelongedTo.HasPerk, bonuses.Add, character.GetPerkValue, character.GetSkillValue | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@27-47 |
| DefaultCharacterStatsModel.MaxHitpoints.27.guard-37 | progression.006 | Source predicate is true: character.IsHero &amp;&amp; character.HeroObject.PartyBelongedTo != null &amp;&amp; character.HeroObject.PartyBelongedTo.LeaderHero != character.HeroObject &amp;&amp; character.HeroObject.PartyBelongedTo.HasPerk(DefaultPerks.Medicine.FortitudeTonic) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: bonuses.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@27-47 |
| DefaultCharacterStatsModel.MaxHitpoints.27.guard-41 | progression.006 | Source predicate is true: character.GetPerkValue(DefaultPerks.Athletics.MightyBlow) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: character.GetSkillValue, bonuses.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel@27-47 |
