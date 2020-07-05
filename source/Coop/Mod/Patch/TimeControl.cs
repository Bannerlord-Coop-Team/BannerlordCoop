using HarmonyLib;
using Mono.Reflection;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeControl
    {
        private static readonly PropertyPatch TimeControlPatch =
            new PropertyPatch(typeof(Campaign)).InterceptSetter(nameof(Campaign.TimeControlMode));

        private static readonly PropertyPatch TimeControlLockPatch =
            new PropertyPatch(typeof(Campaign)).InterceptSetter(nameof(Campaign.TimeControlModeLock));

        public static FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; } =
            new FieldAccess<Campaign, CampaignTimeControlMode>(
                AccessTools.Field(typeof(Campaign), "_timeControlMode"));

        public static FieldAccess<Campaign, bool> TimeControlModeLock { get; } =
            new FieldAccess<Campaign, bool>(
                AccessTools.Property(typeof(Campaign), nameof(Campaign.TimeControlModeLock)).GetBackingField());

        [PatchInitializer]
        public static void Init()
        {
            FieldChangeBuffer.Intercept(TimeControlMode, TimeControlPatch.Setters, Coop.DoSync);
            FieldChangeBuffer.Intercept(TimeControlModeLock, TimeControlLockPatch.Setters, Coop.DoSync);
        }
    }
}
