using Common;
using Common.Network.Session;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Adds "Invite Friends" as the second campaign-map escape-menu item for clients
/// in the session's Steam lobby.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
internal class EscapeMenuInviteFriendsPatch
{
    [HarmonyPostfix]
    static void AddInviteFriendsItem(List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return;
        if (!advertiser.CanInviteFriends) return;

        __result.Insert(1, new EscapeMenuItemVM(
            new TextObject("Invite Friends"),
            _ =>
            {
                if (!advertiser.InviteFriends())
                {
                    InformationManager.DisplayMessage(new InformationMessage(SessionInviteText.OverlayUnavailableHint));
                }
            },
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));
    }
}
