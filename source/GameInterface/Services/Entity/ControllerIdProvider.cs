using Common.Logging;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.PlatformService;

namespace GameInterface.Services.Entity;

public interface IControllerIdProvider
{
    string ControllerId { get; }
    void SetControllerId(string controllerId);
    void SetControllerAsPlatformId();
    void SetControllerFromProgramArgs();
}

public class ControllerIdProvider : IControllerIdProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControllerIdProvider>();
    private readonly IControllerIdStore controllerIdStore;

    public string ControllerId { get; private set; }

    public ControllerIdProvider() : this(new ControllerIdStore())
    {
    }

    public ControllerIdProvider(IControllerIdStore controllerIdStore)
    {
        if (controllerIdStore == null) throw new ArgumentNullException(nameof(controllerIdStore));

        this.controllerIdStore = controllerIdStore;
    }

    public void SetControllerFromProgramArgs()
    {
        try
        {
            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();

            var platformArgIndex = args.FindIndex(x => x.ToLower() == "/platformid");

            ControllerId = args[platformArgIndex + 1];
        }
        catch(Exception)
        {
            SetAsDefault();
        }        
    }

    public void SetControllerAsPlatformId()
    {
        SetControllerAsPlatformId(PlatformServices.ProviderName, PlatformServices.UserId);
    }

    internal void SetControllerAsPlatformId(string providerName, string platformUserId)
    {
        string provider = NormalizeProviderName(providerName);

        if (IsUsablePlatformId(platformUserId))
        {
            ControllerId = provider + ":" + platformUserId.Trim();
            return;
        }

        string installationId = controllerIdStore.GetOrCreateId();
        ControllerId = provider + ":local:" + installationId;
        Logger.Warning(
            "Platform {Provider} returned no usable user id; using persistent installation id",
            provider);
    }

    public void SetControllerId(string controllerId)
    {
        ControllerId = controllerId;
    }

    public void SetAsDefault()
    {
        ControllerId = "DefaultId";
    }

    private static string NormalizeProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return "unknown";

        string normalized = new string(providerName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
    }

    private static bool IsUsablePlatformId(string platformUserId)
    {
        if (string.IsNullOrWhiteSpace(platformUserId)) return false;

        string trimmed = platformUserId.Trim();
        return !ulong.TryParse(trimmed, out ulong numericId) || numericId != 0;
    }
}
