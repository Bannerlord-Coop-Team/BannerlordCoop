using GameInterface.Services.BugReporting.Messages;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.BugReporting;

/// <summary>Backs the in-game bug-report button and submission form.</summary>
internal sealed class BugReportVM : ViewModel
{
    private readonly Func<string, string, bool> submit;
    private string summary = string.Empty;
    private string description = string.Empty;
    private string validationMessage = string.Empty;
    private bool isFormVisible;
    private bool isPresentationVisible;

    public BugReportVM(Func<string, string, bool> submit)
    {
        if (submit == null) throw new ArgumentNullException(nameof(submit));
        this.submit = submit;
    }

    public event Action CloseRequested;
    public event Action OpenRequested;
    public event Action Submitted;

    [DataSourceProperty]
    public string ButtonText => "Report Coop Bug";

    [DataSourceProperty]
    public string TitleText => "Report Coop Bug";

    [DataSourceProperty]
    public string DisclosureText =>
        "Your network ID, summary, and description will be sent to the server. A redacted server log " +
        "and current co-op logs from clients that enabled diagnostic sharing may also be included.";

    [DataSourceProperty]
    public string SummaryLabel => "Summary";

    [DataSourceProperty]
    public string DescriptionLabel => "Description";

    [DataSourceProperty]
    public string SubmitButtonText => "Submit";

    [DataSourceProperty]
    public string CancelButtonText => "Cancel";

    [DataSourceProperty]
    public int MaximumSummaryLength => NetworkRequestBugReport.MaximumSummaryLength;

    [DataSourceProperty]
    public int MaximumDescriptionLength => NetworkRequestBugReport.MaximumDescriptionLength;

    [DataSourceProperty]
    public bool IsButtonVisible => IsPresentationVisible && !IsFormVisible;

    [DataSourceProperty]
    public bool IsFormVisible
    {
        get => isFormVisible;
        private set
        {
            if (isFormVisible == value) return;
            isFormVisible = value;
            OnPropertyChanged(nameof(IsFormVisible));
            OnPropertyChanged(nameof(IsButtonVisible));
        }
    }

    [DataSourceProperty]
    public bool IsPresentationVisible
    {
        get => isPresentationVisible;
        private set
        {
            if (isPresentationVisible == value) return;
            isPresentationVisible = value;
            OnPropertyChanged(nameof(IsPresentationVisible));
            OnPropertyChanged(nameof(IsButtonVisible));
        }
    }

    [DataSourceProperty]
    public string Summary
    {
        get => summary;
        set
        {
            value ??= string.Empty;
            if (summary == value) return;
            summary = value;
            OnPropertyChanged(nameof(Summary));
        }
    }

    [DataSourceProperty]
    public string Description
    {
        get => description;
        set
        {
            value ??= string.Empty;
            if (description == value) return;
            description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    [DataSourceProperty]
    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (validationMessage == value) return;
            validationMessage = value;
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    [DataSourceProperty]
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    public void ActionOpen()
    {
        ValidationMessage = string.Empty;
        IsFormVisible = true;
        OpenRequested?.Invoke();
    }

    public void ActionClose()
    {
        IsFormVisible = false;
        ValidationMessage = string.Empty;
        CloseRequested?.Invoke();
    }

    public void ActionSubmit()
    {
        var normalizedSummary = (Summary ?? string.Empty).Trim();
        var normalizedDescription = (Description ?? string.Empty).Trim();
        if (normalizedSummary.Length == 0)
        {
            ValidationMessage = "Enter a summary.";
            return;
        }
        if (normalizedDescription.Length == 0)
        {
            ValidationMessage = "Enter a description.";
            return;
        }

        try
        {
            if (!submit(normalizedSummary, normalizedDescription)) return;
        }
        catch (ArgumentException exception)
        {
            ValidationMessage = exception.Message;
            return;
        }
        catch (Exception)
        {
            ValidationMessage = "The bug report could not be sent.";
            return;
        }

        CompleteSubmission();
    }

    internal void CompleteSubmission()
    {
        Summary = string.Empty;
        Description = string.Empty;
        IsFormVisible = false;
        ValidationMessage = string.Empty;
        Submitted?.Invoke();
    }

    internal void DiscardSubmission()
    {
        Summary = string.Empty;
        Description = string.Empty;
        ActionClose();
    }

    internal void SetSubmissionError(string message)
    {
        ValidationMessage = message;
    }

    internal void SetPresentationVisible(bool visible)
    {
        IsPresentationVisible = visible;
        if (!visible && IsFormVisible) ActionClose();
    }
}
