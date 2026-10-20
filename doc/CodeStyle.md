# Code Style & Naming

## Basics

- Four-space indentation, PascalCase for public types and members, camelCase for locals and parameters.
- Give synchronization messages, services, and patches descriptive names.
- Keep changes scoped to the owning project.
- Write the positive role check, `ModInformation.IsServer` or `ModInformation.IsClient`, not `!ModInformation.IsClient`. `IsClient` is defined as `!IsServer`, so the negation reads as a double negative.
- Don't add `#region`/`#endregion` blocks; the codebase doesn't use them.

## Fit the Existing Codebase

- **Don't invent new patterns or naming schemes.** Find how the codebase already solves the same problem and match it, reusing the existing suffixes (`*Handler`, `*Patches`, `*Registry`, `*Interface`) and mechanisms.
- A novel concept such as a `SomeFeatureSentinel` class, or a homegrown mechanism next to an existing one, makes the code harder to navigate and gets flagged in review. If nothing existing fits, ask before introducing something new.
- Before adding a helper, grep for an existing one of the same shape and reuse it.

## Classes, Interfaces, and DI

- Declare the interface at the top of the file and the implementation directly below it. Do this even for a single class; it keeps the seam explicit and the type fakeable.
- Name the file after the implementation, without the interface's leading `I`. For example, `AlleyCampaignBehaviorInterface.cs` holds `IAlleyCampaignBehaviorInterface` and `AlleyCampaignBehaviorInterface`.
- Register the type in the owning Autofac module (or confirm it's already registered) so consumers resolve it from the container instead of constructing it.
- Write new shared logic as a **transient DI class** (an interface plus an `InstancePerDependency` registration, constructor-injected), not a `static` helper, because transient dependencies are much easier to fake in tests. `AgentFormationAssigner` started as a static class and was asked in review to become an injected transient. Existing static helpers like `AgentAiWaker` predate this and can stay.
- The battle sub-services `PuppetSpawner`, `ReinforcementFielder`, and `BattleAuthorityMigrator` are `new`ed in `CoopBattleController`. For a helper they share, register it in `MissionModule` and inject it into `CoopBattleController`, which hands it to the services it constructs.

## Null Checks

- Plumbing uses the statement form `if (x == null) throw ...`, not the expression form `x ?? throw new ArgumentNullException(...)`.
- Data and message types just assign.

## Campaign Behaviors

- Let vanilla `RegisterEvents` register its listeners, and Harmony-patch the individual event-handler methods to control which side runs them, with the positive role check on each patch (for example, `OnSessionLaunched => ModInformation.IsClient`).
- Don't patch `RegisterEvents` to rebuild the listener list with reflection or hand-made delegates, unless individual handler patches can't express the behavior.

## Comments

- One line stating the non-obvious why is the norm. Two or three lines are fine when the mechanism needs them; never write a 6+ line block.
- Put longer rationale in the commit message, where `git blame` surfaces it, instead of narrating vanilla behavior in a patch comment.
- Don't write `->`/`→` arrow chains.

## Files and Tooling

- `.cs` files are UTF-8 with a BOM. Files written through the editor have no BOM, so add one after creating a file, and only once, because a second pass doubles it.
- An on-save formatter may rewrite a file, so re-read it before a follow-up edit.
- `GameInterface` targets netstandard2.0, so newer BCL conveniences such as plain `Dictionary.TryAdd` aren't available.
- CodeFactor flags `new T[0]` (use `Array.Empty<T>()`) and unparenthesized mixed-operator arithmetic (write `4L + ((long)n * 4)`). Its `Complex method` note is advisory, so don't contort code to satisfy it.
