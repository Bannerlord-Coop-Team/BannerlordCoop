# r15 relation check

- Selected giver: `Hero_lord_3_5` (Tais), same Aserai kingdom as owner.
- Owner client conversation state: `coop.debug.hero_conversation.state` reported `heroId=Hero_lord_3_5`, `relation=1`.
- Owner client probe: `coop.debug.hero.get_relation Hero_lord_3_5 Player2863` returned `0`.
- Server probe: `coop.debug.hero.get_relation Hero_lord_3_5 Player2863` returned `0`.
- Server eligibility: `coop.debug.ittest.run can_create RaidAnEnemyTerritoryIssue Hero_lord_3_5` returned `canCreate=True`, faction `aserai`, with wars including `empire`.
- Setup command: `coop.debug.kingdom.declare_war Kingdom_aserai Kingdom_empire default testclient` (already at war in this cycle), then the relation probes above.
- Vanilla offer condition was satisfied in the live conversation, and the quest was accepted by the owner client.