using Common;
using Common.Logging;
using Serilog;
using System;
using System.IO;
using System.Linq;

namespace GameInterface.Services.Modules;

/// <summary>
/// Decides once at startup whether War Sails co-op runs in this process; the result becomes <see cref="ModInformation.IsNavalDlcActive"/>.
/// </summary>
public interface INavalSupportDetector
{
    /// <summary>Whether NavalDLC is enabled, the build's feature flag is on, it compiled the naval code and both naval assemblies shipped.</summary>
    bool Detect();
}

/// <inheritdoc cref="INavalSupportDetector"/>
public class NavalSupportDetector : INavalSupportDetector
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalSupportDetector>();

    private readonly IModuleInfoProvider moduleInfoProvider;
    private readonly INavalFeatureFlag navalFeatureFlag;
    private readonly Func<string, bool> isShipped;

    // Takes each input separately so tests can turn the module, the flag and the assemblies on and off.
    public NavalSupportDetector(IModuleInfoProvider moduleInfoProvider, INavalFeatureFlag navalFeatureFlag, Func<string, bool> isShipped)
    {
        this.moduleInfoProvider = moduleInfoProvider;
        this.navalFeatureFlag = navalFeatureFlag;
        this.isShipped = isShipped;
    }

    // The startup composition: this build's TESTER and CoopNavalDlc flags and the naval assemblies next to assemblyDirectory.
    public static NavalSupportDetector ForStartup(IModuleInfoProvider moduleInfoProvider, string assemblyDirectory)
    {
        return new NavalSupportDetector(moduleInfoProvider, new NavalFeatureFlag(),
            file => File.Exists(Path.Combine(assemblyDirectory, file)));
    }

    /// <inheritdoc/>
    public bool Detect()
    {
        bool navalModuleEnabled = moduleInfoProvider.GetModuleInfos()
            .Any(module => ModInformation.IsNavalDlcModule(module.Id, module.IsDlc));
        bool supported = navalFeatureFlag.IsNavalCodeBuilt
            && ModInformation.IsNavalSupported(navalModuleEnabled, navalFeatureFlag.IsEnabled, isShipped);
        if (supported || !navalModuleEnabled) return supported;

        Logger.Warning("NavalDLC is enabled but {Reason}, so NavalDLC is treated as unsupported", UnsupportedReason());
        return false;
    }

    // Names the first missing requirement; naval assemblies left by an earlier deploy don't count for a build without naval code.
    private string UnsupportedReason()
    {
        if (!navalFeatureFlag.IsEnabled) return "War Sails co-op is only in tester builds";
        if (!navalFeatureFlag.IsNavalCodeBuilt) return "this build was made without CoopNavalDlc";
        return "this build has no naval code";
    }
}
