using Common;
using Common.Logging;
using GameInterface.Configuration;
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
    /// <summary>
    /// Whether NavalDLC is enabled, the build's feature flag is on, it compiled the naval code and both naval assemblies shipped.
    /// The hosting side also needs <c>modOptions.warSails</c> to match NavalDLC, otherwise it gets a refusal.
    /// </summary>
    NavalSupportDecision Decide();
}

/// <inheritdoc cref="INavalSupportDetector"/>
public class NavalSupportDetector : INavalSupportDetector
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalSupportDetector>();

    private readonly IModuleInfoProvider moduleInfoProvider;
    private readonly INavalFeatureFlag navalFeatureFlag;
    private readonly Func<string, bool> isShipped;
    private readonly IWarSailsSetting warSailsSetting;
    private readonly bool isHost;

    // Takes each input separately so tests can turn the module, the flags, the assemblies and the side on and off.
    public NavalSupportDetector(IModuleInfoProvider moduleInfoProvider, INavalFeatureFlag navalFeatureFlag,
        Func<string, bool> isShipped, IWarSailsSetting warSailsSetting, bool isHost)
    {
        this.moduleInfoProvider = moduleInfoProvider;
        this.navalFeatureFlag = navalFeatureFlag;
        this.isShipped = isShipped;
        this.warSailsSetting = warSailsSetting;
        this.isHost = isHost;
    }

    // The startup composition: this build's flags, the naval assemblies next to assemblyDirectory and, when hosting, mod-config.json.
    public static NavalSupportDetector ForStartup(IModuleInfoProvider moduleInfoProvider, string assemblyDirectory, bool isHost)
    {
        return new NavalSupportDetector(moduleInfoProvider, new NavalFeatureFlag(),
            file => File.Exists(Path.Combine(assemblyDirectory, file)), new WarSailsSetting(new ModConfig()), isHost);
    }

    /// <inheritdoc/>
    public NavalSupportDecision Decide()
    {
        bool navalModuleEnabled = moduleInfoProvider.GetModuleInfos()
            .Any(module => ModInformation.IsNavalDlcModule(module.Id, module.IsDlc));
        bool supported = navalFeatureFlag.IsNavalCodeBuilt
            && ModInformation.IsNavalSupported(navalModuleEnabled, navalFeatureFlag.IsEnabled, isShipped);

        if (isHost) return DecideForHost(navalModuleEnabled, supported);

        // A client has nothing to configure; the server's module list decides whether it may join.
        if (supported || !navalModuleEnabled) return new NavalSupportDecision(supported);

        Logger.Warning("NavalDLC is enabled but {Reason}, so NavalDLC is treated as unsupported", UnsupportedReason());
        return new NavalSupportDecision(false);
    }

    // The engine's module list is fixed at launch, so a warSails value that doesn't match it can only refuse to host.
    private NavalSupportDecision DecideForHost(bool navalModuleEnabled, bool supported)
    {
        bool warSails = warSailsSetting.IsEnabled;
        if (!warSails && !navalModuleEnabled) return new NavalSupportDecision(false);

        if (!warSails)
            return Refuse("NavalDLC is enabled but warSails is false in mod-config.json. " +
                "Disable NavalDLC in the launcher, or set warSails to true.");
        if (!navalModuleEnabled)
            return Refuse("warSails is true in mod-config.json but NavalDLC is not enabled. " +
                "Enable NavalDLC in the launcher, or set warSails to false.");
        if (!supported)
            return Refuse($"warSails is true in mod-config.json but {UnsupportedReason()}. " +
                "Use a tester build with War Sails, or set warSails to false and disable NavalDLC.");

        return new NavalSupportDecision(true);
    }

    // Logs the hosting refusal once at startup and returns it with naval off.
    private static NavalSupportDecision Refuse(string reason)
    {
        Logger.Error("War Sails mismatch, this process will refuse to host: {Reason}", reason);
        return new NavalSupportDecision(false, reason);
    }

    // Names the first missing requirement; naval assemblies left by an earlier deploy don't count for a build without naval code.
    private string UnsupportedReason()
    {
        if (!navalFeatureFlag.IsEnabled) return "War Sails co-op is only in tester builds";
        if (!navalFeatureFlag.IsNavalCodeBuilt) return "this build was made without CoopNavalDlc";
        return "this build has no naval code";
    }
}
