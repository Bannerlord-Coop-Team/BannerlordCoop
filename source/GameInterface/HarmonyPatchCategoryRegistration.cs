using HarmonyLib;
using System.Reflection;

namespace GameInterface;

/// <summary>
/// Describes an assembly-scoped Harmony patch category contributed through dependency injection.
/// </summary>
public sealed class HarmonyPatchCategoryRegistration
{
    public Assembly Assembly { get; }
    public string Category { get; }

    public HarmonyPatchCategoryRegistration(Assembly assembly, string category)
    {
        Assembly = assembly;
        Category = category;
    }

    /// <summary>Applies every patch in the assembly without a category, like GameInterface's own patches.</summary>
    public static HarmonyPatchCategoryRegistration Uncategorized(Assembly assembly) =>
        new HarmonyPatchCategoryRegistration(assembly, null);

    public void Apply(Harmony harmony)
    {
        if (Category == null)
        {
            harmony.PatchAllUncategorized(Assembly);
            return;
        }

        harmony.PatchCategory(Assembly, Category);
    }
}
