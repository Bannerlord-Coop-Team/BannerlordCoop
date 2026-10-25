using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Runs cancellation as the owner and retains blocked cancellations for this session.</summary>
public interface IIssueCancellationCoordinator
{
    bool BeforeCancellation(IssueBase issue, TextObject log, out IDisposable owner);
    bool BeforeGiverDeath(Hero victim, out IDisposable owner);
    void CancelForPlayerDeath(string controllerId, Hero oldHero, MobileParty originalParty);
    void Retry();
}
