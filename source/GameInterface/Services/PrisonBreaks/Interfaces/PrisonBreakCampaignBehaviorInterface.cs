using Common.Logging;
using GameInterface.Services.PrisonBreaks.Messages;
using Helpers;
using SandBox.CampaignBehaviors;
using SandBox.Missions.MissionLogics.Towns;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.PrisonBreaks.Interfaces;

/// <summary>
/// The server owns the per-settlement cooldowns, clients mirror them and run the prison break mission locally.
/// </summary>
public interface IPrisonBreakCampaignBehaviorInterface : IGameAbstraction
{
    bool IsOnCooldown(Settlement settlement);
    CampaignTime AddCooldown(Settlement settlement);
    void SetCooldown(Settlement settlement, CampaignTime until);
    bool IsPrisonBreakActive(Settlement settlement);
    void SetPrisonBreakActive(Settlement settlement, bool isActive);
    void OpenPrisonBreakMission(Settlement settlement, Hero prisoner);
    void CancelPrisonBreak(PrisonBreakStartResult result);
    void AbortPrisonBreakMission(Hero prisoner);
    void OnPrisonBreakMissionEnded();
}

public class PrisonBreakCampaignBehaviorInterface : IPrisonBreakCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<PrisonBreakCampaignBehaviorInterface>();

    private const string StartMenuId = "start_prison_break";

    private readonly HashSet<Settlement> activeSettlements = new();
    private bool isMissionAborted;

    public bool IsOnCooldown(Settlement settlement)
    {
        if (!TryGetPrisonBreakBehavior(out var behavior)) return false;

        return !behavior.CanPlayerStartPrisonBreak(settlement);
    }

    public CampaignTime AddCooldown(Settlement settlement)
    {
        if (!TryGetPrisonBreakBehavior(out var behavior)) return CampaignTime.Zero;

        behavior.AddCoolDownForPrisonBreak(settlement);
        return behavior._coolDownData[settlement];
    }

    public void SetCooldown(Settlement settlement, CampaignTime until)
    {
        if (!TryGetPrisonBreakBehavior(out var behavior)) return;

        behavior._coolDownData[settlement] = until;
    }

    public bool IsPrisonBreakActive(Settlement settlement)
    {
        return activeSettlements.Contains(settlement);
    }

    public void SetPrisonBreakActive(Settlement settlement, bool isActive)
    {
        if (isActive) activeSettlements.Add(settlement);
        else activeSettlements.Remove(settlement);
    }

    public void OpenPrisonBreakMission(Settlement settlement, Hero prisoner)
    {
        if (!TryGetPrisonBreakBehavior(out var behavior)) return;

        // Vanilla OpenPrisonBreakMission minus the gold and cooldown, which the server already applied.
        behavior._prisonerHero = prisoner;
        behavior._launchingPrisonBreakMission = true;
        Location prison = LocationComplex.Current.GetLocationWithId("prison");
        CampaignMission.OpenPrisonBreakMission(prison.GetSceneName(settlement.Town.GetWallLevel()), prison, prisoner.CharacterObject);
    }

    public void CancelPrisonBreak(PrisonBreakStartResult result)
    {
        if (!TryGetPrisonBreakBehavior(out var behavior)) return;

        behavior._prisonerHero = null;

        // The player may have left the menu while the request was in flight.
        if (Campaign.Current.CurrentMenuContext?.GameMenu?.StringId != StartMenuId) return;

        if (result == PrisonBreakStartResult.OnCooldown)
        {
            GameMenu.SwitchToMenu("prison_break_cool_down");
            return;
        }

        var reason = result == PrisonBreakStartResult.NotEnoughGold
            ? GameTexts.FindText("str_coop_prison_break_not_enough_gold")
            : GameTexts.FindText("str_coop_prison_break_prisoner_unavailable");
        MBInformationManager.AddQuickInformation(reason);
        GameMenu.SwitchToMenu(behavior._previousMenuId);
    }

    public void AbortPrisonBreakMission(Hero prisoner)
    {
        var controller = Mission.Current?.GetMissionBehavior<PrisonBreakMissionController>();
        if (controller == null || controller._prisonerCharacter != prisoner.CharacterObject) return;

        isMissionAborted = true;

        // Same popup as vanilla's ShowMissionFailedPopup.
        var text = GameTexts.FindText("str_coop_prison_break_prisoner_lost");
        StringHelpers.SetCharacterProperties("PRISONER", prisoner.CharacterObject, text);
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=wQbfWNZO}Mission Failed!").ToString(),
            text.ToString(),
            true,
            false,
            new TextObject("{=DM6luo3c}Continue").ToString(),
            null,
            () => Mission.Current?.EndMission(),
            null), pauseGameActiveState: true);
    }

    public void OnPrisonBreakMissionEnded()
    {
        // Vanilla swapped the real prisoners for the mission's guards and prisoner, the handler requests the real roster back.
        LocationComplex.Current?.GetLocationWithId("prison")?.RemoveAllCharacters();

        if (!isMissionAborted) return;
        isMissionAborted = false;

        if (!TryGetPrisonBreakBehavior(out var behavior)) return;

        behavior._prisonerHero = null;
        GameMenu.SwitchToMenu(behavior._previousMenuId);
    }

    private bool TryGetPrisonBreakBehavior(out PrisonBreakCampaignBehavior prisonBreakBehavior)
    {
        prisonBreakBehavior = Campaign.Current?.GetCampaignBehavior<PrisonBreakCampaignBehavior>();
        if (prisonBreakBehavior != null) return true;

        Logger.Debug("Skipping prison break update because the campaign behavior is unavailable");
        return false;
    }
}
