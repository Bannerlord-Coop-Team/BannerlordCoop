using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.MapSpeedModifier.OnMapSpeedChange
{
    [HarmonyPatch(typeof(Campaign))]
    [HarmonyPatch(nameof(Campaign.TimeControlMode), MethodType.Setter)]
    class TimeControlOverrider
    {
        static void Prefix(ref CampaignTimeControlMode value)
        {

            var newSpeed = MapSpeedResolver.Resolve(value, true);

            value = newSpeed;

        }

    }
}