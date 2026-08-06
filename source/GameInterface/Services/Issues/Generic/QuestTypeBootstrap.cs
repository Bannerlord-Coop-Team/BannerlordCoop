using System.Runtime.CompilerServices;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Forces every migrated quest type's own static <c>*QuestType</c> class (e.g. a hypothetical
/// <c>Migrated.SomeQuestType.SomeQuestTypeQuestType</c>) to run its static constructor - and therefore its
/// <see cref="QuestTypeRegistry.Register"/> call - exactly once, at mod boot, before any Harmony patch that
/// consults <see cref="QuestTypeRegistry"/> can run.
///
/// Without this, a <c>*QuestType</c> class (a plain static class, never itself a <c>[HarmonyPatch]</c> class) is
/// only lazily initialized whenever something else first references one of its static members. Because dispatch
/// is registry-driven (see <c>Dispatch.GenericQuestTypeDispatchPatches</c>), the first thing that ever needs to
/// see a registration is <see cref="QuestTypeRegistry"/> itself, which has no compile-time reference to any
/// concrete migrated type - so nothing forces the registration to happen early enough unless this file does it
/// explicitly.
///
/// Called once from <see cref="GameInterface.PatchAll"/>. Idempotent and cheap to call more than once
/// (<see cref="RuntimeHelpers.RunClassConstructor"/> is a no-op after a type's static constructor has already
/// run once in this process) - safe for every test harness that spins up more than one
/// <c>IGameInterface</c>/<c>Harmony</c> instance in the same process.
///
/// No quest types are migrated onto the generic handler yet - this is the framework alone, landing ahead of
/// its consumers (see the project's PR sequencing). The method body is intentionally empty until the first
/// migrated-type PR adds its own <c>RuntimeHelpers.RunClassConstructor(typeof(...).TypeHandle)</c> line here;
/// the call site in <see cref="GameInterface.PatchAll"/> is already wired up so that PR only needs to touch
/// this one file.
/// </summary>
internal static class QuestTypeBootstrap
{
    internal static void EnsureAllMigratedTypesRegistered()
    {
        // Add one line here for every future migrated type:
        // RuntimeHelpers.RunClassConstructor(typeof(Migrated.SomeQuestType.SomeQuestTypeQuestType).TypeHandle);
    }
}
