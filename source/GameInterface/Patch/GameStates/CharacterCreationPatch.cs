using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace GameInterface.Patch.GameStates
{
    [HarmonyPatch(typeof(CharacterCreationState))]
    internal class CharacterCreationPatch
    {
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPostfix]
        public static void CharacterCreationStateCtor(ref CharacterCreationState __instance)
        {
            //MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
        }
    }
}
