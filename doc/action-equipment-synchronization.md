# Action and equipment synchronization

Wielded equipment previously followed the movement polling cadence, while realized actions were sampled after native agent processing. A weapon switch could therefore publish its new attack before the corresponding wield state was sampled.

`AgentActionData` carries an equipment revision. The first published revision and each equipment change include the main hand, offhand, usage index and item identities; subsequent actions carry only the revision. Equipment changes publish an action update even when the animation index is unchanged. Movement polling no longer publishes separate equipment updates.

The receiver retains equipment independently of pending actions, with separate ordinary-sender and host baselines per agent and controller. Host baselines retain the latest positive epoch; epoch zero cannot be blocked by a former host baseline. Applying an action still requires the matching epoch and validated authority. A newer pending action can replace an older one without losing the equipment it references. The receiver checks authority and sequence before applying equipment. It validates both slots, item identities and usage, applies the wield state, and checks the result before replaying the action. If equipment is unavailable, the existing pending-action mechanism retries without acknowledging the sequence. A newer snapshot can supersede that pending dependency. An unknown revision waits for its baseline; a reference to an older revision cannot restore equipment after a newer baseline has arrived. Retained guards stop replaying when their equipment dependency no longer matches.

Joining peers receive full baselines, including for unarmed agents. Sending one peer a baseline does not mark it as broadcast to everyone. Authority revision or battle host epoch changes force a fresh outbound baseline. Once an agent receives combined snapshots, legacy standalone equipment packets cannot overwrite them. Authority transfer clears that mode with the previous equipment state. New fields are optional for deserialization; deployment still requires matching Coop builds, as enforced by the existing connection handshake.

## Automated verification

`ActionEquipmentSyncTests` covers:

- A weapon switch and attack captured after a movement poll, before the next movement poll.
- A weapon-usage change without an animation change.
- Equipment identity and unwielded offhand serialization.
- Wield application before action replay, and rejection of subsequent legacy equipment updates.
- Missing or different items delaying the action until the matching item arrives.
- A newer snapshot replacing a pending dependency and rejecting stale replay.
- Wrong authority changing neither equipment nor action.
- Unchanged equipment using a small revision reference.
- Pending references retaining their baseline across action replacement and late registration.
- Unknown revisions waiting, and controller/host-epoch changes requiring the matching baseline.
- Targeted catch-up preserving the baseline still owed to existing peers.
- Fresh baselines after authority revision changes.
- A deferred former-host action being replaced by the validated ordinary authority before the pending sweep, then replaying its latest action when the missing weapon arrives.

`BattleBlockingSyncTests` covers a former host retaining or regaining ordinary authority, including references arriving before the fresh baseline and rejection of delayed traffic from the former host epoch. Existing blocking, movement, equipment, pickup and authority-transfer tests cover the neighboring paths. The tests use the managed mission fixture; they do not execute native combat or reproduce the captured divide-by-zero.

## Serialized traffic

With unchanged equipment and revision 1, the reference adds 3 bytes per action in the measured serializer samples. An eight-agent sample packet grows from 496 to 520 bytes, instead of 760 bytes for an axe or 816 bytes for an axe and shield with repeated snapshots. This is about 91–93% less added payload than the initial implementation. Full snapshots are still paid for on changes and catch-up; larger revision values use more bytes. These are application payload measurements, not a live battle bandwidth measurement.

## Runtime verification still required

Use the same build on the server and both clients. In a shared siege at Danustica (`town_ES1`), use normal player controls to switch between a ranged weapon and a melee weapon, attack immediately after switching, and repeat while nearby AI is fighting. Have the second player observe the synchronized weapon and attack. Repeat with a held block and an offhand shield change, then leave and rejoin to check current equipment while idle.

Check that the observing client never displays the new attack with the previous weapon, that guard release still works, and that agents whose equipment arrives later resume their latest action. Compare both client logs and retain any crash evidence. A successful run validates this synchronization change; it does not retrospectively prove the original siege crash was caused by this sampling gap.
