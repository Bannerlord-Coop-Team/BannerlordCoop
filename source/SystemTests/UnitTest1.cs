using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BannerlordSystemTestingLibrary;
using System.Collections.Generic;
using System.Threading;
using BannerlordSystemTestingLibrary.Utils;

namespace SystemTests
{
    [TestClass]
    public class UnitTest1
    {
        GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        TestEnvironment environment;

        [TestInitialize]
        public void Setup()
        {
            //List<GameInstance> instances = new List<GameInstance>
            //{
            //    host,
            //    client
            //};

            List<GameInstance> instances = new List<GameInstance>
            {
                host
            };


            environment = new TestEnvironment(instances);
        }

        [TestMethod]
        public void TestMethod1()
        {
            while(host.Running) {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
