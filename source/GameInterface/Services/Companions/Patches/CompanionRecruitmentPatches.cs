using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Companions.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class CompanionRecruitmentPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRecruitmentPatches>();

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_companion_hire_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationCompanionHireOnConsequencesPrefix(ref LordConversationsCampaignBehavior __instance)
    {
        var message = new CompanionHired(
            Hero.MainHero,
            Hero.OneToOneConversationHero,
            Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(Hero.OneToOneConversationHero),
            Clan.PlayerClan,
            MobileParty.MainParty
        );
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
