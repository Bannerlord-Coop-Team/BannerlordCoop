# Installed Bannerlord definitions

Generated from [baseline/installed.json](baseline/installed.json).
This is an independently inspected Native v1.4.8 snapshot. Definitions and registration do not prove gameplay support.
Line numbers refer to the named decompiled type; owning assembly hashes are in [provenance](provenance.md).

## Normal issue availability

The current source allowlist is applied separately from the installed definitions.
Debug grant catalog entries are not an exception to normal gameplay gating. The quest journal is separately blocked
by `GameUIDisable.PushStatePatch` for `QuestsState`.

| Behavior | Normal co-op gate | Registrar / line |
| --- | --- | --- |
| ArmyNeedsSuppliesIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:162 |
| ArtisanCantSellProductsAtAFairPriceIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:163 |
| ArtisanOverpricedGoodsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:164 |
| BettingFraudIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:197 |
| CapturedByBountyHuntersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:165 |
| CaravanAmbushIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:166 |
| EscortMerchantCaravanIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:167 |
| ExtortionByDesertersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:168 |
| FamilyFeudIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:76 |
| GangLeaderNeedsRecruitsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:194 |
| GangLeaderNeedsSpecialWeaponsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:195 |
| GangLeaderNeedsToOffloadStolenGoodsIssueBehavior | allowlisted-source-only | TaleWorlds.CampaignSystem.SandBoxManager:169 |
| GangLeaderNeedsWeaponsIssueQuestBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:170 |
| HeadmanNeedsGrainIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:172 |
| HeadmanNeedsToDeliverAHerdIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:173 |
| HeadmanVillageNeedsDraughtAnimalsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:174 |
| LadysKnightOutIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:175 |
| LandLordCompanyOfTroubleIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:176 |
| LandLordNeedsManualLaborersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:179 |
| LandLordTheArtOfTheTradeIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:177 |
| LandlordNeedsAccessToVillageCommonsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:178 |
| LandlordTrainingForRetainersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:180 |
| LesserNobleRevoltIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:196 |
| LordNeedsGarrisonTroopsIssueQuestBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:181 |
| LordNeedsHorsesIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:185 |
| LordWantsRivalCapturedIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:187 |
| LordsNeedsTutorIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:186 |
| MerchantArmyOfPoachersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:188 |
| MerchantNeedsHelpWithOutlawsIssueQuestBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:189 |
| NearbyBanditBaseIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:190 |
| NotableWantsDaughterFoundIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:77 |
| ProdigalSonIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:79 |
| RaidAnEnemyTerritoryIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:191 |
| RevenueFarmingIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:171 |
| RivalGangMovingInIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:74 |
| RuralNotableInnAndOutIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:75 |
| ScoutEnemyGarrisonsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:192 |
| SmugglersIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:184 |
| SnareTheWealthyIssueBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:81 |
| TheConquestOfSettlementIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:182 |
| TheSpyPartyIssueQuestBehavior | disabled-by-issue-gate | SandBox.SandBoxSubModule:78 |
| VillageNeedsCraftingMaterialsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:183 |
| VillageNeedsToolsIssueBehavior | disabled-by-issue-gate | TaleWorlds.CampaignSystem.SandBoxManager:193 |

## Menu choices

One row per installed `AddGameMenuOption` call from three inspected menu registrars.
Conditions and consequences are named, not assumed successful. Conditional naval/cheat/access paths are included
as declarations; enabled module/license/access state still needs observation.

| Domain | Menu / option | Condition / consequence | Source line |
| --- | --- | --- | --- |
| Encounters | army_encounter.army_attack_army | game_menu_army_attack_on_condition; game_menu_army_attack_on_consequence | 189 |
| Encounters | army_encounter.army_join_army | game_menu_army_join_on_condition; game_menu_army_join_on_consequence | 188 |
| Encounters | army_encounter.army_leave | game_menu_army_leave_on_condition; army_encounter_leave_on_consequence | 190 |
| Encounters | army_encounter.army_talk_to_leader | game_menu_army_talk_to_leader_on_condition; game_menu_army_talk_to_leader_on_consequence | 186 |
| Encounters | army_encounter.army_talk_to_other_members | game_menu_army_talk_to_other_members_on_condition; game_menu_army_talk_to_other_members_on_consequence | 187 |
| Encounters | army_left_settlement_due_to_war_declaration.army_left_settlement_due_to_war_declaration_continue | game_menu_army_left_settlement_due_to_war_on_condition; game_menu_army_left_settlement_due_to_war_on_consequence | 247 |
| Encounters | besiegers_lift_the_blockade.continue | game_menu_try_to_get_away_continue_on_condition; break_in_debrief_continue_on_consequence | 147 |
| Encounters | break_in_debrief_menu.break_in_debrief_continue | continue_on_condition; break_in_debrief_continue_on_consequence | 304 |
| Encounters | break_in_menu.break_in_menu_accept | break_in_menu_accept_on_condition; break_in_menu_accept_on_consequence | 301 |
| Encounters | break_in_menu.break_in_menu_reject | break_in_menu_reject_on_condition; break_in_menu_reject_on_consequence | 302 |
| Encounters | break_out_debrief_menu.break_out_debrief_continue | continue_on_condition; break_out_debrief_continue_on_consequence | 309 |
| Encounters | break_out_menu.break_out_menu_accept | break_out_menu_accept_on_condition; break_out_menu_accept_on_consequence | 306 |
| Encounters | break_out_menu.break_out_menu_reject | break_out_menu_reject_on_condition; break_out_menu_reject_on_consequence | 307 |
| Encounters | castle_enter_bribe.castle_bribe_pay | game_menu_castle_enter_bribe_pay_bribe_on_condition; game_menu_castle_enter_bribe_on_consequence | 261 |
| Encounters | castle_guard.guard_back | game_menu_leave_on_condition; game_menu_town_guard_back_on_consequence | 259 |
| Encounters | castle_guard.request_meeting_commander | game_menu_request_meeting_someone_on_condition; game_menu_request_meeting_someone_on_consequence | 258 |
| Encounters | castle_guard.request_shelter | game_menu_town_guard_request_shelter_on_condition; game_menu_request_entry_to_castle_on_consequence | 257 |
| Encounters | castle_outside.approach_gates | game_menu_castle_outside_approach_gates_on_condition; game_menu_castle_outside_approach_gates_on_consequence | 249 |
| Encounters | castle_outside.town_besiege | game_menu_town_town_besiege_on_condition; game_menu_town_town_besiege_on_consequence | 250 |
| Encounters | castle_outside.town_outside_leave | game_menu_leave_on_condition; game_menu_castle_outside_leave_on_consequence | 251 |
| Encounters | continue_siege_after_attack.continue_siege | continue_siege_after_attack_on_condition; continue_siege_after_attack_on_consequence | 294 |
| Encounters | continue_siege_after_attack.leave_army | leave_army_after_attack_on_condition; leave_army_after_attack_on_consequence | 296 |
| Encounters | continue_siege_after_attack.leave_siege | leave_siege_after_attack_on_condition; leave_siege_after_attack_on_consequence | 295 |
| Encounters | defeated_and_taken_prisoner.taken_prisoner_continue | game_menu_taken_prisoner_continue_on_condition; game_menu_taken_prisoner_continue_on_consequence | 113 |
| Encounters | disguise_first_time.continue | launch_mission_on_condition; launch_disguise_mission | 216 |
| Encounters | disguise_not_first_time.quick_sneak | game_menu_town_disguise_yourself_on_condition; game_menu_town_disguise_yourself_on_consequence | 229 |
| Encounters | disguise_not_first_time.take_a_walk | launch_mission_on_condition; launch_disguise_mission | 230 |
| Encounters | encounter.abandon_army | game_menu_encounter_abandon_army_on_condition; game_menu_encounter_abandon_on_consequence | 183 |
| Encounters | encounter.attack | game_menu_encounter_attack_on_condition; game_menu_encounter_attack_on_consequence | 174 |
| Encounters | encounter.capture_the_enemy | game_menu_encounter_capture_the_enemy_on_condition; game_menu_capture_the_enemy_on_consequence | 175 |
| Encounters | encounter.continue_preparations | game_menu_town_besiege_continue_siege_on_condition; game_menu_town_besiege_continue_siege_on_consequence | 170 |
| Encounters | encounter.go_back_to_settlement | game_menu_sally_out_go_back_to_settlement_on_condition; game_menu_sally_out_go_back_to_settlement_consequence | 184 |
| Encounters | encounter.leave | game_menu_encounter_leave_on_condition; game_menu_encounter_leave_on_consequence | 182 |
| Encounters | encounter.str_order_attack | game_menu_encounter_order_attack_on_condition; game_menu_encounter_order_attack_on_consequence | 176 |
| Encounters | encounter.surrender | game_menu_encounter_surrender_on_condition; game_menu_encounter_surrender_on_consequence | 181 |
| Encounters | encounter.village_force_supplies_action | game_menu_village_hostile_action_on_condition; game_menu_village_force_supplies_no_resist_loot_on_consequence | 173 |
| Encounters | encounter.village_force_volunteer_action | game_menu_village_hostile_action_on_condition; game_menu_village_force_volunteers_no_resist_loot_on_consequence | 172 |
| Encounters | encounter.village_raid_action | game_menu_village_hostile_action_on_condition; game_menu_village_raid_no_resist_on_consequence | 171 |
| Encounters | encounter_interrupted.encounter_interrupted_help_attackers | game_menu_join_encounter_help_attackers_on_condition; game_menu_join_encounter_help_attackers_on_consequence | 284 |
| Encounters | encounter_interrupted.encounter_interrupted_help_defenders | game_menu_join_encounter_help_defenders_on_condition; game_menu_join_encounter_help_defenders_on_consequence | 285 |
| Encounters | encounter_interrupted.leave | game_menu_encounter_interrupted_leave_on_condition; game_menu_encounter_interrupted_leave_on_consequence | 286 |
| Encounters | encounter_interrupted_raid_started.encounter_interrupted_raid_started_leave | game_menu_encounter_interrupted_by_raid_continue_on_condition; game_menu_encounter_interrupted_continue_on_consequence | 292 |
| Encounters | encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_break_out_of_town | game_menu_encounter_interrupted_siege_preparations_break_out_of_town_on_condition; game_menu_encounter_interrupted_break_out_of_town_on_consequence | 289 |
| Encounters | encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_join_defend | game_menu_encounter_interrupted_siege_preparations_join_defend_on_condition; game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence | 288 |
| Encounters | encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_leave_town | game_menu_encounter_interrupted_siege_preparations_leave_town_on_condition; game_menu_encounter_interrupted_leave_on_consequence | 290 |
| Encounters | fortification_crime_rating.fortification_crime_rating_continue | game_menu_fortification_high_crime_rating_continue_on_condition; game_menu_fortification_high_crime_rating_continue_on_consequence | 245 |
| Encounters | game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_back | game_menu_army_talk_to_other_members_back_on_condition; game_menu_army_talk_to_other_members_back_on_consequence | 193 |
| Encounters | game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_item | game_menu_army_talk_to_other_members_item_on_condition; game_menu_army_talk_to_other_members_item_on_consequence | 192 |
| Encounters | join_encounter.join_encounter_abandon | game_menu_join_encounter_abandon_army_on_condition; game_menu_encounter_abandon_on_consequence | 118 |
| Encounters | join_encounter.join_encounter_help_attackers | game_menu_join_encounter_help_attackers_on_condition; game_menu_join_encounter_help_attackers_on_consequence | 116 |
| Encounters | join_encounter.join_encounter_help_defenders | game_menu_join_encounter_help_defenders_on_condition; game_menu_join_encounter_help_defenders_on_consequence | 117 |
| Encounters | join_sally_out.join_siege_event | game_menu_join_sally_out_event_on_condition; game_menu_join_sally_out_on_consequence | 129 |
| Encounters | join_sally_out.join_siege_event_break_in | game_menu_stay_in_settlement_on_condition; game_menu_stay_in_settlement_on_consequence | 130 |
| Encounters | join_siege_event.attack_besiegers | attack_besieger_side_on_condition; game_menu_join_encounter_help_defenders_on_consequence | 154 |
| Encounters | join_siege_event.join_encounter_leave | game_menu_join_encounter_leave_on_condition; break_in_leave_consequence | 156 |
| Encounters | join_siege_event.join_siege_event | game_menu_join_siege_event_on_condition; game_menu_join_siege_event_on_consequence | 153 |
| Encounters | join_siege_event.join_siege_event_break_in | break_in_to_help_defender_side_on_condition; game_menu_join_siege_event_on_defender_side_on_consequence | 155 |
| Encounters | menu_captivity_castle_taken_prisoner.cheat_continue | game_menu_captivity_taken_prisoner_cheat_on_condition; game_menu_captivity_taken_prisoner_cheat_on_consequence | 243 |
| Encounters | menu_captivity_castle_taken_prisoner.mno_sneak_caught_surrender | game_menu_captivity_castle_taken_prisoner_cont_on_condition; game_menu_captivity_castle_taken_prisoner_cont_on_consequence | 242 |
| Encounters | menu_castle_entry_denied.str_continue | null; game_request_entry_to_castle_rejected_continue_on_consequence | 269 |
| Encounters | menu_castle_entry_granted.str_continue | game_request_entry_to_castle_approved_continue_on_condition; game_request_entry_to_castle_approved_continue_on_consequence | 267 |
| Encounters | menu_siege_strategies.menu_siege_strategies_break_out_from_gate | menu_defender_siege_break_out_from_gate_on_condition; menu_defender_siege_break_out_from_gate_on_consequence | 148 |
| Encounters | menu_siege_strategies.menu_siege_strategies_break_out_from_port | menu_defender_siege_break_out_from_port_on_condition; menu_defender_siege_break_out_from_port_on_consequence | 149 |
| Encounters | menu_siege_strategies.menu_siege_strategies_sally_out_from_gate | menu_sally_out_from_gate_on_condition; menu_sally_out_land_on_consequence | 150 |
| Encounters | menu_siege_strategies.menu_siege_strategies_sally_out_from_port | menu_sally_out_from_port_on_condition; menu_sally_out_naval_on_consequence | 151 |
| Encounters | menu_sneak_into_town_caught.mno_sneak_caught_surrender | mno_sneak_caught_surrender_on_condition; mno_sneak_caught_surrender_on_consequence | 240 |
| Encounters | menu_sneak_into_town_succeeded.str_continue | menu_sneak_into_town_succeeded_continue_on_condition; menu_sneak_into_town_succeeded_continue_on_consequence | 238 |
| Encounters | naval_encounter_disengaged.naval_encounter_disengaged_continue | naval_encounter_disengage_condition; naval_encounter_disengaged_continue_on_consequence | 311 |
| Encounters | naval_town_outside.attack_the_blockade | attack_blockade_besieger_side_on_condition; attack_blockade_on_consequence | 132 |
| Encounters | naval_town_outside.join_encounter_leave | game_menu_leave_on_condition; game_menu_town_naval_outside_leave_on_consequence | 134 |
| Encounters | naval_town_outside.join_siege_defender | attack_blockade_besieger_side_break_in_on_condition; game_menu_join_siege_event_on_defender_side_on_consequence | 133 |
| Encounters | raid_interrupted.continue | game_menu_raid_interrupted_continue_on_condition; game_menu_raid_interrupted_continue_on_consequence | 282 |
| Encounters | request_meeting.meeting_castle_leave | game_meeting_castle_leave_on_condition; game_menu_request_meeting_castle_leave_on_consequence | 273 |
| Encounters | request_meeting.meeting_town_leave | game_meeting_town_leave_on_condition; game_menu_request_meeting_town_leave_on_consequence | 272 |
| Encounters | request_meeting.request_meeting_with | game_menu_request_meeting_with_on_condition; game_menu_request_meeting_with_on_consequence | 271 |
| Encounters | request_meeting_with_besiegers.request_meeting_castle_leave | game_meeting_castle_leave_on_condition; game_menu_request_meeting_castle_leave_on_consequence | 277 |
| Encounters | request_meeting_with_besiegers.request_meeting_town_leave | game_meeting_town_leave_on_condition; game_menu_request_meeting_town_leave_on_consequence | 276 |
| Encounters | request_meeting_with_besiegers.request_meeting_with | game_menu_request_meeting_with_besiegers_on_condition; game_menu_request_meeting_with_besiegers_on_consequence | 275 |
| Encounters | settlement_player_run_away_when_disguise.continue_back | menu_sneak_into_town_succeeded_continue_on_condition; escape_continue_on_consequence | 236 |
| Encounters | settlement_player_unconscious_when_disguise_contact_not_set.continue | mno_sneak_caught_surrender_on_condition; game_menu_captivity_castle_taken_prisoner_cont_on_consequence | 227 |
| Encounters | siege_attacker_defeated.siege_attacker_defeated_return_to_settlement | game_menu_siege_attacker_left_return_to_settlement_on_condition; game_menu_siege_attacker_left_return_to_settlement_on_consequence | 164 |
| Encounters | siege_attacker_left.siege_attacker_left_return_to_settlement | game_menu_siege_attacker_left_return_to_settlement_on_condition; game_menu_siege_attacker_left_return_to_settlement_on_consequence | 158 |
| Encounters | taken_prisoner.taken_prisoner_continue | game_menu_taken_prisoner_continue_on_condition; game_menu_taken_prisoner_continue_on_consequence | 111 |
| Encounters | town_caught_by_guards.town_caught_by_guards_criminal_outside_menu_give_yourself_up | outside_menu_criminal_on_condition; caught_outside_menu_criminal_on_consequence | 298 |
| Encounters | town_caught_by_guards.town_caught_by_guards_enemy_outside_menu_give_yourself_up | caught_outside_menu_enemy_on_condition; caught_outside_menu_enemy_on_consequence | 299 |
| Encounters | town_guard.guard_back | game_menu_leave_on_condition; game_menu_town_guard_back_on_consequence | 255 |
| Encounters | town_guard.guard_discuss_criminal_surrender | outside_menu_criminal_on_condition; outside_menu_criminal_on_consequence | 254 |
| Encounters | town_guard.request_meeting_commander | game_menu_request_meeting_someone_on_condition; game_menu_request_meeting_someone_on_consequence | 253 |
| Encounters | town_outside.approach_gates | game_menu_castle_outside_approach_gates_on_condition; game_menu_town_outside_approach_gates_on_consequence | 205 |
| Encounters | town_outside.town_besiege | game_menu_town_town_besiege_on_condition; game_menu_town_town_besiege_on_consequence | 207 |
| Encounters | town_outside.town_disguise_yourself | game_menu_town_disguise_yourself_on_condition; game_menu_town_initial_disguise_yourself_on_consequence | 206 |
| Encounters | town_outside.town_enter_cheat | game_menu_town_outside_cheat_enter_on_condition; game_menu_town_outside_enter_on_consequence | 208 |
| Encounters | town_outside.town_outside_leave | game_menu_leave_on_condition; game_menu_castle_outside_leave_on_consequence | 209 |
| Encounters | try_to_get_away.try_to_get_away_accept | game_menu_try_to_get_away_accept_on_condition; game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence | 195 |
| Encounters | try_to_get_away_debrief.try_to_get_away_continue | game_menu_try_to_get_away_continue_on_condition; game_menu_try_to_get_away_end | 201 |
| Encounters | village_loot_complete.continue | game_menu_village_loot_complete_continue_on_condition; game_menu_village_loot_complete_continue_on_consequence | 280 |
| Settlements | castle.castle_lords_hall | game_menu_castle_go_to_lords_hall_on_condition; game_menu_castle_lordshall_on_consequence | 136 |
| Settlements | castle.castle_lords_hall_cheat | game_menu_castle_go_to_lords_hall_cheat_on_condition; game_menu_lordshall_cheat_on_consequence | 137 |
| Settlements | castle.castle_prison | game_menu_castle_go_to_the_dungeon_on_condition; game_menu_keep_dungeon_on_consequence | 129 |
| Settlements | castle.castle_prison_cheat | game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence | 130 |
| Settlements | castle.castle_return_to_army | game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence | 142 |
| Settlements | castle.leave | game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence | 143 |
| Settlements | castle.leave_troops_to_garrison | game_menu_leave_troops_garrison_on_condition; game_menu_leave_troops_garrison_on_consequece | 134 |
| Settlements | castle.manage_garrison | game_menu_manage_garrison_on_condition; game_menu_manage_garrison_on_consequence | 131 |
| Settlements | castle.manage_production | game_menu_manage_castle_on_condition; null | 132 |
| Settlements | castle.open_stash | game_menu_town_keep_open_stash_on_condition; game_menu_town_keep_open_stash_on_consequence | 133 |
| Settlements | castle.take_a_walk_around_the_castle | game_menu_castle_take_a_walk_on_condition; game_menu_castle_take_a_walk_around_the_castle_on_consequence | 135 |
| Settlements | castle_dungeon.town_prison | game_menu_castle_enter_the_dungeon_on_condition; game_menu_castle_dungeon_on_consequence | 147 |
| Settlements | castle_dungeon.town_prison_cheat | game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence | 148 |
| Settlements | castle_dungeon.town_prison_leave_prisoners | game_menu_castle_leave_prisoners_on_condition; game_menu_castle_leave_prisoners_on_consequence | 145 |
| Settlements | castle_dungeon.town_prison_manage_prisoners | game_menu_castle_manage_prisoners_on_condition; game_menu_castle_manage_prisoners_on_consequence | 146 |
| Settlements | settlement_player_unconscious.continue | continue_on_condition; settlement_player_unconscious_continue_on_consequence | 117 |
| Settlements | town.manage_production | game_menu_town_manage_town_on_condition; null | 50 |
| Settlements | town.manage_production_cheat | game_menu_town_manage_town_cheat_on_condition; null | 51 |
| Settlements | town.recruit_volunteers | game_menu_town_recruit_troops_on_condition; game_menu_recruit_volunteers_on_consequence | 52 |
| Settlements | town.town_keep | game_menu_town_go_to_keep_on_condition; game_menu_town_go_to_keep_on_consequence | 41 |
| Settlements | town.town_leave | game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence | 64 |
| Settlements | town.town_return_to_army | game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence | 63 |
| Settlements | town.town_streets | game_menu_town_town_streets_on_condition; game_menu_town_town_streets_on_consequence | 58 |
| Settlements | town.trade | game_menu_trade_on_condition; game_menu_town_town_market_on_consequence | 53 |
| Settlements | town_arena.town_enter_arena | game_menu_town_enter_the_arena_on_condition; game_menu_town_town_arena_on_consequence | 111 |
| Settlements | town_backstreet.town_tavern | visit_the_tavern_on_condition; game_menu_town_town_tavern_on_consequence | 97 |
| Settlements | town_keep.leave_troops_to_garrison | game_menu_leave_troops_garrison_on_condition; game_menu_leave_troops_garrison_on_consequece | 67 |
| Settlements | town_keep.manage_garrison | game_menu_manage_garrison_on_condition; game_menu_manage_garrison_on_consequence | 68 |
| Settlements | town_keep.open_stash | game_menu_town_keep_open_stash_on_condition; game_menu_town_keep_open_stash_on_consequence | 69 |
| Settlements | town_keep.town_lords_hall | game_menu_town_keep_go_to_lords_hall_on_condition; game_menu_town_lordshall_on_consequence | 70 |
| Settlements | town_keep.town_lords_hall_cheat | game_menu_castle_go_to_lords_hall_cheat_on_condition; game_menu_lordshall_cheat_on_consequence | 71 |
| Settlements | town_keep.town_lords_hall_go_to_dungeon | game_menu_go_dungeon_on_condition; game_menu_go_dungeon_on_consequence | 66 |
| Settlements | town_keep_bribe.town_keep_bribe_pay | game_menu_town_keep_bribe_pay_bribe_on_condition; game_menu_town_keep_bribe_pay_bribe_on_consequence | 86 |
| Settlements | town_keep_dungeon.town_prison | game_menu_castle_enter_the_dungeon_on_condition; game_menu_town_dungeon_on_consequence | 79 |
| Settlements | town_keep_dungeon.town_prison_cheat | game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence | 80 |
| Settlements | town_keep_dungeon.town_prison_leave_prisoners | game_menu_castle_leave_prisoners_on_condition; game_menu_castle_leave_prisoners_on_consequence | 77 |
| Settlements | town_keep_dungeon.town_prison_manage_prisoners | game_menu_castle_manage_prisoners_on_condition; game_menu_castle_manage_prisoners_on_consequence | 78 |
| Settlements | village.disembark | game_menu_village_disembark_on_condition; game_menu_village_disembark_on_consequence | 159 |
| Settlements | village.leave | game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence | 162 |
| Settlements | village.leave_at_sea | game_menu_village_leave_at_sea_on_condition; game_menu_village_set_sail_leave_on_consequence | 161 |
| Settlements | village.leave_set_sail | game_menu_village_set_sail_leave_on_condition; game_menu_village_set_sail_leave_on_consequence | 160 |
| Settlements | village.recruit_volunteers | game_menu_recruit_volunteers_on_condition; game_menu_recruit_volunteers_on_consequence | 154 |
| Settlements | village.trade | game_menu_village_buy_good_on_condition; null | 155 |
| Settlements | village.village_center | game_menu_village_village_center_on_condition; game_menu_village_village_center_on_consequence | 156 |
| Settlements | village.village_return_to_army | game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence | 158 |
| Settlements | village.village_wait | game_menu_wait_here_on_condition; game_menu_wait_village_on_consequence | 157 |
| Settlements | village_looted.disembark | game_menu_village_disembark_on_condition; game_menu_village_disembark_on_consequence | 163 |
| Settlements | village_looted.leave_set_sail | game_menu_village_set_sail_leave_on_condition; game_menu_village_set_sail_leave_on_consequence | 164 |
| Settlements | village_wait_menus.wait_leave | back_on_condition; game_menu_stop_waiting_at_village_on_consequence | 169 |
| Villages | force_supplies_village.force_supplies_village_continue | hostile_action_common_continue_on_condition; village_force_supplies_ended_successfully_on_consequence | 109 |
| Villages | force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_continue | game_menu_force_supplies_village_resist_warn_player_continue_on_condition; game_menu_force_supplies_village_resist_warn_player_continue_on_consequence | 111 |
| Villages | force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_leave | hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence | 112 |
| Villages | force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_continue | game_menu_force_troops_village_resist_warn_player_continue_on_condition; game_menu_force_troops_village_resist_warn_player_continue_on_consequence | 114 |
| Villages | force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_leave | hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence | 115 |
| Villages | force_volunteers_village.force_supplies_village_continue | hostile_action_common_continue_on_condition; village_force_volunteers_ended_successfully_on_consequence | 117 |
| Villages | raid_occupied.raid_occuppied_continue | raid_occupied_on_condition; raid_occupied_on_consequence | 104 |
| Villages | raid_village_no_resist_warn_player.raid_village_warn_continue | game_menu_village_hostile_action_raid_village_warn_continue_on_condition; game_menu_village_hostile_action_raid_village_warn_continue_on_consequence | 106 |
| Villages | raid_village_no_resist_warn_player.raid_village_warn_leave | hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence | 107 |
| Villages | raiding_village.abandon_army | wait_menu_end_raiding_at_army_by_abandoning_on_condition; wait_menu_end_raiding_at_army_by_abandoning_on_consequence | 102 |
| Villages | raiding_village.leave_army | wait_menu_end_raiding_at_army_by_leaving_on_condition; wait_menu_end_raiding_at_army_by_leaving_on_consequence | 101 |
| Villages | raiding_village.raiding_village_end | wait_menu_end_raiding_on_condition; wait_menu_end_raiding_on_consequence | 100 |
| Villages | village.hostile_action | game_menu_village_hostile_action_on_condition; game_menu_village_hostile_action_on_consequence | 93 |
| Villages | village_hostile_action.force_peasants_to_give_supplies | game_menu_village_hostile_action_take_food_on_condition; game_menu_village_hostile_action_take_food_on_consequence | 97 |
| Villages | village_hostile_action.force_peasants_to_give_volunteers | game_menu_village_hostile_action_force_volunteers_condition; game_menu_village_hostile_action_force_volunteers_on_consequence | 96 |
| Villages | village_hostile_action.forget_it | hostile_action_common_back_on_condition; game_menu_village_hostile_action_forget_it_on_consequence | 98 |
| Villages | village_hostile_action.raid_village | game_menu_village_hostile_action_raid_village_on_condition; game_menu_village_hostile_action_raid_village_on_consequence | 95 |
| Villages | village_looted.leave | hostile_action_common_back_on_condition; village_looted_leave_on_consequence | 119 |
| Villages | village_player_raid_ended.continue | hostile_action_common_continue_on_condition; village_player_raid_ended_on_consequence | 121 |
| Villages | village_raid_ended_leaded_by_someone_else.continue | hostile_action_common_continue_on_condition; village_raid_ended_leaded_by_someone_else_on_consequence | 123 |

## Individual perks

Every row is a separately initialized perk. Retain both role-dependent effects and alternative choice.
[Perk effect sheets](perks.md) expand primary/secondary effects, troop masks and located consumer references.
`GetTierCost(n)` is an installed expression, not a copied assertion about the player's current level.
Bonus numbers below are raw declarations: `AddFactor` is not an already measured percentage result.
Zero bonuses can enable a capability and must not be treated as absent effects. Applicable models, troop flags,
hero role, ownership, and actual effect still need inspection and a real action before claiming perk support.

### Athletics

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| AthleticsAGoodDaysRest | tier=GetTierCost(6); alternative=AthleticsWalkItOff; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.1f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2105 |
| AthleticsBraced | tier=GetTierCost(5); alternative=AthleticsSurgingBlow; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.4f,-0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2103 |
| AthleticsDurable | tier=GetTierCost(7); alternative=AthleticsEnergetic; roles=PartyRole.Personal,PartyRole.Governor; bonuses=1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2106 |
| AthleticsEnergetic | tier=GetTierCost(7); alternative=AthleticsDurable; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=-0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2107 |
| AthleticsFormFittingArmor | tier=GetTierCost(2); alternative=AthleticsFury; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.15f,0.04f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2097 |
| AthleticsFury | tier=GetTierCost(2); alternative=AthleticsFormFittingArmor; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2096 |
| AthleticsIgnorePain | tier=GetTierCost(10); alternative=AthleticsSpartan; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2113 |
| AthleticsImposingStature | tier=GetTierCost(3); alternative=AthleticsStamina; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.3f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2098 |
| AthleticsMightyBlow | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.05f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2114 |
| AthleticsMorningExercise | tier=GetTierCost(1); alternative=AthleticsWellBuilt; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.03f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2094 |
| AthleticsPowerful | tier=GetTierCost(4); alternative=AthleticsSprint; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.04f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2101 |
| AthleticsSpartan | tier=GetTierCost(10); alternative=AthleticsIgnorePain; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2112 |
| AthleticsSprint | tier=GetTierCost(4); alternative=AthleticsPowerful; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.03f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2100 |
| AthleticsStamina | tier=GetTierCost(3); alternative=AthleticsImposingStature; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2099 |
| AthleticsSteady | tier=GetTierCost(8); alternative=AthleticsStrong; roles=PartyRole.Personal,PartyRole.Governor; bonuses=1f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2108 |
| AthleticsStrong | tier=GetTierCost(8); alternative=AthleticsSteady; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=1f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2109 |
| AthleticsStrongArms | tier=GetTierCost(9); alternative=AthleticsStrongLegs; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,20f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2111 |
| AthleticsStrongLegs | tier=GetTierCost(9); alternative=AthleticsStrongArms; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.5f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2110 |
| AthleticsSurgingBlow | tier=GetTierCost(5); alternative=AthleticsBraced; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.3f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2102 |
| AthleticsWalkItOff | tier=GetTierCost(6); alternative=AthleticsAGoodDaysRest; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.1f,3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2104 |
| AthleticsWellBuilt | tier=GetTierCost(1); alternative=AthleticsMorningExercise; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,5f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2095 |

### Bow

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| BowBodkin | tier=GetTierCost(2); alternative=BowNockingPoint; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2013 |
| BowBowControl | tier=GetTierCost(1); alternative=BowDeadAim; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.3f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2011 |
| BowBullsEye | tier=GetTierCost(8); alternative=BowRenownedArcher; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.1f,3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2025 |
| BowDeadAim | tier=GetTierCost(1); alternative=BowBowControl; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.3f,20f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2012 |
| BowDeadshot | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2031 |
| BowDeepQuivers | tier=GetTierCost(9); alternative=BowHorseMaster; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=3f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2028 |
| BowDiscipline | tier=GetTierCost(6); alternative=BowHunterClan; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.5f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2021 |
| BowEagleEye | tier=GetTierCost(7); alternative=BowSkirmishPhaseMaster; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2024 |
| BowHorseMaster | tier=GetTierCost(9); alternative=BowDeepQuivers; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0f,30f; increments=EffectIncrementType.Invalid,EffectIncrementType.Add | 2027 |
| BowHunterClan | tier=GetTierCost(6); alternative=BowDiscipline; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.3f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2022 |
| BowMerryMen | tier=GetTierCost(4); alternative=BowMountedArchery; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=5f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2017 |
| BowMountedArchery | tier=GetTierCost(4); alternative=BowMerryMen; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.3f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2018 |
| BowNockingPoint | tier=GetTierCost(2); alternative=BowBodkin; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.5f,0.03f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2014 |
| BowQuickAdjustments | tier=GetTierCost(3); alternative=BowRapidFire; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.5f,-0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2016 |
| BowQuickDraw | tier=GetTierCost(10); alternative=BowRangersSwiftness; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.25f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2029 |
| BowRangersSwiftness | tier=GetTierCost(10); alternative=BowQuickDraw; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0f,0.2f; increments=EffectIncrementType.Invalid,EffectIncrementType.AddFactor | 2030 |
| BowRapidFire | tier=GetTierCost(3); alternative=BowQuickAdjustments; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2015 |
| BowRenownedArcher | tier=GetTierCost(8); alternative=BowBullsEye; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.1f,-0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2026 |
| BowSkirmishPhaseMaster | tier=GetTierCost(7); alternative=BowEagleEye; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.1f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2023 |
| BowStrongBows | tier=GetTierCost(5); alternative=BowTrainer; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.08f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2020 |
| BowTrainer | tier=GetTierCost(5); alternative=BowStrongBows; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=6f,3f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2019 |

### Charm

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| CharmCamaraderie | tier=GetTierCost(10); alternative=null; roles=PartyRole.Personal,PartyRole.ClanLeader; bonuses=2f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2216 |
| CharmEffortForThePeople | tier=GetTierCost(6); alternative=CharmSlickNegotiator; roles=PartyRole.Personal,PartyRole.Personal; bonuses=3f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2208 |
| CharmFirebrand | tier=GetTierCost(5); alternative=CharmFlexibleEthics; roles=PartyRole.ClanLeader,PartyRole.PartyLeader; bonuses=-0.25f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2206 |
| CharmFlexibleEthics | tier=GetTierCost(5); alternative=CharmFirebrand; roles=PartyRole.Personal,PartyRole.Personal; bonuses=-0.3f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2207 |
| CharmForgivableGrievances | tier=GetTierCost(3); alternative=CharmMeaningfulFavors; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2202 |
| CharmGoodNatured | tier=GetTierCost(7); alternative=CharmTribute; roles=PartyRole.Personal,PartyRole.Personal; bonuses=1f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2210 |
| CharmImmortalCharm | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal; bonuses=5f; increments=EffectIncrementType.Add | 2217 |
| CharmInBloom | tier=GetTierCost(4); alternative=CharmYoungAndRespectful; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.2f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2204 |
| CharmMeaningfulFavors | tier=GetTierCost(3); alternative=CharmForgivableGrievances; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.1f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2203 |
| CharmMoralLeader | tier=GetTierCost(8); alternative=CharmNaturalLeader; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2212 |
| CharmNaturalLeader | tier=GetTierCost(8); alternative=CharmMoralLeader; roles=PartyRole.Personal,PartyRole.ClanLeader; bonuses=-1f,0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2213 |
| CharmOratory | tier=GetTierCost(2); alternative=CharmWarlord; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2200 |
| CharmParade | tier=GetTierCost(9); alternative=CharmPublicSpeaker; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2215 |
| CharmPublicSpeaker | tier=GetTierCost(9); alternative=CharmParade; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.3f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2214 |
| CharmSelfPromoter | tier=GetTierCost(1); alternative=CharmVirile; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=3f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2199 |
| CharmSlickNegotiator | tier=GetTierCost(6); alternative=CharmEffortForThePeople; roles=PartyRole.Personal,PartyRole.Personal; bonuses=-0.2f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2209 |
| CharmTribute | tier=GetTierCost(7); alternative=CharmGoodNatured; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.2f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2211 |
| CharmVirile | tier=GetTierCost(1); alternative=CharmSelfPromoter; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.3f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2198 |
| CharmWarlord | tier=GetTierCost(2); alternative=CharmOratory; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.3f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2201 |
| CharmYoungAndRespectful | tier=GetTierCost(4); alternative=CharmInBloom; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.2f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2205 |

### Crafting

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| ArtisanSmith | tier=GetTierCost(7); alternative=PracticalSmith; roles=PartyRole.PartyLeader; bonuses=-0.5f; increments=EffectIncrementType.AddFactor | 2128 |
| CharcoalYield | tier=GetTierCost(1); alternative=IronYield; roles=PartyRole.Personal; bonuses=0f; increments=EffectIncrementType.Add | 2116 |
| CraftingSharpenedEdge | tier=GetTierCost(10); alternative=CraftingSharpenedTip; roles=PartyRole.Personal; bonuses=0.02f; increments=EffectIncrementType.AddFactor | 2132 |
| CraftingSharpenedTip | tier=GetTierCost(10); alternative=CraftingSharpenedEdge; roles=PartyRole.Personal; bonuses=0.02f; increments=EffectIncrementType.AddFactor | 2133 |
| CuriousSmelter | tier=GetTierCost(2); alternative=SteelMaker; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.AddFactor | 2118 |
| CuriousSmith | tier=GetTierCost(3); alternative=SteelMaker2; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.AddFactor | 2120 |
| EnduringSmith | tier=GetTierCost(9); alternative=WeaponMasterSmith; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.Add | 2131 |
| ExperiencedSmith | tier=GetTierCost(4); alternative=SteelMaker3; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.1f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2121 |
| IronYield | tier=GetTierCost(1); alternative=CharcoalYield; roles=PartyRole.Personal; bonuses=0f; increments=EffectIncrementType.Add | 2115 |
| LegendarySmith | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal; bonuses=0.05f; increments=EffectIncrementType.AddFactor | 2134 |
| MasterSmith | tier=GetTierCost(8); alternative=null; roles=PartyRole.Personal; bonuses=0.075f; increments=EffectIncrementType.AddFactor | 2129 |
| PracticalRefiner | tier=GetTierCost(5); alternative=PracticalSmelter; roles=PartyRole.Personal; bonuses=-0.5f; increments=EffectIncrementType.AddFactor | 2123 |
| PracticalSmelter | tier=GetTierCost(5); alternative=PracticalRefiner; roles=PartyRole.Personal; bonuses=-0.5f; increments=EffectIncrementType.AddFactor | 2124 |
| PracticalSmith | tier=GetTierCost(7); alternative=ArtisanSmith; roles=PartyRole.Personal; bonuses=-0.5f; increments=EffectIncrementType.AddFactor | 2127 |
| SteelMaker | tier=GetTierCost(2); alternative=CuriousSmelter; roles=PartyRole.Personal; bonuses=0f; increments=EffectIncrementType.Add | 2117 |
| SteelMaker2 | tier=GetTierCost(3); alternative=CuriousSmith; roles=PartyRole.Personal; bonuses=0f; increments=EffectIncrementType.Add | 2119 |
| SteelMaker3 | tier=GetTierCost(4); alternative=ExperiencedSmith; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0f,4f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2122 |
| StrongSmith | tier=GetTierCost(6); alternative=VigorousSmith; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.Add | 2126 |
| VigorousSmith | tier=GetTierCost(6); alternative=StrongSmith; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.Add | 2125 |
| WeaponMasterSmith | tier=GetTierCost(9); alternative=EnduringSmith; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.Add | 2130 |

### Crossbow

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| CrossbowBoltenGuard | tier=GetTierCost(10); alternative=CrossbowTerror; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=-0.5f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2051 |
| CrossbowCounterFire | tier=GetTierCost(7); alternative=CrossbowMountedCrossbowman; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.1f,-0.03f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2044 |
| CrossbowDeftHands | tier=GetTierCost(6); alternative=CrossbowLooseAndMove; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2043 |
| CrossbowDonkeysSwiftness | tier=GetTierCost(3); alternative=CrossbowSheriff; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.3f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2036 |
| CrossbowFletcher | tier=GetTierCost(5); alternative=CrossbowPuncture; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=4f,2f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2040 |
| CrossbowHammerBolts | tier=GetTierCost(9); alternative=CrossbowPavise; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2048 |
| CrossbowLongShots | tier=GetTierCost(8); alternative=CrossbowSteady; roles=PartyRole.Personal,PartyRole.Governor; bonuses=1f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2047 |
| CrossbowLooseAndMove | tier=GetTierCost(6); alternative=CrossbowDeftHands; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2042 |
| CrossbowMarksmen | tier=GetTierCost(1); alternative=CrossbowPiercer; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.25f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2033 |
| CrossbowMightyPull | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2052 |
| CrossbowMountedCrossbowman | tier=GetTierCost(7); alternative=CrossbowCounterFire; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2045 |
| CrossbowPavise | tier=GetTierCost(9); alternative=CrossbowHammerBolts; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.75f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2049 |
| CrossbowPeasantLeader | tier=GetTierCost(4); alternative=CrossbowRenownMarksmen; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.1f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2038 |
| CrossbowPiercer | tier=GetTierCost(1); alternative=CrossbowMarksmen; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=20f,-0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2032 |
| CrossbowPuncture | tier=GetTierCost(5); alternative=CrossbowFletcher; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2041 |
| CrossbowRenownMarksmen | tier=GetTierCost(4); alternative=CrossbowPeasantLeader; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=2f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2039 |
| CrossbowSheriff | tier=GetTierCost(3); alternative=CrossbowDonkeysSwiftness; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2037 |
| CrossbowSteady | tier=GetTierCost(8); alternative=CrossbowLongShots; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.5f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2046 |
| CrossbowTerror | tier=GetTierCost(10); alternative=CrossbowBoltenGuard; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.2f,0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2050 |
| CrossbowUnhorser | tier=GetTierCost(2); alternative=CrossbowWindWinder; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.4f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2034 |
| CrossbowWindWinder | tier=GetTierCost(2); alternative=CrossbowUnhorser; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2035 |

### Engineering

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| EngineeringApprenticeship | tier=GetTierCost(8); alternative=EngineeringEngineeringGuilds; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=5f,0.01f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2319 |
| EngineeringArchitecturalCommissions | tier=GetTierCost(10); alternative=EngineeringClockwork; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.25f,20f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2323 |
| EngineeringBattlements | tier=GetTierCost(7); alternative=EngineeringCampBuilding; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=1f,100f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2317 |
| EngineeringCampBuilding | tier=GetTierCost(7); alternative=EngineeringBattlements; roles=PartyRole.ArmyCommander,PartyRole.Engineer; bonuses=-0.5f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2316 |
| EngineeringCarpenters | tier=GetTierCost(3); alternative=EngineeringMilitaryPlanner; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.33f,0.12f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2308 |
| EngineeringClockwork | tier=GetTierCost(10); alternative=EngineeringArchitecturalCommissions; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.25f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2322 |
| EngineeringDreadfulSieger | tier=GetTierCost(4); alternative=EngineeringWallBreaker; roles=PartyRole.Governor,PartyRole.Captain; bonuses=0.1f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2311 |
| EngineeringDungeonArchitect | tier=GetTierCost(2); alternative=EngineeringSiegeWorks; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=-0.25f,-0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2307 |
| EngineeringEngineeringGuilds | tier=GetTierCost(8); alternative=EngineeringApprenticeship; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=1f,0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2318 |
| EngineeringForeman | tier=GetTierCost(5); alternative=EngineeringSalvager; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.1f,100f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2313 |
| EngineeringImprovedTools | tier=GetTierCost(9); alternative=EngineeringMetallurgy; roles=PartyRole.Engineer,PartyRole.Captain; bonuses=0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2321 |
| EngineeringMasterwork | tier=GetTierCost(11); alternative=null; roles=PartyRole.Engineer; bonuses=0.01f; increments=EffectIncrementType.AddFactor | 2324 |
| EngineeringMetallurgy | tier=GetTierCost(9); alternative=EngineeringImprovedTools; roles=PartyRole.Engineer,PartyRole.Captain; bonuses=0.3f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2320 |
| EngineeringMilitaryPlanner | tier=GetTierCost(3); alternative=EngineeringCarpenters; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.5f,0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2309 |
| EngineeringSalvager | tier=GetTierCost(5); alternative=EngineeringForeman; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.2f,0.001f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2312 |
| EngineeringScaffolds | tier=GetTierCost(1); alternative=EngineeringTorsionEngines; roles=PartyRole.Engineer,PartyRole.Personal; bonuses=0.1f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2304 |
| EngineeringSiegeEngineer | tier=GetTierCost(6); alternative=EngineeringStonecutters; roles=PartyRole.Governor,PartyRole.Engineer; bonuses=0.3f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2315 |
| EngineeringSiegeWorks | tier=GetTierCost(2); alternative=EngineeringDungeonArchitect; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.1f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2306 |
| EngineeringStonecutters | tier=GetTierCost(6); alternative=EngineeringSiegeEngineer; roles=PartyRole.Governor,PartyRole.Engineer; bonuses=0.3f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2314 |
| EngineeringTorsionEngines | tier=GetTierCost(1); alternative=EngineeringScaffolds; roles=PartyRole.Engineer,PartyRole.Personal; bonuses=0.1f,3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2305 |
| EngineeringWallBreaker | tier=GetTierCost(4); alternative=EngineeringDreadfulSieger; roles=PartyRole.Engineer,PartyRole.Captain; bonuses=0.25f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2310 |

### Leadership

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| LeadershipAuthority | tier=GetTierCost(3); alternative=LeadershipHeroicLeader; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=0.2f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2222 |
| LeadershipCitizenMilitia | tier=GetTierCost(6); alternative=LeadershipVeteransRespect; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2229 |
| LeadershipCombatTips | tier=GetTierCost(1); alternative=LeadershipRaiseTheMeek; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=2f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2218 |
| LeadershipFamousCommander | tier=GetTierCost(4); alternative=LeadershipLoyaltyAndHonor; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.5f,200f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2225 |
| LeadershipFerventAttacker | tier=GetTierCost(2); alternative=LeadershipStoutDefender; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=4f,0.5f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2220 |
| LeadershipGreatLeader | tier=GetTierCost(9); alternative=LeadershipMakeADifference; roles=PartyRole.ArmyCommander,PartyRole.PartyLeader; bonuses=5f,5f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2235 |
| LeadershipHeroicLeader | tier=GetTierCost(3); alternative=LeadershipAuthority; roles=PartyRole.Governor,PartyRole.Captain; bonuses=1f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2223 |
| LeadershipInspiringLeader | tier=GetTierCost(7); alternative=LeadershipUpliftingSpirit; roles=PartyRole.ArmyCommander,PartyRole.Captain; bonuses=-0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2230 |
| LeadershipLeadByExample | tier=GetTierCost(8); alternative=LeadershipTrustedCommander; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2233 |
| LeadershipLeaderOfMasses | tier=GetTierCost(5); alternative=LeadershipPresence; roles=PartyRole.ClanLeader,PartyRole.PartyLeader; bonuses=5f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2227 |
| LeadershipLoyaltyAndHonor | tier=GetTierCost(4); alternative=LeadershipFamousCommander; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=3f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2224 |
| LeadershipMakeADifference | tier=GetTierCost(9); alternative=LeadershipGreatLeader; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=1f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2234 |
| LeadershipPresence | tier=GetTierCost(5); alternative=LeadershipLeaderOfMasses; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,0f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2226 |
| LeadershipRaiseTheMeek | tier=GetTierCost(1); alternative=LeadershipCombatTips; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=4f,3f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2219 |
| LeadershipStoutDefender | tier=GetTierCost(2); alternative=LeadershipFerventAttacker; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=8f,0.5f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2221 |
| LeadershipTalentMagnet | tier=GetTierCost(10); alternative=LeadershipWePledgeOurSwords; roles=PartyRole.PartyLeader,PartyRole.ClanLeader; bonuses=10f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2237 |
| LeadershipTrustedCommander | tier=GetTierCost(8); alternative=LeadershipLeadByExample; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.5f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2232 |
| LeadershipUltimateLeader | tier=GetTierCost(11); alternative=null; roles=PartyRole.PartyLeader; bonuses=1f; increments=EffectIncrementType.Add | 2238 |
| LeadershipUpliftingSpirit | tier=GetTierCost(7); alternative=LeadershipInspiringLeader; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=10f,10f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2231 |
| LeadershipVeteransRespect | tier=GetTierCost(6); alternative=LeadershipCitizenMilitia; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=20f,0f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2228 |
| LeadershipWePledgeOurSwords | tier=GetTierCost(10); alternative=LeadershipTalentMagnet; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2236 |

### Medicine

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| MedicineBattleHardened | tier=GetTierCost(10); alternative=MedicineHelpingHands; roles=PartyRole.Surgeon,PartyRole.Governor; bonuses=25f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2302 |
| MedicineBestMedicine | tier=GetTierCost(4); alternative=MedicineGoodLodging; roles=PartyRole.Surgeon,PartyRole.Personal; bonuses=0.15f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2289 |
| MedicineBushDoctor | tier=GetTierCost(6); alternative=MedicinePristineStreets; roles=PartyRole.Governor,PartyRole.Surgeon; bonuses=0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2294 |
| MedicineCheatDeath | tier=GetTierCost(9); alternative=MedicineFortitudeTonic; roles=PartyRole.Personal,PartyRole.Surgeon; bonuses=0f,-0.5f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2299 |
| MedicineCleanInfrastructure | tier=GetTierCost(8); alternative=MedicinePhysicianOfPeople; roles=PartyRole.Governor,PartyRole.Governor; bonuses=1f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2298 |
| MedicineDoctorsOath | tier=GetTierCost(3); alternative=MedicineSledges; roles=PartyRole.Surgeon,PartyRole.Personal; bonuses=0f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2288 |
| MedicineFortitudeTonic | tier=GetTierCost(9); alternative=MedicineCheatDeath; roles=PartyRole.PartyLeader,PartyRole.Personal; bonuses=10f,5f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2300 |
| MedicineGoodLodging | tier=GetTierCost(4); alternative=MedicineBestMedicine; roles=PartyRole.Surgeon,PartyRole.Personal; bonuses=0.2f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2290 |
| MedicineHealthAdvise | tier=GetTierCost(7); alternative=MedicinePerfectHealth; roles=PartyRole.ClanLeader,PartyRole.Surgeon; bonuses=0f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2296 |
| MedicineHelpingHands | tier=GetTierCost(10); alternative=MedicineBattleHardened; roles=PartyRole.Surgeon,PartyRole.Governor; bonuses=0.02f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2301 |
| MedicineMinisterOfHealth | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal; bonuses=1f; increments=EffectIncrementType.Add | 2303 |
| MedicinePerfectHealth | tier=GetTierCost(7); alternative=MedicineHealthAdvise; roles=PartyRole.Surgeon,PartyRole.Governor; bonuses=0.05f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2295 |
| MedicinePhysicianOfPeople | tier=GetTierCost(8); alternative=MedicineCleanInfrastructure; roles=PartyRole.Governor,PartyRole.Surgeon; bonuses=1f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2297 |
| MedicinePreventiveMedicine | tier=GetTierCost(1); alternative=MedicineSelfMedication; roles=PartyRole.Personal,PartyRole.Personal; bonuses=5f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2284 |
| MedicinePristineStreets | tier=GetTierCost(6); alternative=MedicineBushDoctor; roles=PartyRole.Governor,PartyRole.Surgeon; bonuses=1f,0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2293 |
| MedicineSelfMedication | tier=GetTierCost(1); alternative=MedicinePreventiveMedicine; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.3f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2283 |
| MedicineSiegeMedic | tier=GetTierCost(5); alternative=MedicineVeterinarian; roles=PartyRole.Surgeon,PartyRole.Surgeon; bonuses=0.5f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2291 |
| MedicineSledges | tier=GetTierCost(3); alternative=MedicineDoctorsOath; roles=PartyRole.Surgeon,PartyRole.PartyLeader; bonuses=-0.5f,15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2287 |
| MedicineTriageTent | tier=GetTierCost(2); alternative=MedicineWalkItOff; roles=PartyRole.Surgeon,PartyRole.Governor; bonuses=0.3f,-0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2285 |
| MedicineVeterinarian | tier=GetTierCost(5); alternative=MedicineSiegeMedic; roles=PartyRole.Surgeon,PartyRole.Surgeon; bonuses=0.3f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2292 |
| MedicineWalkItOff | tier=GetTierCost(2); alternative=MedicineTriageTent; roles=PartyRole.Surgeon,PartyRole.Personal; bonuses=0.15f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2286 |

### OneHanded

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| OneHandedArrowCatcher | tier=GetTierCost(5); alternative=OneHandedShieldWall; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.01f,0.01f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 1960 |
| OneHandedBasher | tier=GetTierCost(1); alternative=OneHandedWrappedHandles; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,-0.04f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1952 |
| OneHandedCavalry | tier=GetTierCost(3); alternative=OneHandedShieldBearer; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1955 |
| OneHandedChinkInTheArmor | tier=GetTierCost(10); alternative=OneHandedPrestige; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.1f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1970 |
| OneHandedCorpsACorps | tier=GetTierCost(6); alternative=OneHandedMilitaryTradition; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.1f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1962 |
| OneHandedDeadlyPurpose | tier=GetTierCost(9); alternative=OneHandedUnwaveringDefense; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1967 |
| OneHandedDuelist | tier=GetTierCost(4); alternative=OneHandedTrainer; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.2f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1958 |
| OneHandedFleetOfFoot | tier=GetTierCost(8); alternative=OneHandedSteelCoreShields; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.04f,0.04f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1966 |
| OneHandedLeadByExample | tier=GetTierCost(7); alternative=OneHandedStandUnited; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.05f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1964 |
| OneHandedMilitaryTradition | tier=GetTierCost(6); alternative=OneHandedCorpsACorps; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=2f,-0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 1961 |
| OneHandedPrestige | tier=GetTierCost(10); alternative=OneHandedChinkInTheArmor; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1969 |
| OneHandedShieldBearer | tier=GetTierCost(3); alternative=OneHandedCavalry; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0f,0.03f; increments=EffectIncrementType.Invalid,EffectIncrementType.AddFactor | 1956 |
| OneHandedShieldWall | tier=GetTierCost(5); alternative=OneHandedArrowCatcher; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.2f,0.01f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1959 |
| OneHandedStandUnited | tier=GetTierCost(7); alternative=OneHandedLeadByExample; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=8f,0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 1963 |
| OneHandedSteelCoreShields | tier=GetTierCost(8); alternative=OneHandedFleetOfFoot; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.1f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1965 |
| OneHandedSwiftStrike | tier=GetTierCost(2); alternative=OneHandedToBeBlunt; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.02f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1954 |
| OneHandedToBeBlunt | tier=GetTierCost(2); alternative=OneHandedSwiftStrike; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.05f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1953 |
| OneHandedTrainer | tier=GetTierCost(4); alternative=OneHandedDuelist; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=2f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 1957 |
| OneHandedUnwaveringDefense | tier=GetTierCost(9); alternative=OneHandedDeadlyPurpose; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,10f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 1968 |
| OneHandedWayOfTheSword | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1971 |
| OneHandedWrappedHandles | tier=GetTierCost(1); alternative=OneHandedBasher; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1951 |

### Polearm

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| PolearmBraced | tier=GetTierCost(2); alternative=PolearmKeepAtBay; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1992 |
| PolearmCavalry | tier=GetTierCost(1); alternative=PolearmPikeman; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.02f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1991 |
| PolearmCleanThrust | tier=GetTierCost(3); alternative=PolearmSwiftSwing; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1995 |
| PolearmCounterweight | tier=GetTierCost(10); alternative=PolearmSharpenTheTip; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.15f,20f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2008 |
| PolearmDrills | tier=GetTierCost(8); alternative=PolearmHardyFrontline; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=1f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2005 |
| PolearmFootwork | tier=GetTierCost(4); alternative=PolearmHardKnock; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.02f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1996 |
| PolearmGuards | tier=GetTierCost(6); alternative=PolearmSkewer; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.5f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2001 |
| PolearmHardKnock | tier=GetTierCost(4); alternative=PolearmFootwork; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.25f,3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1997 |
| PolearmHardyFrontline | tier=GetTierCost(8); alternative=PolearmDrills; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=5f,-0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2004 |
| PolearmKeepAtBay | tier=GetTierCost(2); alternative=PolearmBraced; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.3f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1993 |
| PolearmLancer | tier=GetTierCost(5); alternative=PolearmSteadKiller; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1999 |
| PolearmPhalanx | tier=GetTierCost(7); alternative=PolearmStandardBearer; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=30f,0.03f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2003 |
| PolearmPikeman | tier=GetTierCost(1); alternative=PolearmCavalry; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.02f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1990 |
| PolearmSharpenTheTip | tier=GetTierCost(10); alternative=PolearmCounterweight; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2009 |
| PolearmSkewer | tier=GetTierCost(6); alternative=PolearmGuards; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.3f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2000 |
| PolearmStandardBearer | tier=GetTierCost(7); alternative=PolearmPhalanx; roles=PartyRole.Captain,PartyRole.Governor; bonuses=-0.2f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2002 |
| PolearmSteadKiller | tier=GetTierCost(5); alternative=PolearmLancer; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.7f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1998 |
| PolearmSureFooted | tier=GetTierCost(9); alternative=PolearmUnstoppableForce; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.4f,-0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2006 |
| PolearmSwiftSwing | tier=GetTierCost(3); alternative=PolearmCleanThrust; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1994 |
| PolearmUnstoppableForce | tier=GetTierCost(9); alternative=PolearmSureFooted; roles=PartyRole.Personal,PartyRole.Captain; bonuses=3f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2007 |
| PolearmWayOfTheSpear | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2010 |

### Riding

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| RidingAnnoyingBuzz | tier=GetTierCost(8); alternative=RidingThunderousCharge; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2088 |
| RidingBreeder | tier=GetTierCost(7); alternative=RidingShepherd; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.01f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2086 |
| RidingCavalryTactics | tier=GetTierCost(9); alternative=RidingMountedPatrols; roles=PartyRole.ClanLeader,PartyRole.Governor; bonuses=0.3f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2090 |
| RidingDauntlessSteed | tier=GetTierCost(10); alternative=RidingToughSteed; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2091 |
| RidingDeeperSacks | tier=GetTierCost(3); alternative=RidingNomadicTraditions; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.2f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2079 |
| RidingFullSpeed | tier=GetTierCost(1); alternative=RidingNimbleStead; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2074 |
| RidingHorseArcher | tier=GetTierCost(6); alternative=RidingMountedWarrior; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2084 |
| RidingMountedPatrols | tier=GetTierCost(9); alternative=RidingCavalryTactics; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=-0.5f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2089 |
| RidingMountedWarrior | tier=GetTierCost(6); alternative=RidingHorseArcher; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.05f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2083 |
| RidingNimbleStead | tier=GetTierCost(1); alternative=RidingFullSpeed; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2075 |
| RidingNomadicTraditions | tier=GetTierCost(3); alternative=RidingDeeperSacks; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.3f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2078 |
| RidingReliefForce | tier=GetTierCost(5); alternative=null; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=10f,0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2082 |
| RidingSagittarius | tier=GetTierCost(4); alternative=RidingSweepingWind; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.15f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2080 |
| RidingShepherd | tier=GetTierCost(7); alternative=RidingBreeder; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=-0.5f,0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2085 |
| RidingSweepingWind | tier=GetTierCost(4); alternative=RidingSagittarius; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.05f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2081 |
| RidingTheWayOfTheSaddle | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal; bonuses=0.3f; increments=EffectIncrementType.Add | 2093 |
| RidingThunderousCharge | tier=GetTierCost(8); alternative=RidingAnnoyingBuzz; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2087 |
| RidingToughSteed | tier=GetTierCost(10); alternative=RidingDauntlessSteed; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2092 |
| RidingVeterinary | tier=GetTierCost(2); alternative=RidingWellStraped; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2077 |
| RidingWellStraped | tier=GetTierCost(2); alternative=RidingVeterinary; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.5f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2076 |

### Roguery

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| RogueryArmsDealer | tier=GetTierCost(9); alternative=RogueryDirtyFighting; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=-0.2f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2193 |
| RogueryCarver | tier=GetTierCost(8); alternative=RogueryRansomBroker; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2191 |
| RogueryDashAndSlash | tier=GetTierCost(10); alternative=RogueryFleetFooted; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2195 |
| RogueryDeepPockets | tier=GetTierCost(2); alternative=RogueryTwoFaced; roles=PartyRole.Personal,PartyRole.Personal; bonuses=2f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2180 |
| RogueryDirtyFighting | tier=GetTierCost(9); alternative=RogueryArmsDealer; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.5f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2194 |
| RogueryFleetFooted | tier=GetTierCost(10); alternative=RogueryDashAndSlash; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.1f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2196 |
| RogueryInBestLight | tier=GetTierCost(3); alternative=RogueryKnowHow; roles=PartyRole.PartyLeader,PartyRole.ClanLeader; bonuses=1f,0.2f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2181 |
| RogueryKnowHow | tier=GetTierCost(3); alternative=RogueryInBestLight; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.05f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2182 |
| RogueryManhunter | tier=GetTierCost(4); alternative=RogueryPromises; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.2f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2184 |
| RogueryNoRestForTheWicked | tier=GetTierCost(1); alternative=RoguerySweetTalker; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2177 |
| RogueryOneOfTheFamily | tier=GetTierCost(7); alternative=RoguerySaltTheEarth; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=10f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2189 |
| RogueryPartnersInCrime | tier=GetTierCost(6); alternative=RoguerySmugglerConnections; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2188 |
| RogueryPromises | tier=GetTierCost(4); alternative=RogueryManhunter; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=-0.5f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2183 |
| RogueryRansomBroker | tier=GetTierCost(8); alternative=RogueryCarver; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.25f,-0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2192 |
| RogueryRogueExtraordinaire | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal; bonuses=0.01f; increments=EffectIncrementType.AddFactor | 2197 |
| RoguerySaltTheEarth | tier=GetTierCost(7); alternative=RogueryOneOfTheFamily; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.2f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2190 |
| RogueryScarface | tier=GetTierCost(5); alternative=RogueryWhiteLies; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.3f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2185 |
| RoguerySmugglerConnections | tier=GetTierCost(6); alternative=RogueryPartnersInCrime; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0f,-0.5f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2187 |
| RoguerySweetTalker | tier=GetTierCost(1); alternative=RogueryNoRestForTheWicked; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=0.2f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2178 |
| RogueryTwoFaced | tier=GetTierCost(2); alternative=RogueryDeepPockets; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2179 |
| RogueryWhiteLies | tier=GetTierCost(5); alternative=RogueryScarface; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.2f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2186 |

### Scouting

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| ScoutingBeastWhisperer | tier=GetTierCost(7); alternative=ScoutingForagers; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.05f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2148 |
| ScoutingDayTraveler | tier=GetTierCost(1); alternative=ScoutingNightRunner; roles=PartyRole.Scout,PartyRole.Scout; bonuses=0.02f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2135 |
| ScoutingDesertBorn | tier=GetTierCost(3); alternative=ScoutingForestKin; roles=PartyRole.Scout,PartyRole.Governor; bonuses=0.05f,0.025f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2140 |
| ScoutingForagers | tier=GetTierCost(7); alternative=ScoutingBeastWhisperer; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=-0.1f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2147 |
| ScoutingForcedMarch | tier=GetTierCost(4); alternative=ScoutingUnburdened; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.025f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2141 |
| ScoutingForestKin | tier=GetTierCost(3); alternative=ScoutingDesertBorn; roles=PartyRole.Scout,PartyRole.Governor; bonuses=-0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2139 |
| ScoutingKeenSight | tier=GetTierCost(9); alternative=ScoutingVantagePoint; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=-0.5f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2152 |
| ScoutingMountedScouts | tier=GetTierCost(6); alternative=ScoutingPatrols; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.1f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2145 |
| ScoutingNightRunner | tier=GetTierCost(1); alternative=ScoutingDayTraveler; roles=PartyRole.Scout,PartyRole.Scout; bonuses=0.05f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2136 |
| ScoutingPathfinder | tier=GetTierCost(2); alternative=ScoutingWaterDiviner; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.02f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2137 |
| ScoutingPatrols | tier=GetTierCost(6); alternative=ScoutingMountedScouts; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=5f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2146 |
| ScoutingRanger | tier=GetTierCost(5); alternative=ScoutingTracker; roles=PartyRole.Scout,PartyRole.Scout; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2144 |
| ScoutingRearguard | tier=GetTierCost(10); alternative=ScoutingVanguard; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2154 |
| ScoutingRumourNetwork | tier=GetTierCost(8); alternative=ScoutingVillageNetwork; roles=PartyRole.PartyLeader,PartyRole.Scout; bonuses=-0.05f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2150 |
| ScoutingTracker | tier=GetTierCost(5); alternative=ScoutingRanger; roles=PartyRole.Scout,PartyRole.Scout; bonuses=0.2f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2143 |
| ScoutingUnburdened | tier=GetTierCost(4); alternative=ScoutingForcedMarch; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=-0.2f,2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2142 |
| ScoutingUncannyInsight | tier=GetTierCost(11); alternative=null; roles=PartyRole.Scout; bonuses=0.001f; increments=EffectIncrementType.AddFactor | 2155 |
| ScoutingVanguard | tier=GetTierCost(10); alternative=ScoutingRearguard; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.05f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2153 |
| ScoutingVantagePoint | tier=GetTierCost(9); alternative=ScoutingKeenSight; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.25f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2151 |
| ScoutingVillageNetwork | tier=GetTierCost(8); alternative=ScoutingRumourNetwork; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=-0.1f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2149 |
| ScoutingWaterDiviner | tier=GetTierCost(2); alternative=ScoutingPathfinder; roles=PartyRole.Scout,PartyRole.PartyLeader; bonuses=0.1f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2138 |

### Steward

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| StewardAidCorps | tier=GetTierCost(6); alternative=StewardRelocation; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=0f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2273 |
| StewardArenicosHorses | tier=GetTierCost(9); alternative=StewardArenicosMules; roles=PartyRole.Quartermaster,PartyRole.Personal; bonuses=0.1f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2279 |
| StewardArenicosMules | tier=GetTierCost(9); alternative=StewardArenicosHorses; roles=PartyRole.Quartermaster,PartyRole.Quartermaster; bonuses=0.2f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2278 |
| StewardContractors | tier=GetTierCost(8); alternative=StewardForcedLabor; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=-0.25f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2277 |
| StewardDrillSergant | tier=GetTierCost(2); alternative=StewardSevenVeterans; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=2f,-0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2265 |
| StewardEfficientCampaigner | tier=GetTierCost(4); alternative=StewardPaidInPromise; roles=PartyRole.PartyLeader,PartyRole.Quartermaster; bonuses=1f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2269 |
| StewardForcedLabor | tier=GetTierCost(8); alternative=StewardContractors; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=0f,0.01f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2276 |
| StewardForeseeableFuture | tier=GetTierCost(5); alternative=StewardLogistician; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=0f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2270 |
| StewardFrugal | tier=GetTierCost(1); alternative=StewardWarriorsDiet; roles=PartyRole.Quartermaster,PartyRole.PartyLeader; bonuses=-0.05f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2263 |
| StewardGourmet | tier=GetTierCost(7); alternative=StewardSoundReserves; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=1f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2274 |
| StewardLogistician | tier=GetTierCost(5); alternative=StewardForeseeableFuture; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=4f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2271 |
| StewardMasterOfPlanning | tier=GetTierCost(10); alternative=StewardMasterOfWarcraft; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=-0.4f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2280 |
| StewardMasterOfWarcraft | tier=GetTierCost(10); alternative=StewardMasterOfPlanning; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=-0.25f,-0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2281 |
| StewardPaidInPromise | tier=GetTierCost(4); alternative=StewardEfficientCampaigner; roles=PartyRole.PartyLeader,PartyRole.Quartermaster; bonuses=-0.25f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2268 |
| StewardPriceOfLoyalty | tier=GetTierCost(11); alternative=null; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=-0.005f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2282 |
| StewardRelocation | tier=GetTierCost(6); alternative=StewardAidCorps; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=0.25f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2272 |
| StewardSevenVeterans | tier=GetTierCost(2); alternative=StewardDrillSergant; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=4f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2264 |
| StewardSoundReserves | tier=GetTierCost(7); alternative=StewardGourmet; roles=PartyRole.Quartermaster,PartyRole.Quartermaster; bonuses=-0.1f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2275 |
| StewardStiffUpperLip | tier=GetTierCost(3); alternative=StewardSweatshops; roles=PartyRole.Quartermaster,PartyRole.Governor; bonuses=-0.1f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2267 |
| StewardSweatshops | tier=GetTierCost(3); alternative=StewardStiffUpperLip; roles=PartyRole.Personal,PartyRole.Quartermaster; bonuses=0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2266 |
| StewardWarriorsDiet | tier=GetTierCost(1); alternative=StewardFrugal; roles=PartyRole.Quartermaster,PartyRole.PartyLeader; bonuses=-0.1f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2262 |

### Tactics

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| TacticsBesieged | tier=GetTierCost(9); alternative=TacticsPreBattleManeuvers; roles=PartyRole.PartyMember,PartyRole.Personal; bonuses=0.1f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2173 |
| TacticsCallToArms | tier=GetTierCost(6); alternative=TacticsOnTheMarch; roles=PartyRole.ArmyCommander,PartyRole.ArmyCommander; bonuses=0.1f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2167 |
| TacticsCoaching | tier=GetTierCost(4); alternative=TacticsLawkeeper; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.03f,0.01f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2163 |
| TacticsCounteroffensive | tier=GetTierCost(10); alternative=TacticsGensdarmes; roles=PartyRole.PartyLeader,PartyRole.PartyLeader; bonuses=0.1f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2174 |
| TacticsDecisiveBattle | tier=GetTierCost(2); alternative=TacticsExtendedSkirmish; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.05f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2159 |
| TacticsEliteReserves | tier=GetTierCost(8); alternative=TacticsEncirclement; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=-0.2f,-0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2170 |
| TacticsEncirclement | tier=GetTierCost(8); alternative=TacticsEliteReserves; roles=PartyRole.PartyLeader,PartyRole.ArmyCommander; bonuses=0.05f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2171 |
| TacticsExtendedSkirmish | tier=GetTierCost(2); alternative=TacticsDecisiveBattle; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.1f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2158 |
| TacticsGensdarmes | tier=GetTierCost(10); alternative=TacticsCounteroffensive; roles=PartyRole.Captain,PartyRole.Governor; bonuses=0.02f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2175 |
| TacticsHordeLeader | tier=GetTierCost(3); alternative=TacticsSmallUnitTactics; roles=PartyRole.PartyLeader,PartyRole.ArmyCommander; bonuses=10f,-0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2161 |
| TacticsImproviser | tier=GetTierCost(5); alternative=TacticsSwiftRegroup; roles=PartyRole.PartyMember,PartyRole.PartyLeader; bonuses=0f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2165 |
| TacticsLawkeeper | tier=GetTierCost(4); alternative=TacticsCoaching; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.1f,0.04f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2162 |
| TacticsLooseFormations | tier=GetTierCost(1); alternative=TacticsTightFormations; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=-0.1f,-0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2157 |
| TacticsMakeThemPay | tier=GetTierCost(7); alternative=TacticsPickThemOfTheWalls; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.25f,0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2169 |
| TacticsOnTheMarch | tier=GetTierCost(6); alternative=TacticsCallToArms; roles=PartyRole.ArmyCommander,PartyRole.Governor; bonuses=-0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2166 |
| TacticsPickThemOfTheWalls | tier=GetTierCost(7); alternative=TacticsMakeThemPay; roles=PartyRole.Engineer,PartyRole.Governor; bonuses=0.25f,0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2168 |
| TacticsPreBattleManeuvers | tier=GetTierCost(9); alternative=TacticsBesieged; roles=PartyRole.PartyMember,PartyRole.PartyLeader; bonuses=0.25f,0.01f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2172 |
| TacticsSmallUnitTactics | tier=GetTierCost(3); alternative=TacticsHordeLeader; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=1f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2160 |
| TacticsSwiftRegroup | tier=GetTierCost(5); alternative=TacticsImproviser; roles=PartyRole.PartyMember,PartyRole.PartyLeader; bonuses=-0.15f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2164 |
| TacticsTacticalMastery | tier=GetTierCost(11); alternative=null; roles=PartyRole.ArmyCommander; bonuses=0.005f; increments=EffectIncrementType.AddFactor | 2176 |
| TacticsTightFormations | tier=GetTierCost(1); alternative=TacticsLooseFormations; roles=PartyRole.PartyLeader,PartyRole.Captain; bonuses=0.1f,-0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2156 |

### Throwing

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| ThrowingFlexibleFighter | tier=GetTierCost(2); alternative=ThrowingHunter; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2056 |
| ThrowingFocus | tier=GetTierCost(6); alternative=ThrowingLastHit; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.25f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2063 |
| ThrowingHeadHunter | tier=GetTierCost(7); alternative=ThrowingSlingingCompetitions; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2065 |
| ThrowingHunter | tier=GetTierCost(2); alternative=ThrowingFlexibleFighter; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.4f,0.08f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2055 |
| ThrowingImpale | tier=GetTierCost(10); alternative=ThrowingWeakSpot; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2072 |
| ThrowingKnockOff | tier=GetTierCost(4); alternative=ThrowingRunningThrow; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.05f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2060 |
| ThrowingLastHit | tier=GetTierCost(6); alternative=ThrowingFocus; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.5f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2064 |
| ThrowingLongReach | tier=GetTierCost(9); alternative=ThrowingPerfectTechnique; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2070 |
| ThrowingMountedSkirmisher | tier=GetTierCost(3); alternative=ThrowingWellPrepared; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2057 |
| ThrowingPerfectTechnique | tier=GetTierCost(9); alternative=ThrowingLongReach; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2069 |
| ThrowingQuickDraw | tier=GetTierCost(1); alternative=ThrowingShieldBreaker; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2053 |
| ThrowingResourceful | tier=GetTierCost(8); alternative=ThrowingSplinters; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=2f,0.1f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2067 |
| ThrowingRunningThrow | tier=GetTierCost(4); alternative=ThrowingKnockOff; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,30f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2059 |
| ThrowingSaddlebags | tier=GetTierCost(5); alternative=ThrowingSkirmisher; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=2f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2062 |
| ThrowingShieldBreaker | tier=GetTierCost(1); alternative=ThrowingQuickDraw; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.4f,0.08f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2054 |
| ThrowingSkirmisher | tier=GetTierCost(5); alternative=ThrowingSaddlebags; roles=PartyRole.Personal,PartyRole.Captain; bonuses=-0.1f,-0.03f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2061 |
| ThrowingSlingingCompetitions | tier=GetTierCost(7); alternative=ThrowingHeadHunter; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.2f,1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 2066 |
| ThrowingSplinters | tier=GetTierCost(8); alternative=ThrowingResourceful; roles=PartyRole.Personal,PartyRole.Captain; bonuses=3f,0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2068 |
| ThrowingUnstoppableForce | tier=GetTierCost(11); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2073 |
| ThrowingWeakSpot | tier=GetTierCost(10); alternative=ThrowingImpale; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.3f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2071 |
| ThrowingWellPrepared | tier=GetTierCost(3); alternative=ThrowingMountedSkirmisher; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2058 |

### Trade

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| TradeAppraiser | tier=GetTierCost(1); alternative=TradeWholeSeller; roles=PartyRole.PartyLeader,PartyRole.Personal; bonuses=-0.15f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2239 |
| TradeArtisanCommunity | tier=GetTierCost(5); alternative=TradeGreatInvestor; roles=PartyRole.ClanLeader,PartyRole.Quartermaster; bonuses=1f,1f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2247 |
| TradeCaravanMaster | tier=GetTierCost(2); alternative=TradeMarketDealer; roles=PartyRole.Quartermaster,PartyRole.Personal; bonuses=0.3f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2241 |
| TradeContentTrades | tier=GetTierCost(6); alternative=TradeMercenaryConnections; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=0.1f,-0.5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2250 |
| TradeDistributedGoods | tier=GetTierCost(3); alternative=TradeLocalConnection; roles=PartyRole.Personal,PartyRole.Quartermaster; bonuses=2f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2243 |
| TradeEverythingHasAPrice | tier=GetTierCost(12); alternative=null; roles=PartyRole.Personal; bonuses=0f; increments=EffectIncrementType.Invalid | 2261 |
| TradeGranaryAccountant | tier=GetTierCost(8); alternative=TradeTradeyardForeman; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2253 |
| TradeGreatInvestor | tier=GetTierCost(5); alternative=TradeArtisanCommunity; roles=PartyRole.ClanLeader,PartyRole.Quartermaster; bonuses=1f,-0.3f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2248 |
| TradeInsurancePlans | tier=GetTierCost(7); alternative=TradeRapidDevelopment; roles=PartyRole.ClanLeader,PartyRole.Quartermaster; bonuses=5000f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2251 |
| TradeLocalConnection | tier=GetTierCost(3); alternative=TradeDistributedGoods; roles=PartyRole.Personal,PartyRole.Quartermaster; bonuses=2f,-0.15f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2244 |
| TradeManOfMeans | tier=GetTierCost(11); alternative=TradeTrickleDown; roles=PartyRole.ClanLeader,PartyRole.Personal; bonuses=-0.2f,-0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2259 |
| TradeMarketDealer | tier=GetTierCost(2); alternative=TradeCaravanMaster; roles=PartyRole.ClanLeader,PartyRole.Personal; bonuses=-0.5f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2242 |
| TradeMercenaryConnections | tier=GetTierCost(6); alternative=TradeContentTrades; roles=PartyRole.Governor,PartyRole.PartyLeader; bonuses=0.25f,-0.25f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2249 |
| TradeRapidDevelopment | tier=GetTierCost(7); alternative=TradeInsurancePlans; roles=PartyRole.ClanLeader,PartyRole.Quartermaster; bonuses=5000f,-0.25f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 2252 |
| TradeSelfMadeMan | tier=GetTierCost(9); alternative=TradeSwordForBarter; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.5f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2256 |
| TradeSilverTongue | tier=GetTierCost(10); alternative=TradeSpringOfGold; roles=PartyRole.Personal,PartyRole.Quartermaster; bonuses=-0.15f,0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2257 |
| TradeSpringOfGold | tier=GetTierCost(10); alternative=TradeSilverTongue; roles=PartyRole.ClanLeader,PartyRole.Governor; bonuses=0.001f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2258 |
| TradeSwordForBarter | tier=GetTierCost(9); alternative=TradeSelfMadeMan; roles=PartyRole.Personal,PartyRole.Quartermaster; bonuses=-0.2f,-0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2255 |
| TradeTollgates | tier=GetTierCost(4); alternative=TradeTravelingRumors; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0f,30f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2246 |
| TradeTradeyardForeman | tier=GetTierCost(8); alternative=TradeGranaryAccountant; roles=PartyRole.Personal,PartyRole.Governor; bonuses=-0.2f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2254 |
| TradeTravelingRumors | tier=GetTierCost(4); alternative=TradeTollgates; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0f,20f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2245 |
| TradeTrickleDown | tier=GetTierCost(11); alternative=TradeManOfMeans; roles=PartyRole.PartyLeader,PartyRole.Governor; bonuses=1f,2f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 2260 |
| TradeWholeSeller | tier=GetTierCost(1); alternative=TradeAppraiser; roles=PartyRole.PartyLeader,PartyRole.Personal; bonuses=-0.15f,0f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 2240 |

### TwoHanded

| Perk ID | Choice / role / raw bonus / increment | Definition line |
| --- | --- | --- |
| TwoHandedBaptisedInBlood | tier=GetTierCost(3); alternative=TwoHandedShowOfStrength; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,0.05f; increments=EffectIncrementType.Add,EffectIncrementType.AddFactor | 1977 |
| TwoHandedBeastSlayer | tier=GetTierCost(4); alternative=TwoHandedShieldBreaker; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.5f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1978 |
| TwoHandedBerserker | tier=GetTierCost(5); alternative=TwoHandedConfidence; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.2f,-0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1980 |
| TwoHandedBladeMaster | tier=GetTierCost(9); alternative=TwoHandedVandal; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1987 |
| TwoHandedConfidence | tier=GetTierCost(5); alternative=TwoHandedBerserker; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0.15f,0.3f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1981 |
| TwoHandedHeadBasher | tier=GetTierCost(2); alternative=TwoHandedOnTheEdge; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1975 |
| TwoHandedHope | tier=GetTierCost(7); alternative=TwoHandedTerror; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.3f,5f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1984 |
| TwoHandedOnTheEdge | tier=GetTierCost(2); alternative=TwoHandedHeadBasher; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.03f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1974 |
| TwoHandedProjectileDeflection | tier=GetTierCost(6); alternative=null; roles=PartyRole.Personal,PartyRole.Governor; bonuses=0f,0.1f; increments=EffectIncrementType.Invalid,EffectIncrementType.AddFactor | 1982 |
| TwoHandedRecklessCharge | tier=GetTierCost(8); alternative=TwoHandedThickHides; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.2f,0.02f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1985 |
| TwoHandedShieldBreaker | tier=GetTierCost(4); alternative=TwoHandedBeastSlayer; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.4f,0.1f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1979 |
| TwoHandedShowOfStrength | tier=GetTierCost(3); alternative=TwoHandedBaptisedInBlood; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.3f,-0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1976 |
| TwoHandedStrongGrip | tier=GetTierCost(1); alternative=TwoHandedWoodChopper; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.1f,30f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1972 |
| TwoHandedTerror | tier=GetTierCost(7); alternative=TwoHandedHope; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=0.2f,10f; increments=EffectIncrementType.AddFactor,EffectIncrementType.Add | 1983 |
| TwoHandedThickHides | tier=GetTierCost(8); alternative=TwoHandedRecklessCharge; roles=PartyRole.Personal,PartyRole.PartyLeader; bonuses=5f,5f; increments=EffectIncrementType.Add,EffectIncrementType.Add | 1986 |
| TwoHandedVandal | tier=GetTierCost(9); alternative=TwoHandedBladeMaster; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.25f,0.2f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1988 |
| TwoHandedWayOfTheGreatAxe | tier=GetTierCost(10); alternative=null; roles=PartyRole.Personal,PartyRole.Personal; bonuses=0.002f,0.005f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1989 |
| TwoHandedWoodChopper | tier=GetTierCost(1); alternative=TwoHandedStrongGrip; roles=PartyRole.Personal,PartyRole.Captain; bonuses=0.3f,0.15f; increments=EffectIncrementType.AddFactor,EffectIncrementType.AddFactor | 1973 |

## Other campaign registrations

These are registrations from `SandBoxManager` and `SandBoxSubModule`, not an exhaustive game-wide list.
StoryMode, custom battle, native multiplayer, native engine behavior, and optional modules need separate expansion.

| Registered behavior | Registrar / line |
| --- | --- |
| AIMoveToNearestLandBehavior | TaleWorlds.CampaignSystem.SandBoxManager:124 |
| AgingCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:140 |
| AiArmyMemberBehavior | TaleWorlds.CampaignSystem.SandBoxManager:117 |
| AiEngagePartyBehavior | TaleWorlds.CampaignSystem.SandBoxManager:120 |
| AiLandBanditPatrollingBehavior | TaleWorlds.CampaignSystem.SandBoxManager:121 |
| AiMilitaryBehavior | TaleWorlds.CampaignSystem.SandBoxManager:118 |
| AiPartyThinkBehavior | TaleWorlds.CampaignSystem.SandBoxManager:123 |
| AiPatrollingBehavior | TaleWorlds.CampaignSystem.SandBoxManager:119 |
| AiVisitSettlementBehavior | TaleWorlds.CampaignSystem.SandBoxManager:122 |
| AlleyCampaignBehavior | SandBox.SandBoxSubModule:60 |
| AllianceCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:95 |
| ArenaMasterCampaignBehavior | SandBox.SandBoxSubModule:70 |
| BackstoryCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:33 |
| BanditInteractionsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:37 |
| BanditSpawnCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:35 |
| BannerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:160 |
| BarberCampaignBehavior | SandBox.SandBoxSubModule:80 |
| BattleCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:141 |
| BoardGameCampaignBehavior | SandBox.SandBoxSubModule:68 |
| BuildingsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:43 |
| CampaignBattleRecoveryBehavior | TaleWorlds.CampaignSystem.SandBoxManager:151 |
| CampaignFactionManagerBehaviour | TaleWorlds.CampaignSystem.SandBoxManager:156 |
| CampaignWarManagerBehavior | TaleWorlds.CampaignSystem.SandBoxManager:152 |
| CaravanConversationsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:47 |
| CaravansCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:46 |
| CharacterCreationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:207 |
| CharacterDevelopmentCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:75 |
| CharacterRelationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:39 |
| CheckpointCampaignBehavior | SandBox.SandBoxSubModule:85 |
| ClanMemberRolesCampaignBehavior | SandBox.SandBoxSubModule:64 |
| ClanVariablesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:138 |
| CommentCharacterBornBehavior | TaleWorlds.CampaignSystem.SandBoxManager:113 |
| CommentChildbirthBehavior | TaleWorlds.CampaignSystem.SandBoxManager:112 |
| CommentOnChangeRomanticStateBehavior | TaleWorlds.CampaignSystem.SandBoxManager:98 |
| CommentOnChangeSettlementOwnerBehavior | TaleWorlds.CampaignSystem.SandBoxManager:99 |
| CommentOnChangeVillageStateBehavior | TaleWorlds.CampaignSystem.SandBoxManager:104 |
| CommentOnCharacterKilledBehavior | TaleWorlds.CampaignSystem.SandBoxManager:103 |
| CommentOnClanDestroyedBehavior | TaleWorlds.CampaignSystem.SandBoxManager:109 |
| CommentOnClanLeaderChangedBehavior | TaleWorlds.CampaignSystem.SandBoxManager:110 |
| CommentOnDeclareWarBehavior | TaleWorlds.CampaignSystem.SandBoxManager:107 |
| CommentOnDefeatCharacterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:102 |
| CommentOnDestroyMobilePartyBehavior | TaleWorlds.CampaignSystem.SandBoxManager:105 |
| CommentOnEndPlayerBattleBehavior | TaleWorlds.CampaignSystem.SandBoxManager:101 |
| CommentOnKingdomDestroyedBehavior | TaleWorlds.CampaignSystem.SandBoxManager:108 |
| CommentOnLeaveFactionBehavior | TaleWorlds.CampaignSystem.SandBoxManager:97 |
| CommentOnMakePeaceBehavior | TaleWorlds.CampaignSystem.SandBoxManager:106 |
| CommentOnPlayerMeetLordBehavior | TaleWorlds.CampaignSystem.SandBoxManager:100 |
| CommentPregnancyBehavior | TaleWorlds.CampaignSystem.SandBoxManager:111 |
| CommonTownsfolkCampaignBehavior | SandBox.SandBoxSubModule:61 |
| CommonVillagersCampaignBehavior | SandBox.SandBoxSubModule:71 |
| CompanionDismissCampaignBehavior | SandBox.SandBoxSubModule:62 |
| CompanionGrievanceBehavior | TaleWorlds.CampaignSystem.SandBoxManager:131 |
| CompanionRolesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:132 |
| CompanionsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:66 |
| CraftingCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:71 |
| CrimeCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:136 |
| DefaultCutscenesCampaignBehavior | SandBox.SandBoxSubModule:73 |
| DefaultLogsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:114 |
| DefaultNotificationsCampaignBehavior | SandBox.SandBoxSubModule:63 |
| DesertersCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:36 |
| DesertionCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:40 |
| DiplomaticBartersBehavior | TaleWorlds.CampaignSystem.SandBoxManager:125 |
| DisbandPartyCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:149 |
| DiscardItemsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:198 |
| DisorganizedStateCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:200 |
| DumpIntegrityCampaignBehavior | SandBox.SandBoxSubModule:84 |
| DynamicBodyCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:80 |
| EducationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:90 |
| EmissarySystemCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:155 |
| EncounterGameMenuBehavior | TaleWorlds.CampaignSystem.SandBoxManager:31 |
| FactionDiscontinuationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:139 |
| FiefBarterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:127 |
| FindingItemOnMapBehavior | TaleWorlds.CampaignSystem.SandBoxManager:42 |
| FoodConsumptionBehavior | TaleWorlds.CampaignSystem.SandBoxManager:41 |
| GarrisonRecruitmentCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:204 |
| GarrisonTroopsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:45 |
| GoldBarterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:129 |
| GovernorCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:48 |
| GuardsCampaignBehavior | SandBox.SandBoxSubModule:66 |
| HeirSelectionCampaignBehavior | SandBox.SandBoxSubModule:72 |
| HeroAgentSpawnCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:73 |
| HeroKnownInformationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:148 |
| HeroSpawnCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:69 |
| HideoutCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:49 |
| HideoutConversationsCampaignBehavior | SandBox.SandBoxSubModule:59 |
| IncidentsCampaignBehaviour | TaleWorlds.CampaignSystem.SandBoxManager:208 |
| InfluenceGainCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:159 |
| InitialChildGenerationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:144 |
| IssuesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:158 |
| ItemBarterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:128 |
| ItemConsumptionBehavior | TaleWorlds.CampaignSystem.SandBoxManager:44 |
| JournalLogsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:115 |
| KingdomDecisionProposalBehavior | TaleWorlds.CampaignSystem.SandBoxManager:153 |
| LordConversationsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:65 |
| LordDefectionCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:57 |
| MapTracksCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:72 |
| MapWeatherCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:203 |
| MarriageOfferCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:93 |
| MilitiasCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:61 |
| MobilePartyTrainingBehavior | TaleWorlds.CampaignSystem.SandBoxManager:88 |
| NPCEquipmentsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:84 |
| NotableHelperCharacterCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:74 |
| NotablePowerManagementBehavior | TaleWorlds.CampaignSystem.SandBoxManager:145 |
| NotableSupportersCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:210 |
| NotablesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:64 |
| OrderOfBattleCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:199 |
| ParleyCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:206 |
| PartiesBuyFoodCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:50 |
| PartiesBuyHorseCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:51 |
| PartiesSellLootCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:59 |
| PartiesSellPrisonerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:58 |
| PartyDiplomaticHandlerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:205 |
| PartyHealCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:150 |
| PartyRolesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:154 |
| PartyUpgraderCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:30 |
| PatrolPartiesCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:209 |
| PeaceOfferCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:92 |
| PerkActivationHandlerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:146 |
| PerkResetCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:201 |
| PlayerArmyWaitBehavior | TaleWorlds.CampaignSystem.SandBoxManager:137 |
| PlayerCaptivityCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:32 |
| PlayerTownVisitCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:79 |
| PlayerTrackCompanionBehavior | TaleWorlds.CampaignSystem.SandBoxManager:133 |
| PlayerVariablesBehavior | TaleWorlds.CampaignSystem.SandBoxManager:87 |
| PoliticalStagnationAndBorderIncidentCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:52 |
| PregnancyCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:143 |
| PrisonBreakCampaignBehavior | SandBox.SandBoxSubModule:65 |
| PrisonerCaptureCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:54 |
| PrisonerRecruitCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:55 |
| PrisonerReleaseCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:53 |
| RansomOfferCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:91 |
| RebellionsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:134 |
| RecruitPrisonersCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:68 |
| RecruitmentAgentSpawnBehavior | SandBox.SandBoxSubModule:89 |
| RecruitmentCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:77 |
| RetirementCampaignBehavior | SandBox.SandBoxSubModule:82 |
| RetrainOutlawPartyMembersBehavior | TaleWorlds.CampaignSystem.SandBoxManager:67 |
| RomanceCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:56 |
| SallyOutsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:135 |
| SetPrisonerFreeBarterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:126 |
| SettlementClaimantCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:62 |
| SettlementMusiciansCampaignBehavior | SandBox.SandBoxSubModule:67 |
| SettlementVariablesBehavior | TaleWorlds.CampaignSystem.SandBoxManager:60 |
| SiegeAftermathCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:83 |
| SiegeAmbushCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:202 |
| SiegeEventCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:157 |
| StatisticsCampaignBehavior | SandBox.SandBoxSubModule:83 |
| StealthCharactersCampaignBehavior | SandBox.SandBoxSubModule:86 |
| TavernEmployeesCampaignBehavior | SandBox.SandBoxSubModule:87 |
| TeleportationCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:161 |
| TournamentCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:70 |
| TownMerchantsCampaignBehavior | SandBox.SandBoxSubModule:88 |
| TownSecurityCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:147 |
| TradeAgreementsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:89 |
| TradeCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:34 |
| TradeRumorsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:63 |
| TradeSkillCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:76 |
| TradersCampaignBehavior | SandBox.SandBoxSubModule:69 |
| TransferPrisonerBarterBehavior | TaleWorlds.CampaignSystem.SandBoxManager:130 |
| TributesCampaignBehaviour | TaleWorlds.CampaignSystem.SandBoxManager:96 |
| VassalAndMercenaryOfferCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:94 |
| ViewDataTrackerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:116 |
| VillageGoodProductionCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:82 |
| VillageHealCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:86 |
| VillageHostileActionCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:78 |
| VillageTradeBoundCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:81 |
| VillagerCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:85 |
| WorkshopsCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:142 |
| WorkshopsCharactersCampaignBehavior | TaleWorlds.CampaignSystem.SandBoxManager:38 |
