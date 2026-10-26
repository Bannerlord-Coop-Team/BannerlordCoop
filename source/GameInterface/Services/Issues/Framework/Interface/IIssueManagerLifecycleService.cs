using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueManagerLifecycleService
{
    bool TryOpenOwnerScope(IssueBase issue, out IDisposable scope);

    bool TryResolveMainHeroParty(out PartyBase party);
}
