using Common.Util;
using HarmonyLib;
using SandBox.ViewModelCollection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
internal class AllowEquipmentInGUI
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetCharacterCode)),
        AccessTools.Method(typeof(SandBoxUIHelper), nameof(SandBoxUIHelper.GetCharacterCode))
    };

    [HarmonyPrefix]
    private static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyFinalizer]
    private static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}
