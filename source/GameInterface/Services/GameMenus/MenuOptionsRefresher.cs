using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameMenus;

/// <summary>Refreshes a campaign menu's options when its view can take the refresh.</summary>
public interface IMenuOptionsRefresher
{
    /// <summary>Refreshes the menu options, skipped during a mission or before MapScreen binds the menu handler.</summary>
    void RefreshMenuOptions(MenuContext menuContext);
}

/// <summary>Owns when a campaign menu refresh is safe, so every caller skips the same unbound states.</summary>
internal class MenuOptionsRefresher : IMenuOptionsRefresher
{
    /// <summary>Refreshes the menu options, skipped during a mission or before MapScreen binds the menu handler.</summary>
    public void RefreshMenuOptions(MenuContext menuContext)
    {
        // MapState.OnActivate refreshes the menu after a mission
        if (Mission.Current != null) return;

        // RefreshMenuOptions calls Handler.OnMenuRefresh unchecked; an unbound menu builds its options fresh when its view binds
        if (menuContext?.Handler == null) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
    }
}
