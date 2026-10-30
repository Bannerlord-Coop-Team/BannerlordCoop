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
    /// <summary>Whether NavalDLC is enabled, the build's feature flag is on and both naval assemblies shipped.</summary>
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

    // The startup composition: this build's TESTER flag and the naval assemblies next to assemblyDirectory.
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
        bool supported = ModInformation.IsNavalSupported(navalModuleEnabled, navalFeatureFlag.IsEnabled, isShipped);
        if (supported || !navalModuleEnabled) return supported;

        string reason = navalFeatureFlag.IsEnabled
            ? "this build has no naval code"
            : "War Sails co-op is only in tester builds";
        Logger.Warning("NavalDLC is enabled but {Reason}, so NavalDLC is treated as unsupported", reason);
        return false;
    }
}
