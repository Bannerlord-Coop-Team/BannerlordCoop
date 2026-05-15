using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using GameInterface.Services.ObjectManager.Extensions;
using System.Linq;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct IssueBaseSurrogate
{
    [ProtoMember(1)]
    public string Data { get; set; }


    public IssueBaseSurrogate(IssueBase issueBase)
    {
        Data = issueBase?.StringId;

    }
    public static implicit operator IssueBaseSurrogate(IssueBase issueBase)
    {
        return new IssueBaseSurrogate(issueBase);
    }

    public static implicit operator IssueBase(IssueBaseSurrogate surrogate)
    {
        if (string.IsNullOrEmpty(surrogate.Data)) return null;

        var issue = MBObjectManager.Instance.GetObject<IssueBase>(surrogate.Data);

        if (issue == null)
        {
            // Search through heroes since MBObjectManager.Instance.GetObject<IssueBase>(surrogate.Data); returns empty
            return Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Issue != null)
                .Select(h => h.Issue)
                .FirstOrDefault(i => i.StringId == surrogate.Data);
        }
        return issue;
    }
}

