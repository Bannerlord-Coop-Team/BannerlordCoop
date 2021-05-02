using BannerlordSystemTestingLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SystemTests
{
    public class SaveLoad_Test
    {
        GameInstance host = new GameInstance("/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");
        GameInstance client2 = new GameInstance("/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_");

        TestEnvironment environment;

        readonly string SAVE_NAME = "MP_SAVE_TEST";
        public SaveLoad_Test()
        {
            List<GameInstance> instances = new List<GameInstance>
            {
                host,
                client,
                //client2
            };

            environment = new TestEnvironment(instances);
        }

        [Fact]
        public void SaveLoadSystemTest()
        {
            TaskCompletionSource<bool> hostInMap = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> clientInMap = new TaskCompletionSource<bool>();

            TimeSpan load_timeout = TimeSpan.FromMinutes(5);

            environment.OnRegistrationFinished += (instance) =>
            {
                instance.OnGameStateChanged += (state) => {
                    if (state == GameStates.MainMenuReadyState)
                    {
                        instance.SendCommand("StartCoop");
                    }
                    else if (state == GameStates.MapState)
                    {
                        if(instance == host)
                        {
                            hostInMap.SetResult(true);
                        }
                        else if(instance == client)
                        {
                            clientInMap.SetResult(true);
                        }
                        else
                        {
                            throw new Exception($"{instance} is not host or client");
                        }
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
                    Trace.WriteLine(response);
                };

                instance.OnGameStateChanged += (response) =>
                {
                    Trace.WriteLine(response);
                };
            };

            Task bothInMap = Task.WhenAll(new[] { hostInMap.Task, clientInMap.Task });

            Task.WhenAny(bothInMap, Task.Delay(load_timeout)).Wait();

            Thread.Sleep(TimeSpan.FromSeconds(10));

            host.SendCommand("SaveGame", new string[] { SAVE_NAME });

            TaskCompletionSource<bool> hostInMainMenu = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> clientInMainMenu = new TaskCompletionSource<bool>();

            TimeSpan save_timeout = TimeSpan.FromMinutes(5);

            host.OnGameStateChanged += (state) =>
            {
                if (state == GameStates.InitialState)
                {
                    hostInMainMenu.SetResult(true);
                }
            };
            
            client.OnGameStateChanged += (state) => {
                if (state == GameStates.InitialState)
                {
                    clientInMainMenu.SetResult(true);
                }
            };

            client.SendCommand("ExitToMainMenu");
            clientInMainMenu.Task.Wait();

            host.SendCommand("ExitToMainMenu");
            Task bothInMainMenu = Task.WhenAll(new[] { hostInMainMenu.Task, clientInMainMenu.Task });

            Task.WhenAny(bothInMainMenu, Task.Delay(save_timeout)).Wait();

            Console.WriteLine("Done");
        }
    }
}

