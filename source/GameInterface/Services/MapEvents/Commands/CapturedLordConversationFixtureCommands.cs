#if DEBUG
using Autofac;
using Common;
using GameInterface.Services.MapEvents.Interfaces;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal static class CapturedLordConversationFixtureCommands
{
    private const string CaptureOptionId = "talk_lord_defeat_to_lord_capture";

    [CommandLineArgumentFunction("advance_battle_rewards", "coop.debug.mapevent")]
    public static string AdvanceBattleRewards(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client with staged battle rewards.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.advance_battle_rewards";

        PlayerEncounter playerEncounter = PlayerEncounter.Current;
        if (playerEncounter == null)
            return "No active player encounter.";
        if (playerEncounter.EncounterState != PlayerEncounterState.CaptureHeroes)
            return $"Battle rewards are not staged at CaptureHeroes: {playerEncounter.EncounterState}.";

        int pendingPrisoners = playerEncounter.RosterToReceiveLootPrisoners.TotalManCount;
        if (pendingPrisoners == 0)
            return "No pending prisoners are available for the captured-lord flow.";
        if (!ContainerProvider.TryResolve<IPlayerEncounterInterface>(out var playerEncounterInterface))
            return "Unable to resolve the player encounter interface.";

        playerEncounterInterface.UpdateInternalAfterBattle(playerEncounter);

        ConversationManager conversationManager = Campaign.Current.ConversationManager;
        if (!conversationManager.IsConversationInProgress ||
            Campaign.Current.CurrentConversationContext != ConversationContext.CapturedLord)
        {
            return "The post-battle state machine did not open a captured-lord conversation.";
        }

        return $"Battle rewards advanced: pendingPrisoners={pendingPrisoners} " +
               $"conversationHero={Hero.OneToOneConversationHero?.StringId ?? "none"} " +
               $"capturedHeroes={playerEncounter._capturedHeroes?.Count ?? 0}.";
    }

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
