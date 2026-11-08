# Direct delivery and timeout

Head `bdfe1ae6716a61c0ade1ea42743e814a59c64dc2`; DS `fc52b3c12fef858207fafe2ef2a7dc4f075d0fd7`. Job `live-3760-rl-5d74df421b75`, token `7bf2c989ae2bc70eed4cf11815193e25`.

Nine banked rows: ordinary merchant surrender, ruler merchant surrender, concurrent direct ownership, merchant refusal, no-goods choice, B full delivery, A partial then remainder, target delivery refusal and timeout. All use the final head. Scenario and launcher exited 0. All 1,935 collected manifest hashes were checked, with 28 screenshots.

Both deliveries paid 650 once and retained two surplus goods. Surrender, refusal and timeout did not pay a delivery reward. The ruler dialogue uses the actual royal surrender wording. Timeout crossed the native due time and let the hourly quest manager finish it. A cached completed quest can retain its old partial numeric field; acceptance uses the actual inventory/reward, live-manager removal and selected native journal showing 6/6. Timeout's clock jump includes NPC power drift, so the net power change is not presented as an isolated quest delta.

Owner and bystander target captures were reviewed. Runtime cleanup and the independent host audit showed no owned workloads and a clean DS worktree. Full logs and source receipts are retained under `3760/remote/r42-bdfe1ae67-direct-result`.

## Selected captures

- [003-direct-surrender-failure-target-A-testclient.png](r42-003-direct-surrender-failure-target-A-testclient.png)
- [004-direct-surrender-failure-target-B-testclient2.png](r42-004-direct-surrender-failure-target-B-testclient2.png)
- [005-concurrent-full-partial-active-target-A-testclient.png](r42-005-concurrent-full-partial-active-target-A-testclient.png)
- [008-concurrent-full-partial-active-target-B-testclient2.png](r42-008-concurrent-full-partial-active-target-B-testclient2.png)
- [010-concurrent-full-partial-success-target-B-testclient2.png](r42-010-concurrent-full-partial-success-target-B-testclient2.png)
- [011-concurrent-full-partial-active-target-A-testclient.png](r42-011-concurrent-full-partial-active-target-A-testclient.png)
- [013-concurrent-full-partial-active-target-A-testclient.png](r42-013-concurrent-full-partial-active-target-A-testclient.png)
- [015-concurrent-full-partial-success-target-A-testclient.png](r42-015-concurrent-full-partial-success-target-A-testclient.png)
- [017-direct-ruler-surrender-failure-target-A-testclient.png](r42-017-direct-ruler-surrender-failure-target-A-testclient.png)
- [021-delivery-refusal-failure-target-A-testclient.png](r42-021-delivery-refusal-failure-target-A-testclient.png)
- [025-direct-timeout-failure-target-A-testclient.png](r42-025-direct-timeout-failure-target-A-testclient.png)
- [026-direct-timeout-failure-target-B-testclient2.png](r42-026-direct-timeout-failure-target-B-testclient2.png)
