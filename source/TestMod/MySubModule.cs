using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.SaveSystem;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.SaveLoad;
using SandBox;
using TaleWorlds.InputSystem;
using System.IO;
using TaleWorlds.Library;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.Source.Missions;
using SandBox.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using SandBox.Source.Missions.Handlers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.Engine.InputSystem;
using TaleWorlds.MountAndBlade.View.Screen;
using SandBox.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

namespace CoopTestMod
{

    //[Serializable]
    //public class AgentSerilizer : CustomSerializer
    //{
    //    public AgentSerilizer(Agent agent) : base(agent)
    //    {

    //    }
    //    Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
    //    public override object Deserialize()
    //    {



    //        foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
    //        {
    //            entry.Key.SetValue(newClan, entry.Value.Deserialize());
    //        }
    //        base.Deserialize(newClan);
    //    }

    //    public override void ResolveReferenceGuids()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}




    public class MySubModule : MBSubModuleBase
    {
        private Agent _otherAgent;
        private Agent _player;

        private Socket sender;
        private Socket listener;
        private Socket handler;
        private bool isServer = false;
        private UIntPtr playerPtr;
        private UIntPtr otherAgentPtr;
        Func<UIntPtr, Vec3> getPosition;
        PositionRefDelegate setPosition;
        List<MatrixFrame> _initialSpawnFrames;
        IPEndPoint remoteEP;
        MatrixFrame randomElement2;
        float t;
        AgentBuildData agentBuildData;
        AgentBuildData agentBuildData2;
        bool subModuleLoaded = false;
        bool battleLoaded = false;

        // custom delegate is needed since SetPosition uses a ref Vec3
        delegate void PositionRefDelegate(UIntPtr agentPtr, ref Vec3 position);

        // utility to keep trying to connect to server if it fails
        public bool ClientConnect(IPEndPoint remoteEP)
        {
            try
            {
                sender.Connect(remoteEP);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // thread to allow connections and handle data sent by the client
        public void StartServer()
        {
            // Get Host IP Address that is used to establish a connection
            // In this case, we get one IP address of localhost that is IP : 127.0.0.1
            // If a host has multiple addresses, you will get a list of addresses
            IPHostEntry host = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = host.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            try
            {

                // Create a Socket that will use Tcp protocol
                listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                // A Socket must be associated with an endpoint using the Bind method
                listener.Bind(localEndPoint);
                // Specify how many requests a Socket can listen before it gives Server busy response.
                // We will listen 10 requests at a time
                listener.Listen(1000);

                Console.WriteLine("Waiting for a connection...");
                handler = listener.Accept();

                // Incoming data from the client.

                byte[] bytes = null;

                while (true)
                {
                    try
                    {
                        bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        float x = BitConverter.ToSingle(bytes, 0);
                        float y = BitConverter.ToSingle(bytes, 4);
                        float z = BitConverter.ToSingle(bytes, 8);
                        uint movementFlag = BitConverter.ToUInt32(bytes, 12);
                        uint eventFlag = BitConverter.ToUInt32(bytes, 16);
                        float moveX = BitConverter.ToSingle(bytes, 20);
                        float moveY = BitConverter.ToSingle(bytes, 24);
                        float looX = BitConverter.ToSingle(bytes, 28);
                        float lookY = BitConverter.ToSingle(bytes, 32);
                        Vec3 pos = new Vec3(x, y, z);
                        Agent.UsageDirection direction = Agent.MovementFlagToDirection((Agent.MovementControlFlag)movementFlag);
                        if (Mission.Current != null && playerPtr != UIntPtr.Zero && otherAgentPtr != UIntPtr.Zero)
                        {


                            setPosition(otherAgentPtr, ref pos);
                            if (movementFlag != 0)
                            {
                                _otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;

                            }


                            _otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;
                            _otherAgent.SetMovementDirection(new Vec2(moveX, moveY));
                            _otherAgent.AttackDirectionToMovementFlag(direction);
                            _otherAgent.DefendDirectionToMovementFlag(direction);
                            _otherAgent.MovementInputVector = new Vec2(looX, lookY);

                        }
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("wouterror.txt", ex.Message);
                    }




                }





            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }

        // thread to receive the data as the client
        public void StartClient()
        {
            byte[] bytes = new byte[1024];

            try
            {
                IPHostEntry host = Dns.GetHostEntry("localhost");
                IPAddress ipAddress = host.AddressList[0];
                remoteEP = new IPEndPoint(ipAddress, 11000);

                // Create a TCP/IP  socket.
                sender = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.
                try
                {
                    // Connect to Remote EndPoint
                    while (!ClientConnect(remoteEP))
                    {
                        Console.WriteLine("Unable to connect to server, waiting 5 seconds and trying again");
                        Thread.Sleep(5000);
                    }

                    Console.WriteLine("Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.
                    byte[] msg = Encoding.ASCII.GetBytes("This is a test");


                    // Receive the response from the remote device.
                    while (true)
                    {
                        try
                        {


                            bytes = new byte[1024];
                            int bytesRec = sender.Receive(bytes);
                            float x = BitConverter.ToSingle(bytes, 0);
                            float y = BitConverter.ToSingle(bytes, 4);
                            float z = BitConverter.ToSingle(bytes, 8);
                            uint movementFlag = BitConverter.ToUInt32(bytes, 12);
                            uint eventFlag = BitConverter.ToUInt32(bytes, 16);
                            float moveX = BitConverter.ToSingle(bytes, 20);
                            float moveY = BitConverter.ToSingle(bytes, 24);
                            float looX = BitConverter.ToSingle(bytes, 28);
                            float lookY = BitConverter.ToSingle(bytes, 32);
                            Vec3 pos = new Vec3(x, y, z);
                            Agent.UsageDirection direction = Agent.MovementFlagToDirection((Agent.MovementControlFlag)movementFlag);
                            if (Mission.Current != null && playerPtr != UIntPtr.Zero && otherAgentPtr != UIntPtr.Zero)
                            {


                                setPosition(otherAgentPtr, ref pos);
                                if (movementFlag != 0)
                                {
                                    _otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;
                                }


                                _otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;
                                _otherAgent.SetMovementDirection(new Vec2(moveX, moveY));
                                _otherAgent.AttackDirectionToMovementFlag(direction);
                                _otherAgent.DefendDirectionToMovementFlag(direction);
                                _otherAgent.MovementInputVector = new Vec2(looX, lookY);

                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("wouterror.txt", ex.Message);
                        }
                    }




                }
                catch (ArgumentNullException ane)
                {
                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Console.WriteLine("SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }
                finally
                {
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }




        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            //skip intro
            FieldInfo splashScreen = TaleWorlds.MountAndBlade.Module.CurrentModule.GetType().GetField("_splashScreenPlayed", BindingFlags.Instance | BindingFlags.NonPublic);
            splashScreen.SetValue(TaleWorlds.MountAndBlade.Module.CurrentModule, true);


            // pass /server or /client to start as either or
            Thread thread = null;
            string[] array = Utilities.GetFullCommandLineString().Split(' ');
            foreach (string argument in array)
            {
                if (argument.ToLower() == "/server")
                {
                    isServer = true;
                    thread = new Thread(StartServer);
                }
                else if (argument.ToLower() == "/client")
                {
                    thread = new Thread(StartClient);
                }
            }
            thread.IsBackground = true;
            thread.Start();





        }

        // ripped straight out of arena spawns
        private Agent SpawnArenaAgent(CharacterObject character, MatrixFrame frame, bool isMain)
        {
            agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData2 = agentBuildData.Team((character == CharacterObject.PlayerCharacter) ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);
            agent.FadeIn();
            if (isMain)
            {
                agent.Controller = Agent.ControllerType.Player;

            }
            else
            {
                agent.Controller = Agent.ControllerType.None;
            }
            //if (agent.IsAIControlled)
            //{

            //    agent.SetWatchState(Agent.WatchState.Alarmed);
            //}
            //agent.Health = this._customAgentHealth;
            //agent.BaseHealthLimit = this._customAgentHealth;
            //agent.HealthLimit = this._customAgentHealth;

            return agent;
        }




        protected override void OnApplicationTick(float dt)
        {
            if (!subModuleLoaded && Module.CurrentModule.LoadingFinished)
            {
                // sub module is loaded. Isnt there a proper callback for this?
                subModuleLoaded = true;
                
                //Get all the save sames
                SaveGameFileInfo[] games = MBSaveLoad.GetSaveFiles();


                // just load the first one. 
                LoadResult result = SaveManager.Load(games[0].Name, new AsyncFileSaveDriver(), true);

                // Create our own game manager. This will help us override the OnLoaded callback and load the town
                CustomSandboxGame manager = new CustomSandboxGame(result);

                //start it
                MBGameManager.StartNewGame(manager);
            }

            // Press slash next to spawn in the arena
            if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            {
                // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
                battleLoaded = true;
                
                //two teams are created
                Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
                Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

                //players is defender team
                Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


                //find areas of spawn

                _initialSpawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                       select e.GetGlobalFrame()).ToList();
                for (int i = 0; i < _initialSpawnFrames.Count; i++)
                {
                    MatrixFrame value = _initialSpawnFrames[i];
                    value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                    _initialSpawnFrames[i] = value;
                }
                // get a random spawn point
                MatrixFrame randomElement = _initialSpawnFrames.GetRandomElement();
                //remove the point so no overlap
                _initialSpawnFrames.Remove(randomElement);
                //find another spawn point
                randomElement2 = _initialSpawnFrames.GetRandomElement();


                // spawn an instance of the player (controlled by default)
                _player = SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, true);


                //spawn another instance of the player, uncontroller (this should get synced when someone joins)
                _otherAgent = SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement2, false);


                // Our agent's pointer; set it to 0 first
                playerPtr = UIntPtr.Zero;


                // other agent's pointer
                otherAgentPtr = (UIntPtr)_otherAgent.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_otherAgent);


                // Find out agent's pointer from our agent instance
                playerPtr = (UIntPtr)_player.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_player);


                // set the weapons to the available weapons
                _player.WieldInitialWeapons();
                _otherAgent.WieldInitialWeapons();

                //// From MBAPI, get the private interface IMBAgent
                FieldInfo IMBAgentField = typeof(MBAPI).GetField("IMBAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                // get the set and get method of position
                MethodInfo getPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("GetPosition");
                MethodInfo setPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("SetPosition");


                // set the delegates to the method pointers. In case Agent class isn't enough we can invoke IMAgent directly.
                getPosition = (Func<UIntPtr, Vec3>)Delegate.CreateDelegate
                    (typeof(Func<UIntPtr, Vec3>), IMBAgentField.GetValue(null), getPositionMethod);

                setPosition = (PositionRefDelegate)Delegate.CreateDelegate(typeof(PositionRefDelegate), IMBAgentField.GetValue(null), setPositionMethod);




            }



            // Mission is loaded
            if (Mission.Current != null && playerPtr != UIntPtr.Zero)
            {
                // every 0.1 tick send an update to other endpoint
                if (t + 0.05 > Time.ApplicationTime)
                {
                    return;
                }
                // update time
                t = Time.ApplicationTime;

                // create a memory stream
                MemoryStream stream = new MemoryStream();

                // get all the values needed to sync character (there is more for actions, weapon switching, etc).
                Vec3 myPos = getPosition(playerPtr);
                uint movementFlag = (uint)_player.MovementFlags;
                uint eventFlag = (uint)_player.EventControlFlags;
                Vec2 movementDirection = _player.GetMovementDirection();
                Vec2 lookDirection = _player.MovementInputVector;

                if (myPos.IsValid)
                {
                    using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                    {
                        writer.Write(myPos.x);
                        writer.Write(myPos.y);
                        writer.Write(myPos.z);
                        writer.Write(movementFlag);
                        writer.Write(eventFlag);
                        writer.Write(movementDirection.x);
                        writer.Write(movementDirection.y);
                        writer.Write(lookDirection.x);
                        writer.Write(lookDirection.y);
                    }
                    byte[] bytes = stream.ToArray();
                    if (isServer && handler != null && handler.Connected)
                    {
                        handler.Send(bytes);
                    }
                    else if (sender != null && sender.Connected)
                    {
                        sender.Send(bytes);
                    }
                    else
                    {
                        //InformationManager.DisplayMessage(new InformationMessage("Somehow disconnected"));
                        ClientConnect(remoteEP);
                    }

                }
            }

        }

    }
}