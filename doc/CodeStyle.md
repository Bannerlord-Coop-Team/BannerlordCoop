# Code Style & Naming

## Basics

- Four-space indentation, PascalCase for public types and members, camelCase for locals and parameters.
- Give synchronization messages, services, and patches descriptive names.
- Keep changes scoped to the owning project.
- Write the positive role check, `ModInformation.IsServer` or `ModInformation.IsClient`, not `!ModInformation.IsClient`. `IsClient` is defined as `!IsServer`, so the negation reads as a double negative.
- Don't add `#region`/`#endregion` blocks; the codebase doesn't use them.
- Split a condition with several clauses into one guard clause per reason, or name it with a `bool` local, so each rule reads on its own line. A single check stays inline, as in Fit the Existing Codebase.
- Keep filters out of `foreach` headers and lambdas, and keep side effects and `out` variables out of conditions. Iterate the source and open the body with a `continue` guard, so the rule is visible.
- Dispatch on an enum with a `switch` that has a case per value and a throwing `default`, not an `if`/`else if` chain, because a bare final `else` silently takes values added later.

## Fit the Existing Codebase

- **Don't invent new patterns or naming schemes.** Find how the codebase already solves the same problem and match it, reusing the existing suffixes (`*Handler`, `*Patches`, `*Registry`, `*Interface`) and mechanisms.
- A novel concept such as a `SomeFeatureSentinel` class, or a homegrown mechanism next to an existing one, makes the code harder to navigate and gets flagged in review. If nothing existing fits, ask before introducing something new.
- Before adding a helper, grep for an existing one of the same shape and reuse it.
- Don't add a private method that only wraps one or two existing calls. Write the guards inline at the call site, like `if (!playerManager.IsSenderControllingParty(payload.Who, data.MobilePartyId)) return;`.
- When several handlers need the same check, add it once to the shared service, like `IPlayerManager`, instead of a private copy in each handler.

Use the existing framework for each of these instead of building a parallel one:

| Need | Use |
| --- | --- |
| Sync a field or property whenever it changes | AutoSync: a `<Feature>Sync : IAutoSync` class calling `registry.AddField`/`AddProperty` (see `GameInterface/AutoSync/README.md`) |
| Replicate an action or event | an `IMessage`/`IEvent` published on the `MessageBroker` and handled by an `IHandler` |
| Address a game object over the network | `IObjectManager` ids and a `RegistryBase<T>` registry; resolve with `TryGetObjectWithLogging` |
| Decide whether vanilla code runs on this side | a Harmony prefix gated on `CallOriginalPolicy.IsOriginalAllowed()` |
| Re-run vanilla code while applying a received message | `AllowedThread`, on the receive path only |
| Run work on the game thread | `GameThread.RunSafe` |
| Provide a collaborator | an interface plus a registration in the owning Autofac module |
| Log | `LogManager.GetLogger<T>()` |
| Add a console or debug command | an `ICoopCommand` registered in the owning module, not a new `[CommandLineArgumentFunction]` method |
| Lay out a feature | `Services/<Feature>/` with `Commands`, `Handlers`, `Interfaces`, `Messages` and `Patches` folders, plus `<Feature>Registry`/`<Feature>Sync` |
| Fake dependencies in unit tests | `RegisterMock<T>` in `Coop.Tests/TestComponentBase.cs` |
| Read per-client state or a service from a Harmony patch | resolve it inside the patch with `ContainerProvider.TryResolve<T>`, and run vanilla when the resolve fails |
| Hold state that Missions writes and GameInterface patches read | an interface plus a `*Registry`/`*Tracker` in GameInterface, registered `InstancePerLifetimeScope`, like `IBattleHostRegistry` |
| Let GameInterface ask Missions a question | an interface declared in GameInterface, implemented in Missions and registered in `MissionModule`, like `ICoopBattleBehaviorAttacher` |
| Carry context through a vanilla call, such as "dont capture this spawn" | `AllowedThread` on the receive path, otherwise an `IDisposable` scope from an injected service that restores the previous value on dispose |
| Give a vanilla mission behavior per-mission data | a `Coop<Vanilla>` subclass built by the launcher, like `CoopSiegeDeploymentMissionController` |

Don't add a `static` class, or a `*Gate`, that other code writes and patches read as ambient state. The usual reason given, "patches are static so they can't resolve DI", isn't true here, because patches resolve through `ContainerProvider`. A static also leaks between E2E client instances, which share one process. Decompile the vanilla type before patching too, because vanilla often already has the switch (for example `GauntletChatLogView.SetEnabled`).

## Classes, Interfaces, and DI

- Declare the interface at the top of the file and the implementation directly below it. Do this even for a single class; it keeps the seam explicit and the type fakeable.
- Name the file after the implementation, without the interface's leading `I`. For example, `AlleyCampaignBehaviorInterface.cs` holds `IAlleyCampaignBehaviorInterface` and `AlleyCampaignBehaviorInterface`.
- Register the type in the owning Autofac module (or confirm it's already registered) so consumers resolve it from the container instead of constructing it.
- Write new shared logic as a **transient DI class** (an interface plus an `InstancePerDependency` registration, constructor-injected), not a `static` helper, because transient dependencies are much easier to fake in tests. `AgentFormationAssigner` started as a static class and was asked in review to become an injected transient. Existing static helpers like `AgentAiWaker` predate this and can stay.
- The battle sub-services `PuppetSpawner`, `ReinforcementFielder`, and `BattleAuthorityMigrator` are `new`ed in `CoopBattleController`. For a helper they share, register it in `MissionModule` and inject it into `CoopBattleController`, which hands it to the services it constructs.
- A class that subscribes to a message implements `IDisposable` and unsubscribes in `Dispose`.
- Static collections in patches outlive the session. Keep that state in a DI'd service, or clear it when the campaign ends.
- Use the result of `ContainerProvider.TryResolve(out var x)` instead of ignoring it and null-checking `x` afterwards.
- Give a shared field one writer, a method that also does what must go with the write, like nulling it before a destroy or sending the sync message. Otherwise every caller has to remember the sequence and one eventually forgets.
- Don't expose steps a caller must call in a set order or on a set thread; expose one method that does them. Put a check and the record it guards in one method under one lock, otherwise another thread can run between them.

## Harmony

- A class applied through `PatchAllUncategorized` needs a `[HarmonyPatch]` attribute, even an empty one, otherwise it's never applied.
- Harmony calls methods named `Prepare`, `Cleanup`, `TargetMethod` and `TargetMethods` on a patch class at patch time, with null arguments. Don't give helpers those names, because a throw there stops all patching.
- Give each patched method one owner. A `TargetMethods` that yields every method of a type also picks up methods another class patches, so the method gets both classes' patches. Exclude them.
- Check a patch is needed before adding it. Reviewers ask why a patch exists when the path is already blocked.

## Validation

- Don't validate inputs that are always valid, such as constructor-injected dependencies (Autofac never injects null), or a value an earlier step guarantees. Use `First` over `FirstOrDefault` plus a return, so a broken invariant throws into a logging catch like `GameThread.RunSafe` instead of silently falling back to vanilla.
- Validate only where bad data can actually arrive, like network message contents or game state that can really be gone by the time a message applies.
- Log a warning with the ids involved at every early exit that drops requested work, and don't report success from a path that did nothing. Don't send a normal-looking value, like an empty id, in place of a failed lookup.
- Check every precondition before the first state change, so a failure can't leave state half applied. Put must-run cleanup, like releasing a claim or sending the finished message, in a `finally` so an earlier throw can't skip it.
- Co-op assumes people are playing with their friends, so cheating gets dealt with in house and the mod isn't responsible for anti-cheat. Don't add validation or tests whose only justification is stopping a cheating, malicious or modified client or a forged packet. A check, including one on network message contents, is justified by an honest failure path like a double-click, race, disconnect or stale local state.
- When a check is warranted, use the statement form `if (x == null) throw ...`, not `x ?? throw new ArgumentNullException(...)`.
- Data and message types just assign.
- Don't add a role check to a handler that only runs on one side. A message only clients send only reaches the server, so `ModInformation.IsServer` in its handler doesn't do anything.

## Tests

- Test a shared guard once, at the shared method, not again for every handler that calls it. Add a handler test only when that handler has its own logic around the guard.
- A test has to reach the behavior it's named for. One that stops at an earlier reply only shows the request got through.

## Campaign Behaviors

- Let vanilla `RegisterEvents` register its listeners, and Harmony-patch the individual event-handler methods to control which side runs them, with the positive role check on each patch (for example, `OnSessionLaunched => ModInformation.IsClient`).
- Don't patch `RegisterEvents` to rebuild the listener list with reflection or hand-made delegates, unless individual handler patches can't express the behavior.

## Comments

- One line stating the non-obvious why is the norm. Two or three lines are fine when the mechanism needs them; never write a 6+ line block.
- Put longer rationale in the commit message, where `git blame` surfaces it, instead of narrating vanilla behavior in a patch comment.
- A patch that blocks, narrows or copies a vanilla method still gets its one line: why, and where the work now lives or what differs, like `// Equipment creation is handled by the auto-registry (see EquipmentRegistry.Constructors).`
- Comment early exits whose outcome isn't obvious, such as discard versus retry later, and a `Try*` method that returns true with a null or empty result.
- When code moves or changes, re-read the summaries, doc comments and listed cases around it, and keep each comment on the class or method it describes. Interface docs state what callers can rely on, not how the implementation waits.
- Don't write `->`/`→` arrow chains.

## Files and Tooling

- `.cs` files are UTF-8 with a BOM. Files written through the editor have no BOM, so add one after creating a file, and only once, because a second pass doubles it.
- An on-save formatter may rewrite a file, so re-read it before a follow-up edit.
- `GameInterface` targets netstandard2.0, so newer BCL conveniences such as plain `Dictionary.TryAdd` aren't available.
- CodeFactor flags `new T[0]` (use `Array.Empty<T>()`) and unparenthesized mixed-operator arithmetic (write `4L + ((long)n * 4)`). Its `Complex method` note is advisory, so don't contort code to satisfy it.
