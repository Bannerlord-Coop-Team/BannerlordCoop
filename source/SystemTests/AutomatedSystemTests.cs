using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BannerlordSystemTestingLibrary;
using System.Collections.Generic;
using System.Threading;
using BannerlordSystemTestingLibrary.Utils;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SystemTests
{
    [TestClass]
    public class AutomatedSystemTests
    {
        GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        TestEnvironment environment;

        [TestInitialize]
        /// <summary>
        /// Setup for testing environment
        /// </summary>
        public void Setup()
        {
            List<GameInstance> instances = new List<GameInstance>
            {
                host,
                client
            };

            environment = new TestEnvironment(instances);
        }
    }
}
