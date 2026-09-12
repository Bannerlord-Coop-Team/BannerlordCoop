using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// Add an OnClickableCondition to post battle dialogue with lords to block executions based on config.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationExecuteOptionPatch
{
    private const string ExecuteDefeatedLordLineId = "talk_lord_defeat_to_lord_capture_and_kill";

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.AddOtherConversations))]
    [HarmonyPostfix]
    public static void AddOtherConversationsPostfix()
    {
        var targetSentence = Campaign.Current.ConversationManager._sentences.FirstOrDefault(sentence => sentence.Id == ExecuteDefeatedLordLineId);

        // Don't assign clickable condition if the sentence isn't found
        if (targetSentence == null) return;

        targetSentence.OnClickableCondition = ExecuteDefeatedLordOnCondition;
    }

    private static bool ExecuteDefeatedLordOnCondition(out TextObject hint)
    {
        hint = new TextObject("");
        if (!HeroExecutionRules.IsExecutable(Hero.OneToOneConversationHero, out var reason))
        {
            hint = new TextObject(reason);
            return false;
        }

        return true;
    }
}
