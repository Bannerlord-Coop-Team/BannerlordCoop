using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches the escape of the player party, only runs on local client
    /// </summary>
    [HarmonyPatch(typeof(PlayerCaptivityCampaignBehavior))]
    public class PlayerEscapePatch
    {
        private static readonly Action<PlayerCaptivityCampaignBehavior, MenuCallbackArgs> CaptivityEscape =
            typeof(PlayerCaptivityCampaignBehavior)
            .GetMethod("game_menu_captivity_escape_on_init", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<PlayerCaptivityCampaignBehavior, MenuCallbackArgs>>();

        [HarmonyPrefix]
        [HarmonyPatch("game_menu_captivity_escape_on_init")]
        public static bool Prefix(MenuCallbackArgs args)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            MessageBroker.Instance.Publish(null, new LocalPlayerEscaped(
                Hero.MainHero.StringId));

            return false;
        }

        public static void RunEscapeCaptivityMenu()
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    CaptivityEscape.Invoke(
                        Campaign.Current.GetCampaignBehavior<PlayerCaptivityCampaignBehavior>(), 
                        new MenuCallbackArgs(Campaign.Current.CurrentMenuContext, new TextObject()));
                }
            });
        }
    }
}