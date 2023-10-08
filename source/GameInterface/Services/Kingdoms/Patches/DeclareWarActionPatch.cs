using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Patches declaring war
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarAction), "ApplyInternal")]
    public class DeclareWarActionPatch
    {
        private static readonly AllowedInstance<IFaction> AllowedInstance = new AllowedInstance<IFaction>();

        private static readonly Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> ApplyInternal =
        typeof(ChangeOwnerOfSettlementAction)
        .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
        .BuildDelegate<Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>>();

        public static bool Prefix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            if (AllowedInstance.IsAllowed(faction1)) return true;

            MessageBroker.Instance.Publish(faction1,
                new DeclareWar(faction1.StringId, faction2.StringId, (int)declareWarDetail));

            return false;
        }

        public static void RunOriginalApplyInternal(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = faction1;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(faction1, faction2, declareWarDetail);
                }, true);
            }
        }

    }
}
