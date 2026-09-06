using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public interface IClanJoinConfirmation : IGameAbstraction
{
    InquiryData CreateInquiry(Hero joiningHero, Clan targetClan, Action onConfirm, Action onCancel);
}

public class ClanJoinConfirmation : IClanJoinConfirmation
{
    private readonly IClanJoinRules rules;

    public ClanJoinConfirmation(IClanJoinRules rules)
    {
        this.rules = rules;
    }

    public InquiryData CreateInquiry(Hero joiningHero, Clan targetClan, Action onConfirm, Action onCancel)
    {
        var paragraphs = new List<string>
        {
            GameTexts.FindText("str_coop_clan_join_description")
                .SetTextVariable("CLAN_NAME", targetClan.Name)
                .SetTextVariable("LEADER_NAME", targetClan.Leader.Name).ToString(),
        };
        var warnings = rules.GetWarnings(joiningHero, targetClan);
        if (warnings.Count > 0)
        {
            paragraphs.Add(string.Join("\n", warnings.Select(warning => warning.ToString())));
            paragraphs.Add(GameTexts.FindText("str_coop_clan_join_forfeit_warning").ToString());
        }

        return new InquiryData(
            GameTexts.FindText("str_coop_clan_join_title").SetTextVariable("CLAN_NAME", targetClan.Name).ToString(),
            string.Join("\n\n", paragraphs),
            true,
            true,
            GameTexts.FindText("str_coop_clan_join_confirm").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            onConfirm,
            onCancel);
    }
}
