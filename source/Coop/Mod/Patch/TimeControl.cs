using HarmonyLib;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeControl
    {
        private static readonly PropertyPatch TimeControlPatch =
            new PropertyPatch(typeof(Campaign)).InterceptSetter(nameof(Campaign.TimeControlMode));

        public static FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; } =
            new FieldAccess<Campaign, CampaignTimeControlMode>(
                AccessTools.Field(typeof(Campaign), "_timeControlMode"));

        [PatchInitializer]
        public static void Init()
        {
            FieldChangeBuffer.Intercept(TimeControlMode, TimeControlPatch.Setters, Coop.DoSync);
        }
    }
}
