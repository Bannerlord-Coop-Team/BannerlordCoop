using SandBox.AdvancedStartOptions;
using SandBox.ViewModelCollection.CampaignStartingOptions;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using StartOptions = SandBox.AdvancedStartOptions.AdvancedStartOptions;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// Vanilla's starting options window for a joining player, shown after character creation.
/// Unlike <see cref="SandBox.GauntletUI.GauntletCampaignStartingOptionsView"/> it doesn't need the main menu
/// on top, joins the server on confirm, and asks before closing goes back to the main menu.
/// </summary>
internal sealed class CoopStartingOptionsView : GlobalLayer
{
    private readonly StartOptions startOptions;
    private readonly CampaignStartingOptionsVM dataSource;
    private readonly Action<StartOptions> onJoin;
    private readonly Action onLeave;
    private bool finished;
    private int openPopups;

    public CoopStartingOptionsView(StartOptions startOptions, Action<StartOptions> onJoin, Action onLeave)
    {
        this.startOptions = startOptions;
        this.onJoin = onJoin;
        this.onLeave = onLeave;

        MBTextManager.SetTextVariable("newline", "\n");
        dataSource = new CoopCampaignStartingOptionsVM(startOptions, Join, AskToLeave);

        // Above the local map and its UI, below the chat log, notifications, popups, tooltips and loading window.
        var gauntletLayer = new GauntletLayer("CampaignStartingOptions", 15200);
        // Black behind the window so it reads as the end of character creation, not a menu over the local map.
        gauntletLayer.LoadMovie("CoopStartingOptionsBackdrop", dataSource);
        gauntletLayer.LoadMovie("CampaignStartingOptionsScreen", dataSource);
        Layer = gauntletLayer;
        Layer.IsFocusLayer = true;
        Layer.InputRestrictions.SetInputRestrictions();

        var keys = HotKeyManager.GetCategory("GenericPanelGameKeyCategory");
        gauntletLayer.Input.RegisterHotKeyCategory(keys);
        dataSource.SetDoneInputKey(keys.GetHotKey("Confirm"));
        dataSource.SetCancelInputKey(keys.GetHotKey("Exit"));
        dataSource.SetRandomizeInputKey(keys.GetHotKey("Randomize"));
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        // An open popup has focus and handles its own keys.
        if (finished || openPopups > 0) return;

        ScreenManager.TrySetFocus(Layer);
        if (Layer.Input.IsHotKeyReleased("Confirm"))
        {
            if (dataSource.CanConfirmStartType || dataSource.IsASOShown)
            {
                UISoundsHelper.PlayUISound("event:/ui/panels/next");
                dataSource.ExecuteConfirm();
            }
        }
        else if (Layer.Input.IsHotKeyReleased("Exit"))
        {
            UISoundsHelper.PlayUISound(dataSource.IsASOShown ? "event:/ui/panels/next" : "event:/ui/default");
            dataSource.ExecuteCancel();
        }
    }

    /// <summary>Removes the window without running either callback.</summary>
    public void Remove()
    {
        if (finished) return;
        finished = true;

        if (openPopups > 0)
        {
            openPopups = 0;
            InformationManager.HideInquiry();
        }

        ScreenManager.RemoveGlobalLayer(this);
        dataSource.OnFinalize();
    }

    /// <summary>Re-checks which items can be picked, and tells the player if the one they had picked got locked.</summary>
    public void Refresh()
    {
        if (finished) return;

        var lists = startOptions.GetAllOptions().OfType<ListAdvancedStartOption>().ToList();
        var picks = lists.Select(option => option.GetValue<string>()).ToList();

        // Locks items and moves a locked pick on to the next item, as vanilla does when another option changes.
        dataSource.OnOptionChanged();

        for (int i = 0; i < lists.Count; i++)
        {
            var option = lists[i];
            if (option.GetValue<string>() == picks[i] || option.GetIsHidden(startOptions)) continue;

            option.GetItemCondition(picks[i], startOptions, out var reason);
            ShowLockedPick(option, picks[i], reason);
            return;
        }
    }

    // Callbacks run before the window goes so their loading screen covers the local map first.
    private void Join(StartOptions options)
    {
        // A pick stays on a locked item when nothing else in its list can be picked.
        foreach (var option in options.GetAllOptions().OfType<ListAdvancedStartOption>())
        {
            string pick = option.GetValue<string>();
            if (option.GetIsHidden(options) || !option.GetItemCondition(pick, options, out var reason)) continue;

            ShowLockedPick(option, pick, reason);
            return;
        }

        try { onJoin(options); }
        finally { Remove(); }
    }

    private void ShowLockedPick(ListAdvancedStartOption option, string pick, TextObject reason)
    {
        ShowPopup(new InquiryData(
            GameTexts.FindText("str_coop_advanced_start_locked_title").ToString(),
            GameTexts.FindText("str_coop_advanced_start_locked_text")
                .SetTextVariable("OPTION", startOptions.GetItemName(option.StringId, pick))
                .SetTextVariable("REASON", reason)
                .ToString(),
            true, false, GameTexts.FindText("str_ok").ToString(), null,
            ClosePopup, null));
    }

    // Leaving throws away the character just created, so a stray click on X shouldn't do it.
    private void AskToLeave()
    {
        ShowPopup(new InquiryData(
            GameTexts.FindText("str_coop_advanced_start_leave_title").ToString(),
            GameTexts.FindText("str_coop_advanced_start_leave_text").ToString(),
            true, true,
            GameTexts.FindText("str_coop_advanced_start_leave_confirm").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            () =>
            {
                ClosePopup();
                try { onLeave(); }
                finally { Remove(); }
            },
            ClosePopup));
    }

    private void ShowPopup(InquiryData inquiry)
    {
        openPopups++;
        InformationManager.ShowInquiry(inquiry);
    }

    private void ClosePopup()
    {
        if (openPopups > 0) openPopups--;
    }
}
