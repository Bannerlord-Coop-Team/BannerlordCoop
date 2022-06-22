using Coop.Mod;
using Coop.Tests.Persistence;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection("UsesGlobalPatcher")]
    public class ObjectManager_Test
    {
        private readonly TestEnvironment m_Environment = new TestEnvironment(2);

        Harmony harmony;

        public ObjectManager_Test()
        {
            harmony = new Harmony("test");

            var partyBaseCtor = AccessTools.Constructor(typeof(PartyBase), new Type[] { typeof(MobileParty), typeof(Settlement) });
            var InitMembersMethod = AccessTools.Method(typeof(MobileParty), "InitMembers");
            var InitCachedMethod = AccessTools.Method(typeof(MobileParty), "InitCached");
            var InitializeMethod = AccessTools.Method(typeof(MobileParty), "Initialize");

            var patchMethod = AccessTools.Method(GetType(), nameof(this.PartyBasePatch));

            HarmonyMethod patch = new HarmonyMethod(patchMethod);

            harmony.Patch(partyBaseCtor, patch);
            harmony.Patch(InitMembersMethod, patch);
            harmony.Patch(InitCachedMethod, patch);
            harmony.Patch(InitializeMethod, patch);
        }

        [HarmonyPatch(typeof(PartyBase), MethodType.Constructor, typeof(MobileParty), typeof(Settlement))]
        static bool PartyBasePatch()
        {
            return false;
        }


        //[Fact]
        //public void ObserverGC_Test()
        //{
        //    CoopObjectManager.PatchType<MobileParty>(harmony);

        //    MobileParty party = (MobileParty)Activator.CreateInstance(typeof(MobileParty));

        //    for (int x = 0; x < 50; x++)
        //    {
        //        Activator.CreateInstance(typeof(MobileParty));
        //    }
            

        //    Guid id = NetworkedObjectObserver.GetGuid(party);

        //    Assert.NotNull(NetworkedObjectObserver.GetObserver(id));

        //    Assert.Equal(51, NetworkedObjectObserver.ObjectCount());

        //    party = null;

        //    Thread.Sleep(TimeSpan.FromSeconds(1));

        //    GC.Collect();

        //    Thread.Sleep(TimeSpan.FromSeconds(5));

        //    Assert.Equal(1, NetworkedObjectObserver.ObjectCount());
        //}

        //[Fact]
        //public void ObjectManagerCreateFromServer_Test()
        //{
        //    ManagedTypesInitializer.InitializeTypes(harmony);

        //    CoopObjectManager objectManager = new CoopObjectManager();

        //    Guid guid = Guid.NewGuid();

        //    int callCounter = 0;
        //    objectManager.OnObjectCreatedFromServer += (id) => callCounter++;
        //    objectManager.OnObjectCreated += (id) => callCounter++;

        //    MobileParty party = objectManager.CreateObjectFromServer<MobileParty>(guid, new object[0]);

        //    Assert.Equal(2, callCounter);

        //    Assert.NotNull(party);
        //    Assert.Equal(guid, NetworkedObjectObserver.GetGuid(party));
        //}
    }
}
