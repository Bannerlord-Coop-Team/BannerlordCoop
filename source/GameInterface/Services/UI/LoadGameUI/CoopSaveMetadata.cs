using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace Coop.UI.LoadGameUI;

/// <summary>
/// Identifies campaign saves created with the Coop module enabled.
/// </summary>
public static class CoopSaveMetadata
{
    private const string CoopModuleId = "Coop";

    /// <summary>
    /// Checks whether the save metadata records the Coop module id.
    /// </summary>
    public static bool ContainsCoopModule(MetaData metadata)
    {
        return metadata?.GetModules().Contains(CoopModuleId, StringComparer.Ordinal) == true;
    }
}
