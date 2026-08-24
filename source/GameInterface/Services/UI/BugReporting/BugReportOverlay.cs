using Common;
using GameInterface.Services.BugReporting;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Displays the in-game bug-report button and form.</summary>
public interface IBugReportOverlay : IDisposable
{
    void Initialize();
}

/// <inheritdoc />
internal sealed class BugReportOverlay : GlobalLayer, IBugReportOverlay
{
    private const string SummaryInputWidgetId = "CoopBugReportSummaryInput";
    private const int LayerOrder = 2;

    private readonly IBugReportService bugReportService;
    private readonly IBugReportSubmissionConsent submissionConsent;
    private BugReportVM dataSource;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private EditableTextWidget summaryInput;
    private bool initialized;

    public BugReportOverlay(
        IBugReportService bugReportService,
        IBugReportSubmissionConsent submissionConsent)
    {
        if (bugReportService == null) throw new ArgumentNullException(nameof(bugReportService));
        if (submissionConsent == null) throw new ArgumentNullException(nameof(submissionConsent));
        this.bugReportService = bugReportService;
        this.submissionConsent = submissionConsent;
    }

    public void Initialize()
    {
        if (initialized || ModInformation.IsServer || Game.Current == null) return;

        dataSource = new BugReportVM(Submit);
        dataSource.OpenRequested += FocusForm;
        dataSource.CloseRequested += ReleaseInputFocus;
        dataSource.Submitted += HandleSubmitted;

        gauntletLayer = new GauntletLayer("CoopBugReport", LayerOrder);
        movie = gauntletLayer.LoadMovie("CoopBugReportUIMovie", dataSource);
        SetPassiveInputRestrictions();
        Layer = gauntletLayer;
        ScreenManager.AddGlobalLayer(this, false);
        initialized = true;
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!initialized) return;

        UpdateVisibility();
        if (dataSource.IsFormVisible && Input.IsKeyReleased(InputKey.Escape))
            dataSource.ActionClose();
    }

    public void Dispose()
    {
        if (!initialized) return;

        dataSource.OpenRequested -= FocusForm;
        dataSource.CloseRequested -= ReleaseInputFocus;
        dataSource.Submitted -= HandleSubmitted;
        ReleaseInputFocus();
        if (movie != null) gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        dataSource.OnFinalize();

        summaryInput = null;
        movie = null;
        gauntletLayer = null;
        dataSource = null;
        Layer = null;
        initialized = false;
    }

    private bool Submit(string summary, string description)
    {
        if (!submissionConsent.IsRequired())
        {
            bugReportService.SubmitReport(summary, description);
            return true;
        }

        ReleaseInputFocus();
        InformationManager.ShowInquiry(submissionConsent.CreateInquiry(
            () => CompletePendingSubmission(summary, description),
            dataSource.DiscardSubmission));
        return false;
    }

    private void CompletePendingSubmission(string summary, string description)
    {
        try
        {
            bugReportService.SubmitReport(summary, description);
            dataSource.CompleteSubmission();
        }
        catch (ArgumentException exception)
        {
            dataSource.SetSubmissionError(exception.Message);
            FocusForm();
        }
        catch (Exception)
        {
            dataSource.SetSubmissionError("The bug report could not be sent.");
            FocusForm();
        }
    }

    private void HandleSubmitted()
    {
        ReleaseInputFocus();
        InformationManager.DisplayMessage(new InformationMessage(
            "[Bug Report] Submitted to the server. Collecting consenting client logs."));
    }

    private void FocusForm()
    {
        summaryInput ??= movie?.Movie?.RootWidget?
            .FindChild(SummaryInputWidgetId, includeAllChildren: true) as EditableTextWidget;

        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);
        if (!ReferenceEquals(ScreenManager.FocusedLayer, gauntletLayer))
        {
            gauntletLayer.IsFocusLayer = false;
            SetPassiveInputRestrictions();
            return;
        }

        if (summaryInput != null)
            gauntletLayer.UIContext.EventManager.FocusedWidget = summaryInput;
    }

    private void ReleaseInputFocus()
    {
        if (gauntletLayer == null) return;

        gauntletLayer.UIContext.EventManager.FocusedWidget = null;
        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        SetPassiveInputRestrictions();
    }

    private void SetPassiveInputRestrictions()
    {
        gauntletLayer.InputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Mouse);
    }

    private void UpdateVisibility()
    {
        var topScreen = ScreenManager.TopScreen;
        var isGameplayScreen = topScreen is MapScreen || topScreen is MissionScreen;
        var isConversationActive = Campaign.Current?.ConversationManager?.IsConversationInProgress == true;
        if (topScreen is MissionScreen missionScreen)
            isConversationActive |= missionScreen.IsConversationActive;

        var shouldShow = ShouldShowPresentation(
            isGameplayScreen,
            LoadingWindow.IsLoadingWindowActive,
            isConversationActive);
        dataSource.SetPresentationVisible(shouldShow);
    }

    internal static bool ShouldShowPresentation(
        bool isGameplayScreen,
        bool isLoading,
        bool isConversationActive)
    {
        return isGameplayScreen && !isLoading && !isConversationActive;
    }
}
