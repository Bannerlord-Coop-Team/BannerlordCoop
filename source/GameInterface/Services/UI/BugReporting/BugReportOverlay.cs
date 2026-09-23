using Common;
using Common.Messaging;
using GameInterface.Services.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.BugReportTab;
using GameInterface.Services.UI.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Displays the in-game bug-report button and form.</summary>
public interface IBugReportOverlay : IDisposable
{
    bool IsAvailable { get; }
    void Initialize();
    void Open();
}

/// <inheritdoc />
internal sealed class BugReportOverlay : GlobalLayer, IBugReportOverlay
{
    private const string SummaryInputWidgetId = "CoopBugReportSummaryInput";
    // Vanilla map nameplates use 90 and the regular map UI uses 100.
    private const int LayerOrder = 110;

    private readonly IBugReportService bugReportService;
    private readonly IBugReportSubmissionConsent submissionConsent;
    private readonly ICoopOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;
    private BugReportVM dataSource;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private EditableTextWidget summaryInput;
    private bool initialized;
    private bool showBugReportButton;

    public bool IsAvailable => initialized && showBugReportButton;

    public BugReportOverlay(
        IBugReportService bugReportService,
        IBugReportSubmissionConsent submissionConsent,
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker)
    {
        if (bugReportService == null) throw new ArgumentNullException(nameof(bugReportService));
        if (submissionConsent == null) throw new ArgumentNullException(nameof(submissionConsent));
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        this.bugReportService = bugReportService;
        this.submissionConsent = submissionConsent;
        this.optionsStore = optionsStore;
        this.messageBroker = messageBroker;
    }

    public void Initialize()
    {
        if (initialized || ModInformation.IsServer || Game.Current == null) return;

        showBugReportButton = BugReportOptionsTabProvider.GetShowBugReportButtonOrDefault(
            optionsStore.LoadOrDefault());
        dataSource = new BugReportVM(Submit);
        dataSource.OpenRequested += FocusForm;
        dataSource.CloseRequested += ReleaseInputFocus;
        dataSource.Submitted += HandleSubmitted;

        gauntletLayer = new GauntletLayer("CoopBugReport", LayerOrder);
        movie = gauntletLayer.LoadMovie("CoopBugReportUIMovie", dataSource);
        SetPassiveInputRestrictions();
        Layer = gauntletLayer;
        ScreenManager.AddGlobalLayer(this, false);
        messageBroker.Subscribe<BugReportVisibilitySelected>(HandleVisibilitySelected);
        initialized = true;
    }

    public void Open()
    {
        if (!IsAvailable) return;

        dataSource.SetPresentationVisible(true);
        dataSource.ActionOpen();
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!initialized) return;

        if (dataSource.IsFormVisible && Input.IsKeyReleased(InputKey.Escape))
            dataSource.ActionClose();
    }

    public void Dispose()
    {
        if (!initialized) return;

        messageBroker.Unsubscribe<BugReportVisibilitySelected>(HandleVisibilitySelected);
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

    private void HandleVisibilitySelected(MessagePayload<BugReportVisibilitySelected> payload)
    {
        showBugReportButton = payload.What.ShowBugReportButton;
        if (!showBugReportButton) dataSource.SetPresentationVisible(false);
    }
}
