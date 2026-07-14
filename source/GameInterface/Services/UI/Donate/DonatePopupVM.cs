using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Donate;

/// <summary>
/// Backs the donation popup: a prompt above a vertical list of platform buttons and a close button.
/// </summary>
public class DonatePopupVM : ViewModel
{
    private readonly Action close;

    public DonatePopupVM(Action close)
    {
        this.close = close ?? throw new ArgumentNullException(nameof(close));
    }

    public string PromptText =>
        "Bannerlord Coop is free and developed by volunteers.\n\n" +
        "Donations help cover servers, development tools, and other project costs so we can keep improving the mod.\n\n" +
        "If you enjoy the project and would like to support its development, consider donating.";
    public string BuyMeACoffeeButtonText => "Buy a Coffee";
    public string IfdianButtonText => "ifdian";
    public string CloseButtonText => "Close";

    public void ActionBuyMeACoffee()
    {
        System.Diagnostics.Process.Start("https://buymeacoffee.com/bannerlordcoop");
    }

    public void ActionIfdian()
    {
        System.Diagnostics.Process.Start("https://ifdian.net/a/BannerlordCoop");
    }

    public void ActionClose()
    {
        close();
    }
}
