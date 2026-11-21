using SandBox.ViewModelCollection.CampaignStartingOptions;
using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using StartOptions = SandBox.AdvancedStartOptions.AdvancedStartOptions;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// Vanilla's starting options view model with its buttons labelled for joining a co-op server:
/// whichever button sends the hero to the server reads "Join Coop".
/// </summary>
internal sealed class CoopCampaignStartingOptionsVM : CampaignStartingOptionsVM
{
    // Also the review stage's button text, so both buttons that send the hero read the same.
    internal const string JoinLabel = "Join Coop";
    internal const string NextLabel = "{=Rvr1bcu8}Next";

    public CoopCampaignStartingOptionsVM(StartOptions startOptions, Action<StartOptions> onConfirm, Action onClose)
        : base(startOptions, onConfirm, onClose)
    {
        PropertyChangedWithBoolValue += HandleBoolPropertyChanged;
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        StartGameLabel = JoinLabel;
        RefreshConfirmLabel();
    }

    public override void OnFinalize()
    {
        PropertyChangedWithBoolValue -= HandleBoolPropertyChanged;
        base.OnFinalize();
    }

    private void HandleBoolPropertyChanged(object sender, PropertyChangedWithBoolValueEventArgs e)
    {
        if (e.PropertyName == nameof(IsDefaultStartSelected) || e.PropertyName == nameof(IsAdvancedStartSelected))
            RefreshConfirmLabel();
    }

    // The card screen's confirm joins straight away with the default start, and opens the options with the advanced one.
    private void RefreshConfirmLabel()
    {
        if (IsDefaultStartSelected)
            ConfirmLabel = JoinLabel;
        else if (IsAdvancedStartSelected)
            ConfirmLabel = new TextObject(NextLabel).ToString();
        else
            ConfirmLabel = new TextObject("{=5Unqsx3N}Confirm").ToString();
    }
}
