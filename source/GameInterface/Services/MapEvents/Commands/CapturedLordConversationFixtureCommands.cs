#if DEBUG
using Common;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal static class CapturedLordConversationFixtureCommands
{
    private const string CaptureOptionId = "talk_lord_defeat_to_lord_capture";

    [CommandLineArgumentFunction("capture_defeated_lord", "coop.debug.mapevent")]
    public static string CaptureDefeatedLord(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client at the captured-lord conversation.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.capture_defeated_lord";
        if (Campaign.Current?.CurrentConversationContext != ConversationContext.CapturedLord)
            return "The captured-lord conversation is not active.";

        ConversationManager conversationManager = Campaign.Current.ConversationManager;
        if (!conversationManager.IsConversationInProgress)
            return "The captured-lord conversation is not in progress.";

        var option = conversationManager.CurOptions?.FirstOrDefault(candidate => candidate.Id == CaptureOptionId);
        if (!option.HasValue)
            return $"The captured-lord option {CaptureOptionId} is not available.";
        if (!option.Value.IsClickable)
            return $"The captured-lord option {CaptureOptionId} is not clickable.";

        Hero capturedHero = Hero.OneToOneConversationHero;
        conversationManager.DoOption(CaptureOptionId);
        if (conversationManager.IsConversationInProgress)
            conversationManager.DoOptionContinue();
        if (conversationManager.IsConversationInProgress)
            conversationManager.ContinueConversation();
        if (conversationManager.IsConversationInProgress)
            return "The captured-lord conversation did not close after selecting capture.";

        return $"Captured-lord dialogue completed: hero={capturedHero?.StringId ?? "none"} option={CaptureOptionId}.";
    }
}
#endif
