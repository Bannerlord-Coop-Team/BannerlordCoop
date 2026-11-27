using Common.Logging;
using Common.Util;
using GameInterface.Services.MapEvents.Initialization;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Keeps a hideout encounter open until its own flow finishes it, while its mission is still fighting the battle or
/// while it holds staged battle results or a send troops result menu.
/// </summary>
public interface IHideoutResultEncounter
{
    /// <summary>
    /// Keeps the main party's encounter through a replicated settlement leave while its hideout mission is still
    /// fighting, or while it holds staged hideout battle results or a send troops result menu. Returns false when the
    /// leave should close the encounter as usual.
    /// </summary>
    bool TryKeepThroughLeave(MobileParty party);
}

/// <inheritdoc cref="IHideoutResultEncounter"/>
internal class HideoutResultEncounter : IHideoutResultEncounter
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutResultEncounter>();

    public bool TryKeepThroughLeave(MobileParty party)
    {
        var mainParty = MobileParty.MainParty;
        var encounter = PlayerEncounter.Current;
        var settlement = encounter?.EncounterSettlementAux;
        if (party != mainParty || settlement == null)
            return false;

        // Native's hideout boss line reads this encounter, without it the boss only says "I am not allowed to talk with you".
        if (IsFightingInMission(encounter, mainParty))
        {
            Logger.Debug("Kept hideout encounter {MapEventId} through replicated settlement leave while its mission runs",
                encounter._mapEvent.StringId);
            return true;
        }

        if (!IsHoldingResults(encounter))
            return false;

        // Same gate and hold as the settlement close, without finishing the encounter that holds the loot or the send troops result menu.
        using (new AllowedThread())
        {
            mainParty.Position = settlement.GatePosition;
            mainParty.SetMoveModeHold();
        }

        Logger.Debug("Kept hideout result encounter {MapEventId} through replicated settlement leave",
            encounter._mapEvent.StringId);
        return true;
    }

    internal static bool IsHoldingResults(PlayerEncounter encounter) =>
        encounter?._mapEvent?.IsHideoutBattle == true &&
        MapEventInitializationBarrier.IsBattleResultEncounter(encounter);

    private static bool IsFightingInMission(PlayerEncounter encounter, MobileParty mainParty) =>
        Mission.Current != null &&
        encounter._mapEvent is { IsHideoutBattle: true, IsFinalized: false } battle &&
        mainParty.MapEvent == battle;
}
