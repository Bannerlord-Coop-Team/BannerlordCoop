using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Donate;

/// <summary>
/// Shared community / donation link actions used by the co-op menus (connect menu, coop options).
/// Keeps the platform URLs and the donate-popup launch in one place so screens stay in sync.
/// </summary>
internal static class CommunityLinks
{
    private const string DiscordUrl = "https://discord.gg/ngC4RVb";
    private const string PatreonUrl = "https://www.patreon.com/c/bannerlordcoop";

    public static void OpenDiscord() => Open(DiscordUrl);

    public static void OpenPatreon() => Open(PatreonUrl);

    /// <summary>Shows the donation popup layered over whichever screen is currently on top.</summary>
    public static void ShowDonatePopup()
    {
        if (ScreenManager.TopScreen is ScreenBase owner)
        {
            DonatePopupOverlay.Show(owner);
        }
    }

    private static void Open(string url) => System.Diagnostics.Process.Start(url);
}
