using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using TaleWorlds.Library;
using GameInterface.Services.UI.CoopOptions;

namespace GameInterface.Services.Modules.Handlers;


/// <summary>
/// Detects active modules that are unsupported by COOP and prepare
/// a one-time informational warning for the client.
/// </summary>
public class UnsupportedModuleWarningHandler
{
    public const string PromptTitle = "Additional Modules Detected";
    public const string TabId = "Warnings";
    public const string SectionId = "UnsupportedModules";

    private const string CoopModuleId = "Coop";
    private const string CoopNightlyModuleId = "CoopNightly";
    private const string DedicatedServerModulePrefix = "DedicatedServer.";
    
    private readonly IModuleInfoProvider moduleInfoProvider;
    private readonly ICoopOptionsStore optionsStore;
    private readonly Action<Exception> reportError;
    
    private bool optionsLoaded;
    private bool warningsSuppressed; 
    private bool modulesEvaluated;
    private bool promptShown;
    private string[] unsupportedModuleLabels = Array.Empty<string>();
    
    public UnsupportedModuleWarningHandler(
        IModuleInfoProvider moduleInfoProvider,
        ICoopOptionsStore optionsStore,
        Action<Exception> reportError)
    {
        this.moduleInfoProvider = moduleInfoProvider ?? throw new ArgumentNullException(nameof(moduleInfoProvider));
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.reportError = reportError;
    }

    public void TryShowPrompt(bool canShow, Action<InquiryData> showInquiry)
    {
        if (!canShow || promptShown)
        {
            return;
        }

        LoadOptions();
        if (warningsSuppressed)
        {
            return;
        }

        EvaluateModules();
        if (unsupportedModuleLabels.Length == 0)
        {
            return;
        }

        if (showInquiry == null)
        {
            throw new ArgumentNullException(nameof(showInquiry));
        }
        
        promptShown = true;
        showInquiry(new InquiryData(
            PromptTitle,
            BuildPromptText(unsupportedModuleLabels),
            true,
            true,
            "Continue",
            "Don't Show Again",
            null,
            SupressWarning));
    }
    
    private void LoadOptions()
    {
        if (optionsLoaded)
        {
            return;
        }
        
        optionsLoaded = true;

        CoopOptionsData options = optionsStore.LoadOrDefault();
        if (options.TryGetSection(
                TabId,
                SectionId,
                out UnsupportedModuleWarningOptions saved))
        {
            warningsSuppressed = saved.DontShowAgain;
        }
    }

    private void SupressWarning()
    {
        warningsSuppressed = true;

        try
        {
            CoopOptionsData options = optionsStore.LoadOrDefault();
            options.SetSection(
                TabId,
                SectionId,
                new UnsupportedModuleWarningOptions
                {
                    DontShowAgain = true,
                });
            optionsStore.Save(options);
        }
        catch (Exception exception)
        {
            reportError?.Invoke(exception);
        }
    }

    private void EvaluateModules()
    {
        if (modulesEvaluated)
        {
            return;
        }

        modulesEvaluated = true;
        var modules = moduleInfoProvider.GetModuleInfos().ToArray();
        var hasConflictingCoopVariants = modules.Any(module => IsCoopVariant(module, CoopModuleId)) &&
                                         modules.Any(module => IsCoopVariant(module, CoopNightlyModuleId));
        unsupportedModuleLabels = modules
            .Where(module => IsUnsupported(module) ||
                             (hasConflictingCoopVariants && IsCoopVariant(module)))
            .Where(module => !string.IsNullOrWhiteSpace(module.Id))
            .GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => GetDisplayLabel(group.First()))
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Players know mods by the launcher name, the id still matches the folder in Modules.
    private static string GetDisplayLabel(ModuleInfo module)
    {
        var name = module.Name?.Trim();
        if (string.IsNullOrEmpty(name) || string.Equals(name, module.Id, StringComparison.OrdinalIgnoreCase))
        {
            return module.Id;
        }

        return $"{name} ({module.Id})";
    }

    private static bool IsUnsupported(ModuleInfo module)
    {
        if (IsCoopVariant(module))
        {
            return false;
        }

        if (module.Id?.StartsWith(
                DedicatedServerModulePrefix,
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        return !module.IsOfficial || module.IsDlc;
    }

    private static bool IsCoopVariant(ModuleInfo module, string moduleId = null)
    {
        return moduleId == null
            ? string.Equals(module.Id, CoopModuleId, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(module.Id, CoopNightlyModuleId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPromptText(string[] moduleLabels)
    {
        return "Bannerlord Coop may be unstable when used with additional modules. " +
               "The following active modules are not supported:\n\n" +
               string.Join("\n", moduleLabels.Select(label => "- " + label)) +
               "\n\nContinue at your own risk.";
    }
}
/// <summary>
/// Stores the client's preference for the unsupported module startup warning.
/// </summary>
public class UnsupportedModuleWarningOptions
{
    [JsonPropertyName("dontShowAgain")]
    public bool DontShowAgain {get; set;}
}
