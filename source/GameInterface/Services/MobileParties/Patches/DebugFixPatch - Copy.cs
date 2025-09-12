//using HarmonyLib;
//using SandBox.View.Map;
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using TaleWorlds.CampaignSystem.Map;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.Party;
//using TaleWorlds.CampaignSystem.Settlements;
//using TaleWorlds.Core;
//using TaleWorlds.Engine;
//using TaleWorlds.Library;
//using TaleWorlds.MountAndBlade.View;

//namespace GameInterface.Services.MobileParties.Patches;

//[HarmonyPatch(typeof(MapScreen), "HandleLeftMouseButtonClick")]
//class DebugFixPatch2
//{

//    [HarmonyPrefix]
//    static void Prefix(MapScreen __instance, UIntPtr selectedSiegeEntityID, PartyVisual visualOfSelectedEntity, Vec3 intersectionPoint, PathFaceRecord mouseOverFaceIndex)
//    {
//        __instance._mapCameraView.HandleLeftMouseButtonClick(__instance.SceneLayer.Input.GetIsMouseActive());
//        if (!__instance._mapState.AtMenu)
//        {
//            if (((visualOfSelectedEntity != null) ? visualOfSelectedEntity.GetMapEntity() : null) != null)
//            {
//                IMapEntity mapEntity = visualOfSelectedEntity.GetMapEntity();
//                if (visualOfSelectedEntity.PartyBase == PartyBase.MainParty)
//                {
//                    MobileParty.MainParty.Ai.SetMoveModeHold();
//                    return;
//                }
//                PathFaceRecord faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(mapEntity.InteractionPosition);
//                if (__instance._mapScene.DoesPathExistBetweenFaces(faceIndex.FaceIndex, MobileParty.MainParty.CurrentNavigationFace.FaceIndex, false) && __instance._mapCameraView.ProcessCameraInput && PartyBase.MainParty.MapEvent == null)
//                {
//                    if (mapEntity.OnMapClick(__instance.SceneLayer.Input.IsHotKeyDown("MapFollowModifier")))
//                    {
//                        if (!__instance._leftButtonDoubleClickOnSceneWidget && Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward)
//                        {
//                            __instance._waitForDoubleClickUntilTime = 10f + 0.3f;
//                            Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward;
//                        }
//                        else
//                        {
//                            Campaign.Current.TimeControlMode = (__instance._leftButtonDoubleClickOnSceneWidget ? CampaignTimeControlMode.StoppableFastForward : CampaignTimeControlMode.StoppablePlay);
//                        }
//                        if (TaleWorlds.InputSystem.Input.IsGamepadActive)
//                        {
//                            if (mapEntity.IsMobileEntity)
//                            {
//                                if (mapEntity.IsAllyOf(PartyBase.MainParty.MapFaction))
//                                {
//                                    UISoundsHelper.PlayUISound("event:/ui/campaign/click_party");
//                                }
//                                else
//                                {
//                                    UISoundsHelper.PlayUISound("event:/ui/campaign/click_party_enemy");
//                                }
//                            }
//                            else if (mapEntity.IsAllyOf(PartyBase.MainParty.MapFaction))
//                            {
//                                UISoundsHelper.PlayUISound("event:/ui/campaign/click_settlement");
//                            }
//                            else
//                            {
//                                UISoundsHelper.PlayUISound("event:/ui/campaign/click_settlement_enemy");
//                            }
//                        }
//                    }
//                    MobileParty.MainParty.Ai.ForceAiNoPathMode = false;
//                    return;
//                }
//            }
//            else if (mouseOverFaceIndex.IsValid())
//            {
//                bool flag;
//                if (__instance.Input.IsControlDown() && Game.Current.CheatMode)
//                {
//                    if (MobileParty.MainParty.Army != null)
//                    {
//                        foreach (MobileParty mobileParty in MobileParty.MainParty.Army.LeaderParty.AttachedParties)
//                        {
//                            mobileParty.Position2D += intersectionPoint.AsVec2 - MobileParty.MainParty.Position2D;
//                        }
//                    }
//                    MobileParty.MainParty.Position2D = intersectionPoint.AsVec2;
//                    MobileParty.MainParty.Ai.SetMoveModeHold();
//                    foreach (MobileParty mobileParty2 in MobileParty.All)
//                    {
//                        mobileParty2.Party.UpdateVisibilityAndInspected(0f);
//                    }
//                    foreach (Settlement settlement in Settlement.All)
//                    {
//                        settlement.Party.UpdateVisibilityAndInspected(0f);
//                    }
//                    MBDebug.Print(string.Concat(new object[]
//                    {
//                            "main party cheat move! - ",
//                            intersectionPoint.x,
//                            " ",
//                            intersectionPoint.y
//                    }), 0, Debug.DebugColor.White, 17592186044416UL);
//                    flag = true;
//                }
//                else
//                {
//                    flag = Campaign.Current.MapSceneWrapper.AreFacesOnSameIsland(mouseOverFaceIndex, MobileParty.MainParty.CurrentNavigationFace, false);
//                }
//                if (flag && __instance._mapCameraView.ProcessCameraInput && MobileParty.MainParty.MapEvent == null)
//                {
//                    __instance._mapState.ProcessTravel(intersectionPoint.AsVec2);
//                    if (!__instance._leftButtonDoubleClickOnSceneWidget && Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward)
//                    {
//                        __instance._waitForDoubleClickUntilTime = 10f + 0.3f;
//                        Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward;
//                    }
//                    else
//                    {
//                        Campaign.Current.TimeControlMode = (__instance._leftButtonDoubleClickOnSceneWidget ? CampaignTimeControlMode.StoppableFastForward : CampaignTimeControlMode.StoppablePlay);
//                    }
//                }
//                __instance.OnTerrainClick();
//                return;
//            }
//        }
//        else
//        {
//            if (selectedSiegeEntityID != UIntPtr.Zero)
//            {
//                Tuple<MatrixFrame, PartyVisual> tuple = MapScreen.FrameAndVisualOfEngines[selectedSiegeEntityID];
//                __instance.OnSiegeEngineFrameClick(tuple.Item1);
//                return;
//            }
//            __instance.OnTerrainClick();
//        }
//    }
//}
