using Common.Logging;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CultureObjects;

/// <summary>Repairs the null entries left by unregistered policy IDs in culture XML (#3337).</summary>
internal interface ICultureDefaultPolicyRepairer
{
    /// <summary>Removes unresolved default policies from the loaded cultures.</summary>
    void Repair(IEnumerable<CultureObject> cultures);
}

/// <summary>Removes null policy references after culture deserialization (#3337).</summary>
internal class CultureDefaultPolicyRepairer : ICultureDefaultPolicyRepairer
{
    internal const string CultureXmlId = "SPCultures";

    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicyRepairer>();

    public void Repair(IEnumerable<CultureObject> cultures)
    {
        foreach (var culture in cultures)
        {
            var policies = culture._defaultPolicyList;
            if (policies == null) continue;

            int removed = policies.RemoveAll(policy => policy == null);
            if (removed > 0)
            {
                Logger.Warning("Removed {Count} unresolved default policy references from culture {CultureId}", removed, culture.StringId);
            }
        }
    }
}
