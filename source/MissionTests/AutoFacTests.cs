using Autofac;
using HarmonyLib;
using Missions;
using Missions.Services;
using Missions.Services.Network;
using Xunit;

namespace MissionTests
{
    public class PatchTests
    {

        [Fact]
        public void MissionModule_Resolve_CoopMissionNetworkBehavior()
        {
            Harmony harmony = new Harmony(nameof(PatchTests));

            harmony.PatchAll(typeof(MissionModule).Assembly);
        }
    }
}