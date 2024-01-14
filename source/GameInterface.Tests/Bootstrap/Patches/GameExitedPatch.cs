using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SandBox.View.Map;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch("OnExit")]
internal class GameExitedPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnExit")]
    static bool OnExit(ref MapScreen __instance)
    {
        return false;
    }
}
