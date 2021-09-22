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
    public class ClientServerTest
    {
        GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client2 = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        TestEnvironment environment;

        [TestInitialize]
        public void Setup()
        {
            List<GameInstance> instances = new List<GameInstance>
            {
                host,
                client,
                client2
            };

            environment = new TestEnvironment(instances);
        }

            [TestMethod]
        public void ClientServer_IsConnected_StartSession()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            environment.OnRegistrationFinished += (instance) =>
            {
                instance.OnGameStateChanged += (state) => {
                    if (state == GameStates.MainMenuReadyState)
                    {
                        instance.SendCommand("StartCoop");
                    }
                    else if(state == GameStates.UnspecifiedDedicatedServerState)
                    {
                        tcs.SetResult(true);
                    }
                    else if (!typeof(GameStates)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                     .ToDictionary(f => f.Name,
                            f => (string)f.GetValue(null)).Values.Contains(state))
                    {
                        throw new Exception($"{state} not in GameStates");
                    };
                };

                instance.OnCommandResponse += (response) =>
                {
                    Console.WriteLine(response);
                };
            };

            tcs.Task.Wait();
        }
    }
}
