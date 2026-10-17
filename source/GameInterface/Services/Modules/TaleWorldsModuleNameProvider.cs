using System;
using System.Linq;

namespace GameInterface.Services.Modules;

/// <summary>
/// Looks up the display name the launcher shows for a module, for player-facing text only.
/// Kept apart from <see cref="ModuleInfo"/> so names never take part in module compatibility.
/// </summary>
public interface IModuleNameProvider
{
    /// <summary>The module's display name, or null when it is not active or has no name.</summary>
    string GetDisplayName(string moduleId);
}

/// <inheritdoc cref="IModuleNameProvider"/>
public class TaleWorldsModuleNameProvider : IModuleNameProvider
{
    public string GetDisplayName(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        return TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()
            .FirstOrDefault(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }
}
