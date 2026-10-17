# Escort Merchant Caravan live handoff

Live testing is pending. Use the assigned dedicated server and two clients after the supervisor assigns a lane. Do not treat the automated quest tests as proof of encounters, missions, or UI presentation.

Start in Danustica (`town_ES1`, Southern Empire). Use `coop.debug.settlements.list_notables town_ES1` to resolve a merchant in the loaded save, then `coop.debug.issues.give` with that merchant's registered id and `EscortMerchantCaravanIssue`. Accept through the real merchant dialogue. Resolve a second merchant for the other player's concurrent quest. State-changing setup belongs on the server; inspect each player's journal on that player's client.

For each flow, compare the server's quest/party state and both clients. The owner alone should receive the personal journal, progress, relation changes and gold. Both clients should agree with the server about the caravan, raiders, battles and cleanup. Quest trait XP follows the framework's current disabled policy.

| Flow | Expected server result | Expected owner and other client results |
| --- | --- | --- |
| Offer, decline, direct accept | Decline creates no party. One accepted issue creates one registered caravan as the accepting player. | Only the accepting client starts the quest and sees its journal and caravan dialogue. |
| Eligibility and accept race | Vanilla relation, war and stay-alive conditions apply. The framework refuses a second Escort to the same player and arbitrates two accepts of one issue. | A rejected accept leaves no stray quest, caravan or journal. |
| Two players, separate merchants | Both issues have different owner controllers and caravans. | Each player sees only their own quest, progress and reward. |
| Caravan travel | The server selects destinations, waits, adjusts speed and records nearby settlement visits using the owner's party. | Visited-settlement counts, route logs and tracked parties match the owner's authoritative quest. |
| Complete required visits | Reward is `min(8000, daily reward * days escorting)`, giver power +10, owner relation +5, town prosperity +10. | The owner receives the completion log and gold once. The other player's personal state is unchanged. |
| No eligible next destination | The early-success branch pays the same day-based reward and applies the success world effects. | The owner sees the no-destination completion log. |
| Lose sight, then abandon | The warning starts beyond the owner's seeing range +3 while unseen; failure occurs beyond seeing range +20. Giver power -10, relation -5, town prosperity -10. | Only the owner sees the warning and failure journal. Another nearby player's sight does not keep this quest alive. |
| Raider ambush and escort battle | Raiders are registered, pursue the caravan and enter the real supported encounter/mission. Exercise a caravan win and defeat with the owner allied, hostile and absent, plus an empty caravan after battle. | Both clients agree about participants and cleanup. Owner journal and failure consequences match the actual battle outcome. |
| Attack the caravan | The real caravan attack dialogue reaches server hostility. Giver power -10, owner relation -10 and town prosperity -20. | The owner gets the hostile-failure journal once; the other player receives only world changes. |
| War, kingdom and settlement-owner changes | Cancel when the giver's town becomes hostile to this quest's owner; reroute away from hostile destinations where the quest remains active. | Only the affected owner's quest ends. The other owner's compatible quest continues. |
| Timeout, giver death and owner disconnect | End through the framework and release quest party flags and AI. Convert a surviving custom caravan back to a normal caravan once. | The owner sees the corresponding final state. No client repeats party conversion, rewards or world consequences. |
| Companion solution | Exercise selection cancel, invalid troops, accepted tier-2+ troops and a companion with 120 Riding or Scouting, success, failure/casualties and return. Preserve the server's creation reward roll and return troops to the accepting player's party. | Initial and final alternative journals belong only to the sender; the other player's troops, gold and personal journal stay unchanged. |

Retain before/after journal, reward, relation, party-id and cleanup observations for both clients. Combat must use actual encounters/missions; directly assigning a winner or calling a final outcome is not combat proof.
