# Action and equipment synchronization

Wielded equipment previously followed the movement polling cadence, while realized actions were sampled after native agent processing. A weapon switch could therefore publish its new attack before the corresponding wield state was sampled.

`AgentActionData` now carries a nullable equipment snapshot containing the main hand, offhand, usage index and item identities. Equipment changes also publish an action update, even when the animation index is unchanged. Movement polling no longer publishes separate equipment updates.

The receiver checks authority and sequence before applying equipment. It validates both slots, item identities and usage, applies the wield state, and checks the result before replaying the action. If equipment is unavailable, the existing pending-action mechanism retries without acknowledging the sequence. A newer snapshot can supersede that pending dependency. Retained guards stop replaying when their equipment dependency no longer matches.

Once an agent receives combined snapshots, legacy standalone equipment packets cannot overwrite them. Authority transfer clears that mode with the previous equipment state. New fields are optional for deserialization; deployment still requires matching Coop builds, as enforced by the existing connection handshake.

## Automated verification

`ActionEquipmentSyncTests` covers:

- A weapon switch and attack captured after a movement poll, before the next movement poll.
- A weapon-usage change without an animation change.
- Equipment identity and unwielded offhand serialization.
- Wield application before action replay, and rejection of subsequent legacy equipment updates.
- Missing or different items delaying the action until the matching item arrives.
- A newer snapshot replacing a pending dependency and rejecting stale replay.
- Wrong authority changing neither equipment nor action.

Existing blocking, movement, equipment, pickup and authority-transfer tests cover the neighboring paths. The tests use the managed mission fixture; they do not execute native combat or reproduce the captured divide-by-zero.

## Runtime verification still required

Use the same build on the server and both clients. In a shared siege at Danustica (`town_ES1`), use normal player controls to switch between a ranged weapon and a melee weapon, attack immediately after switching, and repeat while nearby AI is fighting. Have the second player observe the synchronized weapon and attack. Repeat with a held block and an offhand shield change, then leave and rejoin to check current equipment while idle.

Check that the observing client never displays the new attack with the previous weapon, that guard release still works, and that agents whose equipment arrives later resume their latest action. Compare both client logs and retain any crash evidence. A successful run validates this synchronization change; it does not retrospectively prove the original siege crash was caused by this sampling gap.
