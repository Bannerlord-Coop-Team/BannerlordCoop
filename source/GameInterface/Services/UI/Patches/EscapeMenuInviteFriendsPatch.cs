using Common;
using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Adds an "Invite Friends" entry to the campaign-map escape menu on the client that
/// advertises the session (the hosting player's client).
/// </summary>
[HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
internal class EscapeMenuInviteFriendsPatch
{
    [HarmonyPostfix]
    static void AddInviteFriendsItem(List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return;
        if (!advertiser.IsAdvertising) return;

        __result.Add(new EscapeMenuItemVM(
            new TextObject("Invite Friends"),
            _ =>
            {
                if (!advertiser.InviteFriends())
                {
                    MessageBroker.Instance.Publish(null, new SendInformationMessage(SessionInviteText.OverlayUnavailableHint));
                }
            },
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));
    }
}
