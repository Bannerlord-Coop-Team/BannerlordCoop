using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.RomanceFlow;

public interface IPlayerDivorceConfirmation
{
    bool CanDivorce(Hero playerHero, Hero spouse);
    InquiryData CreateInquiry(Hero playerHero, Hero spouse);
}

/// <summary>Confirms divorce before sending the selected marriage to the server.</summary>
internal class PlayerDivorceConfirmation : IPlayerDivorceConfirmation
{
    private readonly IPlayerDivorceRules rules;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;

    public PlayerDivorceConfirmation(IPlayerDivorceRules rules, IMessageBroker messageBroker, IPlayerManager playerManager)
    {
        this.rules = rules;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
    }

    public bool CanDivorce(Hero playerHero, Hero spouse) => rules.CanDivorce(playerHero, spouse, out _);

    public InquiryData CreateInquiry(Hero playerHero, Hero spouse)
    {
        bool sharedPlayerClan = playerManager.Contains(spouse) && playerHero.Clan != null && playerHero.Clan == spouse.Clan;
        var description = GameTexts.FindText("str_coop_divorce_description")
            .SetTextVariable("HERO", spouse.Name).ToString();
        description += "\n\n" + GameTexts.FindText(sharedPlayerClan
            ? "str_coop_divorce_shared_clan" : "str_coop_divorce_keep_clans");

        return new InquiryData(
            GameTexts.FindText("str_coop_divorce_title").SetTextVariable("HERO", spouse.Name).ToString(),
            description, true, true,
            GameTexts.FindText("str_coop_divorce").ToString(), GameTexts.FindText("str_cancel").ToString(),
            () =>
            {
                if (CanDivorce(playerHero, spouse))
                    messageBroker.Publish(this, new PlayerDivorceRequested(playerHero, spouse));
            }, null);
    }
}
