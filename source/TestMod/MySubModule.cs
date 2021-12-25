using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.SaveSystem;
using TaleWorlds.InputSystem;
using System.IO;
using TaleWorlds.Library;
using System.Linq;
using TaleWorlds.Engine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.MountAndBlade.Diamond;

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

        public class MessageParser
        {
            public void parseMessage(byte[] bytes, Agent otherAgent)
            {
                float x = BitConverter.ToSingle(bytes, 0);
                float y = BitConverter.ToSingle(bytes, 4);
                float z = BitConverter.ToSingle(bytes, 8);
                uint movementFlag = BitConverter.ToUInt32(bytes, 12);
                uint eventFlag = BitConverter.ToUInt32(bytes, 16);
                float moveX = BitConverter.ToSingle(bytes, 20);
                float moveY = BitConverter.ToSingle(bytes, 24);
                float inputVectorX = BitConverter.ToSingle(bytes, 28);
                float inputVectorY = BitConverter.ToSingle(bytes, 32);
                int cacheIndex1 = BitConverter.ToInt32(bytes, 36);
                float progress1 = BitConverter.ToSingle(bytes, 40);
                AnimFlags flags1 = (AnimFlags)BitConverter.ToUInt64(bytes, 44);
                int cacheIndex2 = BitConverter.ToInt32(bytes, 52);
                float progress2 = BitConverter.ToSingle(bytes, 56);
                AnimFlags flags2 = (AnimFlags)BitConverter.ToUInt64(bytes, 60);
                //int cacheIndex3 = BitConverter.ToInt32(bytes, 68);
                //float progress3 = BitConverter.ToSingle(bytes, 72);
                //AnimFlags flags3 = (AnimFlags)BitConverter.ToUInt64(bytes, 76);
                float lookDirectionX = BitConverter.ToSingle(bytes, 68);
                float lookDirectionY = BitConverter.ToSingle(bytes, 72);
                float lookDirectionZ = BitConverter.ToSingle(bytes, 76);

                //float targetPositionX = BitConverter.ToSingle(bytes, 80);
                //float targetPositionY = BitConverter.ToSingle(bytes, 84);
                //float targetDirectionX = BitConverter.ToSingle(bytes, 88);
                //float targetDirectionY = BitConverter.ToSingle(bytes, 92);
                //float targetDirectionZ = BitConverter.ToSingle(bytes, 96);


                Vec3 pos = new Vec3(x, y, z);
                //Vec2 targetPosition = new Vec2(targetPositionX, targetPositionY);
                //Vec3 targetDirection = new Vec3(targetDirectionX, targetDirectionY, targetDirectionZ);
                Agent.UsageDirection direction = Agent.MovementFlagToDirection((Agent.MovementControlFlag)movementFlag);



                if (Mission.Current != null && otherAgent != null)
                {

                    //otherAgent.TeleportToPosition(pos);
                    


                    //// we either don't have an action so set it to the new one or the receive action is different than our current action
                    //if (otherAgent.GetCurrentAction(0) == ActionIndexCache.act_none || otherAgent.GetCurrentAction(0).Index != cacheIndex1)
                    //{
                    //    string actionName1 = MBAnimation.GetActionNameWithCode(cacheIndex1);
                    //    otherAgent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (ulong)flags1, startProgress: progress1);

                    //}
                    //else
                    //{
                    //    otherAgent.SetCurrentActionProgress(0, progress1);
                    //}

                    //if (otherAgent.GetCurrentAction(1) == ActionIndexCache.act_none || otherAgent.GetCurrentAction(1).Index != cacheIndex2)
                    //{
                    //    string actionName2 = MBAnimation.GetActionNameWithCode(cacheIndex2);
                    //    otherAgent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (ulong)flags2, startProgress: progress2);

                    //}
                    //else
                    //{
                    //    otherAgent.SetCurrentActionProgress(1, progress2);
                    //}

                    otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;
                    otherAgent.EventControlFlags |= (Agent.EventControlFlag)eventFlag;

                    //otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;
                    //otherAgent.SetMovementDirection(new Vec2(moveX, moveY));
                    //otherAgent.AttackDirectionToMovementFlag(direction);
                    //otherAgent.DefendDirectionToMovementFlag(direction);
                    otherAgent.MovementInputVector = new Vec2(inputVectorX, inputVectorY);
                   // otherAgent.EnforceShieldUsage(direction);
                    //InformationManager.DisplayMessage(new InformationMessage("Received: " + ((Agent.EventControlFlag)eventFlag).ToString()));



                    //InformationManager.DisplayMessage(new InformationMessage("Received : X: " +  lookDirectionX + " Y: " + lookDirectionY + " | Z: " + lookDirectionZ));

                    otherAgent.LookDirection = new Vec3(lookDirectionX, lookDirectionY, lookDirectionZ);

                    InformationManager.DisplayMessage(new InformationMessage("Receiving: " + ((Agent.EventControlFlag)eventFlag).ToString()));

                    //otherAgent.SetTargetPositionAndDirection(targetPosition, targetDirection);


                    //otherAgent.SetCurrentActionProgress(1, progress2);

                    //string actionName3 = MBAnimation.GetActionNameWithCode(cacheIndex3);
                    //otherAgent.SetActionChannel(2, ActionIndexCache.Create(actionName3), additionalFlags: (ulong)flags3, startProgress: progress3);
                    //otherAgent.SetCurrentActionProgress(2, progress3);


                }
            }
        }

        private Agent _otherAgent;
        private Agent _player;

        private Socket sender;
        private Socket receiver;
        private UIntPtr playerPtr;
        private UIntPtr otherAgentPtr;
        private const int bufSize = 1024;

        Func<UIntPtr, Vec3> getPosition;
        PositionRefDelegate setPosition;
        List<MatrixFrame> _initialSpawnFrames;
        MatrixFrame randomElement2;
        float t;
        AgentBuildData agentBuildData;
        AgentBuildData agentBuildData2;
        bool subModuleLoaded = false;
        bool battleLoaded = false;
        EndPoint epFrom;
        EndPoint epTo;
        bool isServer = false;

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

        public void initSockets(string ipAddress, int sendPort, int recvPort)
        {
             sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
             receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            receiver.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            receiver.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), recvPort));

            sender.Connect(IPAddress.Parse(ipAddress), sendPort);
        }


        public void StartServer()
        {
            initSockets("127.0.0.1", 14905, 14906);
            MessageParser messageParser = new MessageParser();
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            epTo = new IPEndPoint(ipAddress, 14905);
            epFrom = new IPEndPoint(ipAddress, 14906);


            try
            {

                

                // Incoming data from the client.

                byte[] bytes = null;

                while (true)
                {
                    try
                    {
                        bytes = new byte[1024];
                        int bytesRec = receiver.ReceiveFrom(bytes, ref epFrom);
                        messageParser.parseMessage(bytes, _otherAgent);
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
                sender.Close();
                receiver.Close();
            }
        }

        // thread to receive the data as the client
        public void StartClient()
        {
            byte[] bytes = null;
            MessageParser messageParser = new MessageParser();
            initSockets("127.0.0.1", 14906, 14905);
            IPAddress ipAddress = System.Net.IPAddress.Parse("127.0.0.1");
            epTo = new IPEndPoint(ipAddress, 14906);
            epFrom = new IPEndPoint(ipAddress, 14905);
            try
            {
                while (true)
                {
                    bytes = new byte[1024];
                    int bytesRec = receiver.ReceiveFrom(bytes, ref epFrom);
                    messageParser.parseMessage(bytes, _otherAgent);
                }
              
            }
            catch(Exception e)
            {

            }
            finally
            {
                sender.Close();
                receiver.Close();
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
            agentBuildData2 = agentBuildData.Team(isMain ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
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



        bool channel2HasSomething = false;
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
                randomElement2 = randomElement;


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


                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon0).RemovePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon0).RemoveEnginePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon0).SetPhysicsState(false, true);


                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon1).RemovePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon1).RemoveEnginePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon1).SetPhysicsState(false, true);

                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon2).RemovePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon2).RemoveEnginePhysics();
                _otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon2).SetPhysicsState(false, true);



                //_otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon3).RemovePhysics();
                //_otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon3).RemoveEnginePhysics();

                //_otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon4).RemovePhysics();
                //_otherAgent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.Weapon4).RemoveEnginePhysics();


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


            

            if (Input.IsKeyReleased(InputKey.Slash))
            {
                
                //FieldInfo IMBNetwork = typeof(MBAPI).GetField("IMBNetwork", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);


                if (isServer)
                {
                    InformationManager.DisplayMessage(new InformationMessage("I am Server"));
                    try
                    {
                        GameNetwork.Initialize(new GameNetworkHandler());
                        GameNetwork.InitializeCompressionInfos();
                        FieldInfo IMBAgentField = typeof(GameNetwork).GetField("IMBAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        MethodInfo method2 = typeof(GameNetwork).GetMethod("PreStartMultiplayerOnServer", BindingFlags.Static | BindingFlags.NonPublic);
                        MethodInfo method = typeof(GameNetwork).GetMethod("InitializeServerSide", BindingFlags.Static | BindingFlags.NonPublic);
                       
                        if (method != null)
                        {
                            //MBCommon.CurrentGameType = (GameNetwork.IsDedicatedServer ? MBCommon.GameType.MultiServer : MBCommon.GameType.MultiClientServer);
                            GameNetwork.ClientPeerIndex = -1;
                            
                            
                            method.Invoke(null, new object[] { 15801});
                            //GameNetwork.StartMultiplayerOnClient("127.0.0.1", 15801, 1, 1);
                            //BannerlordNetwork.StartMultiplayerLobbyMission(LobbyMissionType.Custom);
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage("Not found!"));
                        }
                        
                        //GameNetwork.StartMultiplayerOnServer(15801);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("wouterror.txt", ex.Message);
                    }

                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("I am client"));
                    //MethodInfo initServer = IMBNetwork.GetValue(null).GetType().GetMethod("InitializeClientSide");
                    //initServer.Invoke(IMBNetwork.GetValue(null), new object[] { "127.0.0.1", 14890, 0, 0 });
                    GameNetwork.Initialize(new GameNetworkHandler());
                    GameNetwork.InitializeCompressionInfos();
                    GameNetwork.StartMultiplayerOnClient("127.0.0.1", 15801, 1, 1);
                    BannerlordNetwork.StartMultiplayerLobbyMission(LobbyMissionType.Custom);



                }


            }

            if (Input.IsKeyReleased(InputKey.BackSlash))
            {

                InformationManager.DisplayMessage(new InformationMessage("Peer Count: " + GameNetwork.NetworkPeerCount));
                InformationManager.DisplayMessage(new InformationMessage("Game Type: " + MBCommon.CurrentGameType));
                

            }
            if (Input.IsReleased(InputKey.Numpad1))
            {
                //_player.SetAIBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.AttackEntityMelee, 1f, 1f, 1f, 1f, 1f);
                // _player.SetActionChannel(1, ActionIndexCache.Create("act_defend_shield_up_1h_passive_down"), ignorePriority: true, 0);
                 InformationManager.DisplayMessage(new InformationMessage("Crouching?"));
                _otherAgent.MovementFlags |= Agent.MovementControlFlag.DefendDown;
                InformationManager.DisplayMessage(new InformationMessage(_otherAgent.EventControlFlags.ToString()));


            }




            // Mission is loaded
            if (Mission.Current != null && playerPtr != UIntPtr.Zero)
            {
                // every 0.1 tick send an update to other endpoint
                //if (t + 0.01 > Time.ApplicationTime)
                //{
                //    return;
                //}
                // update time
                t = Time.ApplicationTime;

                // create a memory stream
                MemoryStream stream = new MemoryStream();

                // get all the values needed to sync character (there is more for actions, weapon switching, etc).
                Vec3 myPos = getPosition(playerPtr);
                uint movementFlag = (uint)_player.MovementFlags;
                uint eventFlag = (uint)_player.EventControlFlags;
                Vec2 movementDirection = _player.GetMovementDirection();
                Vec2 inputVector = _player.MovementInputVector;
                ActionIndexCache cache1 = _player.GetCurrentAction(0);
                float progress1 = _player.GetCurrentActionProgress(0);
                AnimFlags flags1 = _player.GetCurrentAnimationFlag(0);
                ActionIndexCache cache2 = _player.GetCurrentAction(1);
                float progress2 = _player.GetCurrentActionProgress(1);
                AnimFlags flags2 = _player.GetCurrentAnimationFlag(1);
                Vec3 lookDirection = _player.LookDirection;

                //Vec2 targetPosition = _player.GetTargetPosition();
                //Vec3 targetDirection = _player.GetTargetDirection();
                //InformationManager.DisplayMessage(new InformationMessage("Sending: X: " + lookDirection.x + " Y: " + lookDirection.y + " | Z: " + lookDirection.z));

                if (!channel2HasSomething)
                {
                    ActionIndexCache cache3 = _player.GetCurrentAction(2);
                    if(cache3 != null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(cache3.Name));
                        channel2HasSomething = false;
                    }
                }

                //InformationManager.ClearAllMessages();
                //InformationManager.DisplayMessage(new InformationMessage("Sending: " + _player.EventControlFlags.ToString()));
                //InformationManager.DisplayMessage(new InformationMessage(cache2.Name));
                
                //InformationManager.DisplayMessage(new InformationMessage(_player.GetActionChannelCurrentActionWeight(1).ToString()));
                    
                    //throw new Exception();
                
                InformationManager.DisplayMessage(new InformationMessage("Sending: " + _player.EventControlFlags.ToString()));

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
                        writer.Write(inputVector.x);
                        writer.Write(inputVector.y);
                        writer.Write(cache1.Index);
                        writer.Write(progress1);
                        writer.Write((ulong)flags1);
                        writer.Write(cache2.Index);
                        writer.Write(progress2);
                        writer.Write((ulong)flags2);
                        writer.Write(lookDirection.x);
                        writer.Write(lookDirection.y);
                        writer.Write(lookDirection.z);
                        //writer.Write(targetPosition.x);
                        //writer.Write(targetPosition.y);
                        //writer.Write(targetDirection.x);
                        //writer.Write(targetDirection.y);
                        //writer.Write(targetDirection.z);
                    }
                    byte[] bytes = stream.ToArray();
                    if (sender != null && sender.Connected)
                    {
                        sender.Send(bytes);
                    }

                }
            }

        }

    }
}