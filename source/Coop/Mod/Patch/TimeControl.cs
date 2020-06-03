using HarmonyLib;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeControl
    {
        private static readonly PropertyPatcher _TimeControlPatcher =
            new PropertyPatcher(typeof(Campaign)).Setter(nameof(Campaign.TimeControlMode));

        public static FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; } =
            new FieldAccess<Campaign, CampaignTimeControlMode>(
                AccessTools.Field(typeof(Campaign), "_timeControlMode"));

        [PatchInitializer]
        public static void Init()
        {
            FieldChangeBuffer.TrackChanges(
                TimeControlMode,
                _TimeControlPatcher.Setters,
                () => Coop.DoSync);
        }
    }
}
