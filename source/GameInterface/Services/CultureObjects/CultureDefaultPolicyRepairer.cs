using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CultureObjects;

/// <summary>Repairs the null entries left by unregistered policy IDs in culture XML (#3337).</summary>
internal interface ICultureDefaultPolicyRepairer
{
    /// <summary>Removes unresolved default policies from a loaded culture.</summary>
    void Repair(CultureObject culture);
}

/// <summary>Removes null policy references after culture deserialization (#3337).</summary>
internal class CultureDefaultPolicyRepairer : ICultureDefaultPolicyRepairer
{
    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicyRepairer>();

    public void Repair(CultureObject culture)
    {
        var policies = culture._defaultPolicyList;
        if (policies == null) return;

        int removed = policies.RemoveAll(policy => policy == null);
        if (removed > 0)
        {
            Logger.Warning("Removed {Count} unresolved default policy references from culture {CultureId}", removed, culture.StringId);
        }
    }
}
