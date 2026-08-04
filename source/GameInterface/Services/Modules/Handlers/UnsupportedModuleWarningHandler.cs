using System;
using System.Linq;
using TaleWorlds.Library;

namespace GameInterface.Services.Modules.Handlers;


/// <summary>
/// Detects active modules that are unsupported by COOP and prepare
/// a one-time informational warning for the client.
/// </summary>
public class UnsupportedModuleWarningHandler
{
    public const string PromptTitle = "Additional Modules Detected";

    private const string CoopModuleId = "Coop";
    private const string DedicatedServerModulePrefix = "DedicatedServer.";
    
    private readonly IModuleInfoProvider moduleInfoProvider;

    private bool modulesEvaluated;
    private bool promptShown;
    private string[] unsupportedModuleIds = Array.Empty<string>();
    
    public UnsupportedModuleWarningHandler(
        IModuleInfoProvider moduleInfoProvider)
    {
        this.moduleInfoProvider = moduleInfoProvider ?? throw new ArgumentNullException(nameof(moduleInfoProvider));
    }

    public void TryShowPrompt(bool canShow, Action<InquiryData> showInquiry)
    {
        if (!canShow || promptShown)
        {
            return;
        }

        EvaluateModules();
        if (unsupportedModuleIds.Length == 0)
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
            BuildPromptText(unsupportedModuleIds),
            true,
            false,
            "Continue",
            string.Empty,
            null,
            null));
    }

    private void EvaluateModules()
    {
        if (modulesEvaluated)
        {
            return;
        }

        modulesEvaluated = true;
        unsupportedModuleIds = moduleInfoProvider.GetModuleInfos()
            .Where(IsUnsupported)
            .Select(module => module.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnsupported(ModuleInfo module)
    {
        if (string.Equals(module.Id, CoopModuleId, StringComparison.OrdinalIgnoreCase))
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

    private static string BuildPromptText(string[] moduleIds)
    {
        return "Bannerlord Coop may be unstable when used with additional modules. " +
               "The following active modules are not supported:\n\n" +
               string.Join("\n", moduleIds.Select(id => "- " + id)) +
               "\n\nContinue at your own risk.";
    }
}