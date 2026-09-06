using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans;

public interface IClanJoinRules : IGameAbstraction
{
    IReadOnlyList<TextObject> GetWarnings(Hero joiningHero, Clan targetClan);
    void Apply(Hero joiningHero, Clan targetClan);
}

public class ClanJoinRules : IClanJoinRules
{
    private readonly IPlayerManager playerManager;

    public ClanJoinRules(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    public IReadOnlyList<TextObject> GetWarnings(Hero joiningHero, Clan targetClan)
    {
        var warnings = new List<TextObject>();
        var clan = joiningHero.Clan;
        AddWarning(warnings, "str_coop_clan_join_companions", clan.Companions.Count);
        AddWarning(warnings, "str_coop_clan_join_workshops", clan.Heroes.Sum(hero => hero.OwnedWorkshops.Count));
        AddWarning(warnings, "str_coop_clan_join_caravans", clan.Heroes.Sum(hero => hero.OwnedCaravans.Count));
        AddWarning(warnings, "str_coop_clan_join_parties", clan.WarPartyComponents.Count(party => !playerManager.Contains(party.MobileParty)));
        AddWarning(warnings, "str_coop_clan_join_alleys", clan.Heroes.Sum(hero => hero.OwnedAlleys.Count));
        AddWarning(warnings, "str_coop_clan_join_supporters", clan.SupporterNotables.Count);
        return warnings;
    }

    public void Apply(Hero joiningHero, Clan targetClan)
    {
        // TODO: Apply asset and family rules on the server before changing the hero's clan.
    }

    private void AddWarning(List<TextObject> warnings, string textId, int count)
    {
        if (count > 0)
            warnings.Add(GameTexts.FindText(textId).SetTextVariable("COUNT", count));
    }
}
