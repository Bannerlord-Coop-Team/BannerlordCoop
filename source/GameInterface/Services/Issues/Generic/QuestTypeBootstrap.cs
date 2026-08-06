using System.Runtime.CompilerServices;

namespace GameInterface.Services.Issues.Generic;

// Forces every migrated quest type's static ctor (and therefore its QuestTypeRegistry.Register call) to run
// before any registry-driven dispatch check can fire - registration would otherwise stay lazy since dispatch
// has no compile-time reference to any concrete migrated type. Called once from GameInterface.PatchAll.
internal static class QuestTypeBootstrap
{
    internal static void EnsureAllMigratedTypesRegistered()
    {
        // Add one line here for every future migrated type:
        // RuntimeHelpers.RunClassConstructor(typeof(Migrated.SomeQuestType.SomeQuestTypeQuestType).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Migrated.VillageNeedsTools.VillageNeedsToolsQuestType).TypeHandle);
    }
}
