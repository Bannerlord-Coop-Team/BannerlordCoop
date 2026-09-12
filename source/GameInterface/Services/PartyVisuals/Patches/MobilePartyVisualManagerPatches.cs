using Common.Logging;
using HarmonyLib;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.PartyVisuals.Patches
{
    [HarmonyPatch(typeof(MobilePartyVisualManager))]
    public class MobilePartyVisualManagerPatches
    {
        private static ILogger Logger = LogManager.GetLogger<MobilePartyVisualManagerPatches>();

        [HarmonyPatch(nameof(MobilePartyVisualManager.OnInitialize))]
        [HarmonyPrefix]
        private static void OnInitializePrefix(MobilePartyVisualManager __instance)
        {
            PrepareDirtyPartyVisualBuffer(
                ref __instance._dirtyPartyVisualCount,
                ref __instance._dirtyPartiesList,
                MobileParty.All.Count);
        }

        [HarmonyPatch(nameof(MobilePartyVisualManager.OnTick))]
        [HarmonyPrefix]
        private static bool OnTickPrefix(MobilePartyVisualManager __instance, float realDt, float dt)
        {
            if (Mission.Current != null) return false;

            PrepareDirtyPartyVisualBuffer(
                ref __instance._dirtyPartyVisualCount,
                ref __instance._dirtyPartiesList,
                __instance._visualsFlattened.Count);
            TWParallel.For(0, __instance._visualsFlattened.Count, delegate (int startInclusive, int endExclusive)
            {
                for (int i = startInclusive; i < endExclusive; i++)
                {
                    if (i >= __instance._visualsFlattened.Count)
                    {
                        Logger.Warning("Index {index} was out of bounds for visuals flattened list of size {size}", i, __instance._visualsFlattened.Count);
                        continue;
                    }

                    // Skip a visual whose party has been removed (IsActive == false) or unhooked: its native
                    // scene entity is already freed, so the native Tick below throws AccessViolationException —
                    // a corrupted-state exception the try/catch cannot catch, so it hard-crashes the game.
                    var visual = __instance._visualsFlattened[i];
                    var party = visual?.MapEntity?.MobileParty;  
                    if (party == null || !party.IsActive)
                        continue;

                    try
                    {
                        visual.Tick(dt, realDt, ref __instance._dirtyPartyVisualCount, ref __instance._dirtyPartiesList);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to tick party visual");
                    }
                }
            });
            for (int num = 0; num < __instance._dirtyPartyVisualCount + 1; num++)
            {
                try
                {
                    __instance._dirtyPartiesList[num].ValidateIsDirty();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to validate is party visual dirty");
                }
            }
            for (int num2 = __instance._fadingPartiesFlatten.Count - 1; num2 >= 0; num2--)
            {
                try {
                    __instance._fadingPartiesFlatten[num2].TickFadingState(realDt, dt);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to tick fading state");
                }
            }

            return false;
        }

        internal static void PrepareDirtyPartyVisualBuffer(
            ref int dirtyPartyVisualCount,
            ref MobilePartyVisual[] dirtyPartiesList,
            int requiredCapacity)
        {
            dirtyPartyVisualCount = -1;
            if (dirtyPartiesList.Length >= requiredCapacity)
                return;

            int doubledCapacity = dirtyPartiesList.Length > int.MaxValue / 2
                ? requiredCapacity
                : dirtyPartiesList.Length * 2;
            int newCapacity = Math.Max(requiredCapacity, Math.Max(1, doubledCapacity));
            Array.Resize(ref dirtyPartiesList, newCapacity);
        }
    }

    /// <summary>
    /// Grows the optional Naval DLC manager's independent dirty-visual buffer.
    /// </summary>
    [HarmonyPatch]
    public class NavalMobilePartyVisualManagerPatches
    {
        private const string ManagerTypeName = "NavalDLC.View.Map.Managers.NavalMobilePartyVisualManager";
        private static Type managerType;
        private static FieldInfo dirtyPartyVisualCountField;
        private static FieldInfo dirtyPartiesListField;
        private static FieldInfo visualsFlattenedField;
        private static PropertyInfo currentProperty;
        private static MethodInfo onInitializeMethod;
        private static MethodInfo onTickMethod;

        [HarmonyPrepare]
        public static bool Prepare() => TryResolveManagerType();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return onInitializeMethod;
            yield return onTickMethod;
        }

        [HarmonyPrefix]
        private static void Prefix(object __instance, MethodBase __originalMethod)
        {
            int requiredCapacity = __originalMethod.Name == "OnInitialize"
                ? MobileParty.All.Count
                : ((ICollection)visualsFlattenedField.GetValue(__instance)).Count;
            PrepareDirtyPartyVisualBuffer(__instance, requiredCapacity);
        }

        internal static bool TryGetBufferState(
            out int visualCount,
            out int bufferCapacity,
            out int dirtyCount)
        {
            visualCount = 0;
            bufferCapacity = 0;
            dirtyCount = -1;
            if (!TryResolveManagerType())
                return false;

            object manager = currentProperty.GetValue(null);
            if (manager == null)
                return false;

            visualCount = ((ICollection)visualsFlattenedField.GetValue(manager)).Count;
            bufferCapacity = ((Array)dirtyPartiesListField.GetValue(manager)).Length;
            dirtyCount = (int)dirtyPartyVisualCountField.GetValue(manager);
            return true;
        }

        internal static Array PrepareDirtyPartyVisualBuffer(
            ref int dirtyPartyVisualCount,
            Array dirtyPartiesList,
            int requiredCapacity)
        {
            dirtyPartyVisualCount = -1;
            if (dirtyPartiesList.Length >= requiredCapacity)
                return dirtyPartiesList;

            int doubledCapacity = dirtyPartiesList.Length > int.MaxValue / 2
                ? requiredCapacity
                : dirtyPartiesList.Length * 2;
            int newCapacity = Math.Max(requiredCapacity, Math.Max(1, doubledCapacity));
            Array resizedBuffer = Array.CreateInstance(
                dirtyPartiesList.GetType().GetElementType(),
                newCapacity);
            Array.Copy(dirtyPartiesList, resizedBuffer, dirtyPartiesList.Length);
            return resizedBuffer;
        }

        private static void PrepareDirtyPartyVisualBuffer(object manager, int requiredCapacity)
        {
            int dirtyCount = (int)dirtyPartyVisualCountField.GetValue(manager);
            Array dirtyPartiesList = (Array)dirtyPartiesListField.GetValue(manager);
            dirtyPartiesList = PrepareDirtyPartyVisualBuffer(
                ref dirtyCount,
                dirtyPartiesList,
                requiredCapacity);
            dirtyPartyVisualCountField.SetValue(manager, dirtyCount);
            dirtyPartiesListField.SetValue(manager, dirtyPartiesList);
        }

        private static bool TryResolveManagerType()
        {
            if (managerType != null)
                return true;

            Type resolvedManagerType = AccessTools.TypeByName(ManagerTypeName);
            if (resolvedManagerType == null)
                return false;

            FieldInfo resolvedDirtyPartyVisualCountField = AccessTools.Field(resolvedManagerType, "_dirtyPartyVisualCount");
            FieldInfo resolvedDirtyPartiesListField = AccessTools.Field(resolvedManagerType, "_dirtyPartiesList");
            FieldInfo resolvedVisualsFlattenedField = AccessTools.Field(resolvedManagerType, "_visualsFlattened");
            PropertyInfo resolvedCurrentProperty = AccessTools.Property(resolvedManagerType, "Current");
            MethodInfo resolvedOnInitializeMethod = AccessTools.Method(resolvedManagerType, "OnInitialize");
            MethodInfo resolvedOnTickMethod = AccessTools.Method(resolvedManagerType, "OnTick");
            if (resolvedDirtyPartyVisualCountField == null ||
                resolvedDirtyPartiesListField == null ||
                resolvedVisualsFlattenedField == null ||
                resolvedCurrentProperty == null ||
                resolvedOnInitializeMethod == null ||
                resolvedOnTickMethod == null)
            {
                return false;
            }

            dirtyPartyVisualCountField = resolvedDirtyPartyVisualCountField;
            dirtyPartiesListField = resolvedDirtyPartiesListField;
            visualsFlattenedField = resolvedVisualsFlattenedField;
            currentProperty = resolvedCurrentProperty;
            onInitializeMethod = resolvedOnInitializeMethod;
            onTickMethod = resolvedOnTickMethod;
            managerType = resolvedManagerType;
            return true;
        }
    }
}
