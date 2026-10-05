# DefaultPrisonerDonationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultPrisonerDonationModel.CalculateRelationGainAfterHeroPrisonerDonate.12.member | prisoners.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return result; | Locally assigned targets: result, num, relation, num2 | Calls in this member: Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue, donatedHero.GetRelation, MathF.Min, MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel@12-23 |
| DefaultPrisonerDonationModel.CalculateRelationGainAfterHeroPrisonerDonate.12.guard-17 | prisoners.009 | Source predicate is true: relation &lt;= 0 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num2, result | True-branch calls: MathF.Min, MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel@12-23 |
| DefaultPrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation.25.member | prisoners.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return MathF.Pow(Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(donatedPrisoner, donatingParty.LeaderHero), 0.4f) * 0.2f; | Locally assigned targets: none located | Calls in this member: MathF.Pow, Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel@25-28 |
| DefaultPrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation.30.member | prisoners.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return stat.ResultNumber; | Locally assigned targets: leaderHero, stat | Calls in this member: ExplainedNumber, donatedCharacter.GetPower, leaderHero.GetPerkValue, PerkHelper.AddPerkBonusForParty | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel@30-39 |
| DefaultPrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation.30.guard-34 | prisoners.009 | Source predicate is true: leaderHero != null &amp;&amp; leaderHero.GetPerkValue(DefaultPerks.Steward.Relocation) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: PerkHelper.AddPerkBonusForParty | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel@30-39 |
