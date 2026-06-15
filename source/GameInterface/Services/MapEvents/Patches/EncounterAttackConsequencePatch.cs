using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MenuHelper))]
internal class EncounterAttackConsequencePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncounterAttackConsequencePatch>();

    private static readonly TextObject PvpNotSupported = new("{=!}Player-vs-player battles are not yet supported in Co-op.");

    [HarmonyPatch(nameof(MenuHelper.EncounterAttackConsequence))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // Player-vs-player battles are not yet functional. When the player's map event involves more than one
        // player party, swallow the attack so nothing happens — on the host and every client. The encounter menu
        // stays open, so the player can still Leave. This runs before the normal client/server handling below.
        if (IsPlayerVsPlayerMapEvent(MapEvent.PlayerMapEvent))
        {
            InformationManager.DisplayMessage(new InformationMessage(PvpNotSupported.ToString()));
            return false;
        }

        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        // Server can run the original consequence normally.
        if (ModInformation.IsServer)
            return true;

        var battle = PlayerEncounter.Battle;

        if (battle == null)
        {
            Logger.Warning("Client tried to start attack mission, but PlayerEncounter.Battle was null");
            return false;
        }

        // Ask the server for the authoritative map event state / mission start.
        MessageBroker.Instance.Publish(
            battle,
            new AttackMissionAttempted(battle));

        return false;
    }

    /// <summary>True when the map event involves more than one player party (PvP).</summary>
    private static bool IsPlayerVsPlayerMapEvent(MapEvent mapEvent)
    {
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return false;

        int playerParties = 0;
        foreach (var party in mapEvent.AttackerSide.Parties)
        {
            if (party.Party.MobileParty.IsPlayerParty())
            {
                playerParties++;
            }
        }

        foreach (var party in mapEvent.DefenderSide.Parties)
        {
            if (party.Party.MobileParty.IsPlayerParty())
            {
                playerParties++;
            }
        }

        return playerParties > 1;
    }
}
