using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace GameInterface.Services.Modules;

[Serializable]
public struct ModuleInfo
{
    public string Id { get; set; }
    public bool IsOfficial { get; set; }
    public ApplicationVersion Version { get; set; }

    public ModuleInfo(string id, bool isOfficial, ApplicationVersion version)
    {
        Id = id;
        IsOfficial = isOfficial;
        Version = version;
    }
}

public interface IModuleInfoProvider
{
    List<ModuleInfo> GetModuleInfos();
}

// TODO move to module interface
public class TaleWorldsModuleInfoProvider : IModuleInfoProvider
{
    public List<ModuleInfo> GetModuleInfos()
    {
        var modules = TaleWorlds.ModuleManager.ModuleHelper.GetModules();
        var moduleInfos = new List<ModuleInfo>();
        foreach (TaleWorlds.ModuleManager.ModuleInfo moduleInfo in modules)
        {
            if (!moduleInfo.IsSelected)
                continue;
            moduleInfos.Add(new ModuleInfo(moduleInfo.Id, moduleInfo.IsOfficial, moduleInfo.Version));
        }

        return moduleInfos;
    }
}
public class CompatibilityInfo
{
    public List<ModuleInfo> Modules { get; private set; } = new List<ModuleInfo>();
    public static IModuleInfoProvider ModuleProvider { get; set; }

    static CompatibilityInfo()
    {
        ModuleProvider = new TaleWorldsModuleInfoProvider();
    }

    public static CompatibilityInfo Get()
    {
        CompatibilityInfo info = new CompatibilityInfo();

        if (ModuleProvider == null)
            return info;

        foreach (ModuleInfo moduleInfo in ModuleProvider.GetModuleInfos())
        {
            info.AddModule(moduleInfo);
        }

        return info;
    }

    public ApplicationVersion GameVersion()
    {
        return Modules.Find(m => m.IsOfficial).Version;
    }

    public bool CompatibleWith(CompatibilityInfo other)
    {
        foreach (ModuleInfo ourModule in Modules)
        {
            if (!other.Modules.Contains(ourModule))
                return false;
        }
        return true;
    }

    public bool GameVersionMatches(CompatibilityInfo other)
    {
        return GameVersion().Equals(other.GameVersion());
    }

    public override bool Equals(object obj)
    {
        if (obj is CompatibilityInfo == false) return false;

        return CompatibleWith(obj as CompatibilityInfo);
    }

    public override int GetHashCode()
    {
        var hashCode = 0;
        if (Modules == null || !Modules.Any())
        {
            return hashCode;
        }

        foreach (var module in Modules)
        {
            hashCode += module.GetHashCode();
        }

        return hashCode;
    }

    private void AddModule(ModuleInfo moduleInfo)
    {
        Modules.Add(moduleInfo);
    }
}
