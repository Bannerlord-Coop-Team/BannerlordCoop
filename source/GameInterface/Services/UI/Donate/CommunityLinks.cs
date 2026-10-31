using GameInterface.Services.UI.Credits;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Donate;

/// <summary>
/// Shared community / donation / credits actions used by the co-op menus (connect menu, coop options).
/// Keeps the platform URLs and the popup launches in one place so screens stay in sync.
/// </summary>
internal static class CommunityLinks
{
    private const string DiscordUrl = "https://discord.gg/ngC4RVb";
    private const string PatreonUrl = "https://www.patreon.com/16338430/join";
    private const string RentServerUrl = "https://www.patreon.com/checkout/BannerlordCoop?rid=28995946";

    public static void OpenDiscord() => Open(DiscordUrl);

    public static void OpenPatreon() => Open(PatreonUrl);

    // Opens the checkout for the Patreon tier that includes a rented server.
    public static void OpenRentServer() => Open(RentServerUrl);

    /// <summary>Shows the donation popup layered over whichever screen is currently on top.</summary>
    public static void ShowDonatePopup()
    {
        if (ScreenManager.TopScreen is ScreenBase owner)
        {
            DonatePopupOverlay.Show(owner);
        }
    }

    /// <summary>Shows the credits popup layered over whichever screen is currently on top.</summary>
    public static void ShowCreditsPopup()
    {
        if (ScreenManager.TopScreen is ScreenBase owner)
        {
            CreditsPopupOverlay.Show(owner);
        }
    }

    private static void Open(string url) => System.Diagnostics.Process.Start(url);
}
