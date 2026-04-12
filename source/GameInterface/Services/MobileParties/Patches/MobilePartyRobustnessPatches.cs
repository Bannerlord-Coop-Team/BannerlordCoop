using HarmonyLib;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyRobustnessPatches
{
    [HarmonyPatch(nameof(MobileParty.Anchor), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Postfix(ref MobileParty __instance, ref AnchorPoint __result)
    {
        if (__result is null)
        {
            var anchor = new AnchorPoint(__instance);
            __instance.Anchor = anchor;
            __result = anchor;
        }
    }

    [HarmonyPatch(nameof(MobileParty.TickForStationaryMobileParty))]
    [HarmonyPrefix]
    private static bool TickForStationaryMobilePartyPrefix(MobileParty __instance, ref MobileParty.CachedPartyVariables variables, float dt, float realDt)
    {
        if (__instance.StationaryStartTime == CampaignTime.Never)
        {
            __instance.StationaryStartTime = CampaignTime.Now;
        }
        __instance.CheckIsDisorganized();
        __instance.DoUpdatePosition(ref variables, dt, realDt);

        return false;
    }
    [HarmonyPatch(nameof(MobileParty.DoUpdatePosition))]
    [HarmonyPrefix]
    private static bool TickForStationaryMobilePartyPrefix2(MobileParty __instance, ref MobileParty.CachedPartyVariables variables, float dt, float realDt)
    {
        Vec2 vec;
        if (variables.IsAttachedArmyMember)
        {
            if (variables.HasMapEvent || __instance.CurrentSettlement != null)
            {
                vec = Vec2.Zero;
            }
            else
            {
                Vec2 v = variables.HasMapEvent ? __instance.Army.LeaderParty.Position.ToVec2() : __instance.Army.LeaderParty.NextTargetPosition.ToVec2();
                CampaignVec2 campaignVec;
                bool flag;
                __instance.Army.LeaderParty.GetTargetCampaignPosition(ref variables, out campaignVec, out flag);
                Vec2 armyFacing = ((v - __instance.Army.LeaderParty.Position.ToVec2()).LengthSquared < 0.0025000002f) ? __instance.Army.LeaderParty.Bearing.Normalized() : (v - __instance.Army.LeaderParty.Position.ToVec2()).Normalized();
                Vec2 v2 = armyFacing.TransformToParentUnitF(__instance.Army.GetRelativePositionForParty(__instance, armyFacing));
                vec = v + v2 - __instance.VisualPosition2DWithoutError;
                if ((campaignVec.ToVec2() + v2 - __instance.VisualPosition2DWithoutError).LengthSquared < 1.0000001E-06f || vec.LengthSquared < 1.0000001E-06f)
                {
                    vec = Vec2.Zero;
                }
                float num = vec.LeftVec().Normalized().DotProduct(__instance.Army.LeaderParty.Position.ToVec2() + v2 - __instance.VisualPosition2DWithoutError);
                vec.RotateCCW((num < 0f) ? MathF.Max(num * 2f, -0.7853982f) : MathF.Min(num * 2f, 0.7853982f));
            }
        }
        else
        {
            if(variables.HasMapEvent)
            {
                if(__instance.Party.MapEvent == null)
                {
                    vec = __instance.NextTargetPosition.ToVec2() - __instance.VisualPosition2DWithoutError;
                }
                else
                {
                    vec = __instance.Party.MapEvent.Position.ToVec2();
                }
            }
            else
            {
                vec = __instance.NextTargetPosition.ToVec2() -__instance.VisualPosition2DWithoutError;
            }
        }
        float num2 = vec.Normalize();
        if (num2 < variables.NextMoveDistance)
        {
            variables.NextMoveDistance = num2;
        }
        if (__instance.BesiegedSettlement == null && __instance.CurrentSettlement == null && (variables.NextMoveDistance > 0f || variables.HasMapEvent))
        {
            Vec2 vec2 = __instance.Bearing;
            if (num2 > 0f)
            {
                vec2 = vec;
                if (!variables.IsAttachedArmyMember || !variables.HasMapEvent)
                {
                    __instance.Bearing = vec2;
                }
            }
            else if (variables.IsAttachedArmyMember && variables.HasMapEvent)
            {
                vec2 = __instance.Army.LeaderParty.Bearing;
                __instance.Bearing = vec2;
            }
            variables.NextPosition = variables.CurrentPosition + vec2 * variables.NextMoveDistance;
        }
        return false;
    }

}
