using Common.Logging;
using Serilog;
using System;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;

/// <summary>Repairs missing default policy references after the campaign loads its culture data.</summary>
internal interface ICultureDefaultPolicyRepairer
{
    void Repair(MBObjectManager objectManager, bool ignoreGameTypeInclusionCheck, string gameType);
}

/// <summary>Restores unresolved culture policy references from the merged campaign XML.</summary>
internal class CultureDefaultPolicyRepairer : ICultureDefaultPolicyRepairer
{
    internal const string CultureXmlId = "SPCultures";

    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicyRepairer>();
    private readonly Func<bool, string, XmlDocument> loadCultureXml;
    private readonly Func<MBObjectManager, string, PolicyObject> resolvePolicy;

    public CultureDefaultPolicyRepairer()
        : this(
            (ignoreGameType, gameType) => MBObjectManager.GetMergedXmlForManaged(
                CultureXmlId, skipValidation: false, ignoreGameTypeInclusionCheck: ignoreGameType, gameType: gameType),
            (objectManager, policyId) => objectManager.GetObject<PolicyObject>(policyId))
    {
    }

    internal CultureDefaultPolicyRepairer(
        Func<bool, string, XmlDocument> loadCultureXml,
        Func<MBObjectManager, string, PolicyObject> resolvePolicy)
    {
        this.loadCultureXml = loadCultureXml;
        this.resolvePolicy = resolvePolicy;
    }

    public void Repair(MBObjectManager objectManager, bool ignoreGameTypeInclusionCheck, string gameType)
    {
        var affectedCultures = objectManager.GetObjectTypeList<CultureObject>()
            .Where(culture => culture.DefaultPolicyList?.Any(policy => policy == null) == true)
            .ToArray();
        if (affectedCultures.Length == 0) return;

        XmlDocument xml;
        try
        {
            xml = loadCultureXml(ignoreGameTypeInclusionCheck, gameType);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Could not reload culture XML to repair unresolved default policies");
            return;
        }

        var definitions = xml.SelectNodes("/SPCultures/Culture").Cast<XmlElement>().ToArray();
        foreach (var culture in affectedCultures)
        {
            var definition = definitions.FirstOrDefault(node => node.GetAttribute("id") == culture.StringId);
            RepairCulture(objectManager, culture, definition);
        }
    }

    private void RepairCulture(MBObjectManager objectManager, CultureObject culture, XmlElement definition)
    {
        var policyNodes = definition?.SelectNodes("default_policies/policy");
        if (policyNodes == null || policyNodes.Count == 0)
        {
            Logger.Error("Could not repair default policies for culture {CultureId}: no policy definitions in culture XML", culture.StringId);
            return;
        }

        var policies = new MBList<PolicyObject>();
        foreach (XmlElement node in policyNodes)
        {
            var policyId = node.GetAttribute("id");
            var policy = resolvePolicy(objectManager, policyId);
            if (policy == null)
            {
                Logger.Error("Could not repair default policies for culture {CultureId}: policy {PolicyId} is unresolved", culture.StringId, policyId);
                return;
            }
            policies.Add(policy);
        }

        culture._defaultPolicyList = policies;
    }
}
