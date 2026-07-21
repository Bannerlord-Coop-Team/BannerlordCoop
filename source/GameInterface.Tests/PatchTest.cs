using Common.Util;
using HarmonyLib;
using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace GameInterface.Tests
{
    public class PatchTest
    {
        [Fact]
        public void HarmonyPatchesAll()
        {
            var harmony = new Harmony("Test");

            harmony.PatchAll(typeof(GameInterface).Assembly);
        }

        [Fact]
        public void PlayerLeaveSiegeEncounterPatch_HooksDontGetInvolved()
        {
            var harmony = new Harmony(nameof(PlayerLeaveSiegeEncounterPatch_HooksDontGetInvolved));
            try
            {
                var patched = harmony.CreateClassProcessor(typeof(PlayerLeaveSiegeEncounterPatch)).Patch();

                Assert.Contains(
                    patched,
                    method => method.Name.Contains(nameof(EncounterGameMenuBehavior.break_in_leave_consequence)));
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);
            }
        }

        [Fact]
        public void PlayerLeaveSiegeEncounterPatch_NeutralPartyRequestsSynchronizedLeave()
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();

            Assert.True(PlayerLeaveSiegeEncounterPatch.ShouldRequestLeave(party));
        }

        [Fact]
        public void PlayerLeaveSiegeEncounterPatch_SiegeParticipantUsesVanillaCleanup()
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            besiegerCamp.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            party._besiegerCamp = besiegerCamp;

            Assert.False(PlayerLeaveSiegeEncounterPatch.ShouldRequestLeave(party));
        }

        [Fact]
        public void PlayerLeaveSiegeEncounterPatch_ArmyFollowerUsesVanillaCleanup()
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var leader = ObjectHelper.SkipConstructor<MobileParty>();
            var army = ObjectHelper.SkipConstructor<Army>();
            army.LeaderParty = leader;
            party._army = army;

            Assert.False(PlayerLeaveSiegeEncounterPatch.ShouldRequestLeave(party));
        }

        private readonly Dictionary<Type, Delegate> BranchLookup = new Dictionary<Type, Delegate>()
        {
            { typeof(Hideout), Delegate.CreateDelegate(typeof(TestClass.GetObjectDelegate), null, typeof(TestClass).GetMethod(nameof(TestClass.GetObject))) }
        };

        [Fact]
        public void DynamicInvokeTest()
        {
            object obj = null;
            var testClass = new TestClass();
            BranchLookup[typeof(Hideout)].DynamicInvoke(testClass, "someid", obj);
        }
    }

    class TestClass
    {

        public delegate bool GetObjectDelegate(TestClass instance, string id, out object obj);
        public bool GetObject(string id, out object obj)
        {
            obj = 5;
            return true;
        }
    }

}
