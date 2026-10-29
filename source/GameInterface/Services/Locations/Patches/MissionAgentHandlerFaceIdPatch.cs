using HarmonyLib;
using SandBox.Missions.MissionLogics;

namespace GameInterface.Services.Locations.Patches;

[HarmonyPatch(typeof(MissionAgentHandler), nameof(MissionAgentHandler.GetAllProps))]
internal class MissionAgentHandlerFaceIdPatch
{
    // vanilla keeps these static and only sets them when the scene has a navigation_mesh_deactivator,
    // so a tavern entered after some village centers keeps the village id and disables most of its seats
    static void Prefix()
    {
        MissionAgentHandler._disabledFaceId = -1;
        MissionAgentHandler._disabledFaceIdForAnimals = 1;
    }
}
