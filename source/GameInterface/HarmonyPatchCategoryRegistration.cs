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

    public void Apply(Harmony harmony)
    {
        harmony.PatchCategory(Assembly, Category);
    }
}
