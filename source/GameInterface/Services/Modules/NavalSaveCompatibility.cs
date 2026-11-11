using Common;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Modules;

/// <summary>
/// Refuses to host a save whose NavalDLC status, fixed when it was created, differs from this process's War Sails decision.
/// </summary>
public interface INavalSaveCompatibility
{
    /// <summary>Null when the save can be hosted, otherwise a reason naming the save and the fix.</summary>
    string GetHostRefusal(string saveName, IEnumerable<string> saveModuleIds);
}

/// <inheritdoc cref="INavalSaveCompatibility"/>
public class NavalSaveCompatibility : INavalSaveCompatibility
{
    /// <inheritdoc/>
    public string GetHostRefusal(string saveName, IEnumerable<string> saveModuleIds)
    {
        bool savedWithNaval = (saveModuleIds ?? Enumerable.Empty<string>()).Contains(ModInformation.NavalDlcModuleId);
        if (savedWithNaval == ModInformation.IsNavalEnabled) return null;

        return ModInformation.IsNavalEnabled
            ? $"Save '{saveName}' was made without NavalDLC and a campaign can't gain it later. Host a save made with NavalDLC, or turn War Sails off."
            : $"Save '{saveName}' was made with NavalDLC, which War Sails off can't load. Host a save made without NavalDLC, or turn War Sails on.";
    }
}
