using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.Save.Patches;

/// <summary>
/// Replaces the base game autosave rotation count with the configured count from
/// ModConfigProvider.ModOptions.AutoSaveCount.
/// </summary>
[HarmonyPatch(typeof(MBSaveLoad))]
internal static class AutoSaveCountPatches
{
    private const int DefaultAutoSaveCount = 3;

    private static int AutoSaveCount
    {
        get
        {
            int count = ModConfigProvider.ModOptions.AutoSaveCount;
            // A value below 1 is meaningless (an autosave slot must have an index)
            return count < 1 ? DefaultAutoSaveCount : count;
        }
    }

    private static bool IsConfiguredCount() => AutoSaveCount != DefaultAutoSaveCount;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MBSaveLoad.IncrementAutoSaveIndex))]
    private static bool IncrementAutoSaveIndexPrefix()
    {
        if (!IsConfiguredCount()) return true;

        MBSaveLoad.AutoSaveIndex = NextAutoSaveIndex(MBSaveLoad.AutoSaveIndex, AutoSaveCount);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MBSaveLoad.InitializeAutoSaveIndex))]
    private static bool InitializeAutoSaveIndexPrefix(string saveName)
    {
        if (!IsConfiguredCount()) return true;

        string text = string.Empty;
        if (saveName.StartsWith(MBSaveLoad.AutoSaveNamePrefix))
        {
            text = saveName;
        }
        else
        {
            string[] saveFileNames = MBSaveLoad.GetSaveFileNames();
            foreach (string fileName in saveFileNames)
            {
                if (fileName.StartsWith(MBSaveLoad.AutoSaveNamePrefix))
                {
                    text = fileName;
                    break;
                }
            }
        }

        MBSaveLoad.AutoSaveIndex =
            TryParseAutoSaveSlot(text, MBSaveLoad.AutoSaveNamePrefix, AutoSaveCount, out int index)
                ? index
                : 1;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MBSaveLoad.IsSaveFileNameReserved))]
    private static bool IsSaveFileNameReservedPrefix(string name, ref bool __result)
    {
        if (!IsConfiguredCount()) return true;

        __result = IsAutoSaveNameReserved(name, MBSaveLoad.AutoSaveNamePrefix, AutoSaveCount);
        return false;
    }

    internal static int NextAutoSaveIndex(int current, int count)
    {
        int next = current + 1;
        return next > count ? 1 : next;
    }

    internal static bool IsAutoSaveNameReserved(string name, string prefix, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            if (name == prefix + i)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool TryParseAutoSaveSlot(string name, string prefix, int count, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix)) return false;

        return int.TryParse(name.Substring(prefix.Length), out index) && index > 0 && index <= count;
    }
}