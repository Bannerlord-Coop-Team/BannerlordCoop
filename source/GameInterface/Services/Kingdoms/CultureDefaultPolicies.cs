using Common.Logging;
using Serilog;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Kingdoms;

/// <summary>Resolves a culture's configured default policies before kingdom creation.</summary>
internal interface ICultureDefaultPolicies
{
    bool TryResolve(CultureObject culture);
}

/// <summary>Repairs unresolved default policy references using the active campaign's culture XML.</summary>
internal class CultureDefaultPolicies : ICultureDefaultPolicies
{
    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicies>();

    public bool TryResolve(CultureObject culture)
    {
        if (culture.DefaultPolicyList != null && culture.DefaultPolicyList.All(policy => policy != null))
        {
            return true;
        }

        // CultureObject.Deserialize keeps null references but discards their XML ids.
        var campaign = Campaign.Current;
        var cultures = MBObjectManager.GetMergedXmlForManaged(
            "SPCultures",
            skipValidation: false,
            ignoreGameTypeInclusionCheck: campaign.IsDevelopment,
            gameType: campaign.GameTypeStringId);
        var cultureNode = cultures?.DocumentElement?.SelectNodes("Culture").Cast<XmlElement>()
            .FirstOrDefault(node => node.GetAttribute("id") == culture.StringId);
        if (cultureNode == null)
        {
            Logger.Warning("Could not find culture {CultureId} in the campaign culture XML", culture.StringId);
            return false;
        }

        var policies = new MBList<PolicyObject>();
        foreach (XmlElement policyNode in cultureNode.SelectNodes("default_policies/policy"))
        {
            var policyId = policyNode.GetAttribute("id");
            var policy = campaign.ObjectManager.GetObject<PolicyObject>(policyId);
            if (policy == null)
            {
                Logger.Warning("Could not resolve default policy {PolicyId} for culture {CultureId}", policyId, culture.StringId);
                return false;
            }
            policies.Add(policy);
        }

        culture._defaultPolicyList = policies;
        return true;
    }
}
