# Manual testing — Keep campaign time running while players join

## Fix under test

Players already in the campaign should be able to continue playing while another player joins. Campaign time keeps its current speed, player time controls stay locked during loading, and the joining client remains behind the loading screen until the held campaign updates have been applied.

The synchronous campaign snapshot can still cause a brief hitch when the join begins, but the game should not remain paused throughout the save transfer and client load.

## Setup

1. Build and deploy the Debug mod from the main checkout.
2. Start the server and two clients.
3. Connect the first client and wait for the campaign map to finish loading.
4. Leave the second client at the connection screen.

## Test 1 — Existing player continues during a join

1. First client: set campaign time to fast-forward and watch the campaign clock and nearby mobile parties move.
2. Second client: connect to the server and begin loading the campaign.
3. First client: confirm there may be a brief hitch when the server takes the snapshot, but the campaign clock and mobile parties resume without waiting for the second client to finish loading.
4. Confirm the first client is told that one player is loading and that attempts to change campaign speed are ignored until loading completes.
5. Continue moving the first client's party while the second client loads.

## Test 2 — Joining player catches up before release

1. Second client: confirm the loading screen stays visible while the save and held campaign updates are applied.
2. Wait for the second client to enter the campaign map.
3. Compare both clients and confirm the campaign time and the first client's party position agree.
4. Confirm both clients are told that time controls are enabled.
5. Change campaign speed and verify the new speed applies to both clients.

## Expected (fixed)

The campaign only has a brief snapshot hitch. It does not stay paused while the joining client loads, the joining client receives changes made after its snapshot, and time controls work again after catch-up completes.

## Buggy behavior (before the fix)

The server forced campaign time to pause before taking the snapshot and kept it paused until every loading player entered the campaign. Existing players could not continue until the join finished.

## Watch the logs

Inspect `mb2/bin/Win64_Shipping_Client/Coop_server.log`, `Coop_client.log`, and the PID-suffixed `Coop_client_<pid>.log`. They should not contain `Failed to get`, `BlockingTimeout`, `NullReferenceException`, or errors handling `NetworkJoinCatchUpComplete`.
