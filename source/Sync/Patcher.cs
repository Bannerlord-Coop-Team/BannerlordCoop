using System;
using HarmonyLib;

namespace Sync
{
    public static class Patcher
    {
        private static readonly Lazy<Harmony> m_HarmonyInstance =
            new Lazy<Harmony>(() => new Harmony("Sync.Patcher.Harmony"));

        public static Harmony HarmonyInstance => m_HarmonyInstance.Value;
        public static object HarmonyLock { get; } = new object();
    }
}
