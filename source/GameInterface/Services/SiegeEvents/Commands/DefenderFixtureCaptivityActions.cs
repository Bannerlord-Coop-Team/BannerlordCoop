#if DEBUG
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Commands;

public interface IDefenderFixtureCaptivityActions
{
    void Release(Hero hero);
    void Recapture(PartyBase captor, Hero hero);
}

public sealed class DefenderFixtureCaptivityActions : IDefenderFixtureCaptivityActions
{
    public void Release(Hero hero) => EndCaptivityAction.ApplyByEscape(hero);

    public void Recapture(PartyBase captor, Hero hero) => TakePrisonerAction.Apply(captor, hero);
}
#endif
