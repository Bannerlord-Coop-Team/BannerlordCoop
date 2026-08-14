using System;
using TaleWorlds.ModuleManager;

namespace GameInterface.Services.Modules;

public interface ICoopModulePathResolver
{
    string GetXmlPath(string xmlName);
}

internal sealed class CoopModulePathResolver : ICoopModulePathResolver
{
    internal const string StableModuleId = "Coop";
    internal const string NightlyModuleId = "CoopNightly";

    private readonly Func<string, bool> isModuleActive;
    private readonly Func<string, string, string> getXmlPath;

    public CoopModulePathResolver()
        : this(ModuleHelper.IsModuleActive, ModuleHelper.GetXmlPath)
    {
    }

    internal CoopModulePathResolver(
        Func<string, bool> isModuleActive,
        Func<string, string, string> getXmlPath)
    {
        if (isModuleActive == null) throw new ArgumentNullException(nameof(isModuleActive));
        if (getXmlPath == null) throw new ArgumentNullException(nameof(getXmlPath));

        this.isModuleActive = isModuleActive;
        this.getXmlPath = getXmlPath;
    }

    public string GetXmlPath(string xmlName)
    {
        if (isModuleActive(StableModuleId))
            return getXmlPath(StableModuleId, xmlName);

        if (isModuleActive(NightlyModuleId))
            return getXmlPath(NightlyModuleId, xmlName);

        return null;
    }
}
