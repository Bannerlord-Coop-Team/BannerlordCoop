# PR #3949 live evidence

Product source: b82865f7d967f328b6366ef44b2bfbb67b6261a3. Local up20.

TournamentFailure completed through native elimination: A received the failed-quest journal and -5 relation once; B had no quest or changes. A's actual-defeat image shows the native Quest Failed banner.

The village-crime failure completed through the real hostile-action goods menu at an already-hostile village owned by Sora's clan. A lost 50 Honor XP per attempt; B's personal XP stayed 0. Both endpoints and the server reported the same public trait levels. The images show the native quest-update banner and B's unchanged town; the XP and crime-specific journal were verified through retained game-state reads, not visible UI text.

Only the combat input was automated. No outcome overlay or screenshot edit was used. Cleanup verified all owned processes absent and the bench free.

Success-result and rejoin screenshots remain pending. The existing up18 file named TournamentSuccess-owner actually shows acceptance and is deliberately excluded from branch proof.

## up21, b8fd953df

Production co-op UI proof replaces the earlier native-only tournament evidence for current-head acceptance. A clicked the real arena Join/Start options. CoopTournamentBehavior and canonical server sessions were observed. AI-only matches used the co-op Skip choice; the player's matches used Join followed by combat automation against the actual spawned agents. The native shared tournament_join/StartMatch path was not used.

TournamentSuccess: completed canonical session, success journal, +750 quest gold and +1 quest renown, model-adjusted relation +6; the tournament separately awarded +3 renown. TournamentFailure: first player match lost, failure journal and relation -5, no rewards. Village crime: already-hostile giver-clan village's real force-goods menu, crime-specific failure journal, owner XP -50 and relation -5. B had no personal quest or reward changes across these outcomes.

The success image shows the actual victory bracket and gold receipt; the failure image shows A down and team disqualified. Exact journal/trait/reward assertions were read on server and clients. The crime image shows the actual quest-update banner, not XP text. Rejoin remains deferred by the supervisor for #4013. Cleanup verified all run processes absent and the bench free.
