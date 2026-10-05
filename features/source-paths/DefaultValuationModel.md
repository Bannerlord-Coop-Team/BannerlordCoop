# DefaultValuationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultValuationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultValuationModel.GetMilitaryValueOfParty.8.member | trade.011 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return party.Party.CalculateCurrentStrength() * 15f; | Locally assigned targets: none located | Calls in this member: party.Party.CalculateCurrentStrength | TaleWorlds.CampaignSystem.GameComponents.DefaultValuationModel@8-11 |
| DefaultValuationModel.GetValueOfTroop.13.member | trade.011 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return troop.GetPower() * 15f; | Locally assigned targets: none located | Calls in this member: troop.GetPower | TaleWorlds.CampaignSystem.GameComponents.DefaultValuationModel@13-16 |
| DefaultValuationModel.GetValueOfHero.18.member | trade.011 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return ((float)hero.Clan.Gold * 0.15f + (float)((1 + hero.Clan.Tier * hero.Clan.Tier) * 500)) * ((hero.Clan.Leader == hero) ? 4f : 1f);; return 500f; | Locally assigned targets: none located | Calls in this member: return | TaleWorlds.CampaignSystem.GameComponents.DefaultValuationModel@18-25 |
| DefaultValuationModel.GetValueOfHero.18.guard-20 | trade.011 | Source predicate is true: hero.Clan != null | Local true-branch returns: return ((float)hero.Clan.Gold * 0.15f + (float)((1 + hero.Clan.Tier * hero.Clan.Tier) * 500)) * ((hero.Clan.Leader == hero) ? 4f : 1f); | True-branch assignment targets: none located | True-branch calls: return | TaleWorlds.CampaignSystem.GameComponents.DefaultValuationModel@18-25 |
