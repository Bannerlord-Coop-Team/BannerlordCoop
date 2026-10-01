using System;
using System.Collections.Generic;
using GameInterface.Services.Clans.Data;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public interface IClanJoinConfirmation : IGameAbstraction
{
    InquiryData CreateInquiry(Hero joiningHero, Clan targetClan, Action onConfirm, Action onCancel,
        ClanJoinConfirmationContext context = ClanJoinConfirmationContext.JoinRequest);
}

public class ClanJoinConfirmation : IClanJoinConfirmation
{
    private readonly IClanJoinRules rules;

    public ClanJoinConfirmation(IClanJoinRules rules)
    {
        this.rules = rules;
    }

    public InquiryData CreateInquiry(Hero joiningHero, Clan targetClan, Action onConfirm, Action onCancel,
        ClanJoinConfirmationContext context = ClanJoinConfirmationContext.JoinRequest)
    {
        var (titleTextId, descriptionTextId, confirmTextId) = context switch
        {
            ClanJoinConfirmationContext.MarriageProposal => ("str_coop_marriage_proposal_title",
                "str_coop_marriage_proposal_description", "str_coop_marriage_send"),
            ClanJoinConfirmationContext.MarriageAcceptance => ("str_coop_marriage_acceptance_title",
                "str_coop_marriage_acceptance_description", "str_coop_marriage_accept"),
            _ => ("str_coop_clan_join_title", "str_coop_clan_join_description", "str_coop_clan_join_confirm")
        };
        var paragraphs = new List<string>
        {
            GameTexts.FindText("str_coop_clan_experimental_warning").ToString(),
            GameTexts.FindText(descriptionTextId)
                .SetTextVariable("CLAN_NAME", targetClan.Name)
                .SetTextVariable("LEADER_NAME", targetClan.Leader.Name).ToString(),
        };
        var warnings = rules.GetWarnings(joiningHero, targetClan);
        if (warnings.Count > 0)
        {
            paragraphs.Add(string.Join("\n", warnings.Select(warning => warning.ToString())));
            paragraphs.Add(GameTexts.FindText(context == ClanJoinConfirmationContext.JoinRequest
                ? "str_coop_clan_join_forfeit_warning" : "str_coop_marriage_forfeit_warning").ToString());
        }
        else if (context != ClanJoinConfirmationContext.JoinRequest)
        {
            paragraphs.Add(GameTexts.FindText("str_coop_marriage_clan_commitment").ToString());
        }

        return new InquiryData(
            GameTexts.FindText(titleTextId)
                .SetTextVariable("CLAN_NAME", targetClan.Name)
                .SetTextVariable("LEADER_NAME", targetClan.Leader.Name).ToString(),
            string.Join("\n\n", paragraphs),
            true,
            true,
            GameTexts.FindText(confirmTextId).ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            onConfirm,
            onCancel);
    }
}
