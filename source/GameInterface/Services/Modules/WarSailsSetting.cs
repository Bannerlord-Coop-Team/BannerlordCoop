using GameInterface.Configuration;

namespace GameInterface.Services.Modules;

/// <summary>
/// Whether the hosting side's mod-config.json asks for War Sails (NavalDLC) co-op.
/// </summary>
public interface IWarSailsSetting
{
    /// <summary>The <c>modOptions.warSails</c> value, false when the key is absent.</summary>
    bool IsEnabled { get; }
}

/// <inheritdoc cref="IWarSailsSetting"/>
public class WarSailsSetting : IWarSailsSetting
{
    private readonly IModConfig modConfig;

    // Reads the flag from the given config on each access.
    public WarSailsSetting(IModConfig modConfig)
    {
        this.modConfig = modConfig;
    }

    /// <inheritdoc/>
    public bool IsEnabled => modConfig.Data.ModOptions?.WarSails ?? false;
}
