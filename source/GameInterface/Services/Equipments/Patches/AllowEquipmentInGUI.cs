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
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Scripts;
using TaleWorlds.MountAndBlade.View.Tableaus.Thumbnails;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch]
internal class AllowEquipmentInGUI
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetCharacterCode)),
        AccessTools.Method(typeof(SandBoxUIHelper), nameof(SandBoxUIHelper.GetCharacterCode)),
        AccessTools.Method(typeof(Mission), nameof(Mission.SpawnAgent)),
        AccessTools.Method(typeof(CharacterSpawner), nameof(CharacterSpawner.InitWithCharacter)),
        AccessTools.Method(typeof(CharacterThumbnailCache), "GetPoseParamsFromCharacterCode")
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
