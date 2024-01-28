using HarmonyLib;
using System.Collections.Generic;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using static GameInterface.Services.ObjectManager.ObjectManager;
using Xunit.Sdk;

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
