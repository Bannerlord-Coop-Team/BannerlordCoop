using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IIssueManagerLifecycleService
{
    bool TryResolveMainHeroParty(out PartyBase party);
}
