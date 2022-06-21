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
using HarmonyLib;
using NetworkMessages.FromServer;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using SandBox;
using TaleWorlds.Engine.Screens;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI;
using LiteNetLib;
using MissionsShared;
using ProtoBuf;
using System.Collections.Concurrent;
using TaleWorlds.ObjectSystem;
using LiteNetLib.Utils;
using SandBox.BoardGames;

namespace CoopTestMod
{
    struct EquipmentHitPoint
    {
        public bool IsShield { get; private set; }
        public short HitPoint { get; private set; }
        public EquipmentIndex Index { get; private set; }

        public EquipmentHitPoint(bool _isShield, short _hitPoint, EquipmentIndex _index)
        {
            IsShield = _isShield;
            HitPoint = _hitPoint;
            Index = _index;
        }
    }


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




        private Socket sender;
        private Socket receiver;
        private const int bufSize = 1024;
        //private static MethodInfo OnAgentShootMissileMethod = typeof(Mission).GetMethod("OnAgentShootMissile",BindingFlags.NonPublic|BindingFlags.Instance);
        private static ConcurrentDictionary<int, ConcurrentDictionary<uint, Agent>> playerTickInfo = new ConcurrentDictionary<int, ConcurrentDictionary<uint, Agent>>();
        List<MatrixFrame> _initialSpawnFrames;
        float t;
        AgentBuildData agentBuildData;
        AgentBuildData agentBuildData2;
        bool subModuleLoaded = false;
        bool battleLoaded = false;
        bool isServer = false;
        ConcurrentQueue<AgentState> agentSpawnQueue = new ConcurrentQueue<AgentState>();
        ConcurrentQueue<BoardGameMoveEvent> boardGameQueue = new ConcurrentQueue<BoardGameMoveEvent>();
        ConcurrentQueue<int> despawnAgentQueue = new ConcurrentQueue<int>();

        Vec2 mInputVector;
        AnimFlags mFlags1;
        float mProgress1;
        ActionIndexCache mCache1;

        EventBasedNetListener listener;
        private static NetManager client;

        private int myPeerId = -1;


        private static string currentScene = "";


        public static bool MyAgentShot = false;

        public static object MyAgent { get; private set; }

        // custom delegate is needed since SetPosition uses a ref Vec3
        //delegate void PositionRefDelegate(UIntPtr agentPtr, ref Vec3 position);


        private static ConcurrentDictionary<uint, AgentUpdate> agentUpdateState = new ConcurrentDictionary<uint, AgentUpdate>();

        private PlayerTickInfo playerMainTickInfo = new PlayerTickInfo();


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

        public class AgentState
        {
            public int clientId;
            public uint id;

            public AgentState(int clientId, uint id)
            {
                this.clientId = clientId;
                this.id = id;
            }
        }


        public class AgentUpdate
        {
            public Agent agent;
            public PlayerTickInfo playerTickInfo;
            public AgentUpdate(PlayerTickInfo playerTickInfo, Agent agent)
            {
                this.playerTickInfo = playerTickInfo;
                this.agent = agent;
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

        private Agent SpawnAgent(CharacterObject character, MatrixFrame frame)
        {
            agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData2 = agentBuildData.Team(Mission.Current.PlayerTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);
            agent.FadeIn();
            agent.Controller = Agent.ControllerType.None;
            return agent;
        }

        private void UpdatePlayerTick(PlayerTickInfo info, Agent agent)
        {
            if (Mission.Current != null && agent != null && Mission.Current.IsLoadingFinished)
            {
                if (agent.Health <= 0)
                {
                    return;
                }

                Vec3 pos = new Vec3(info.PosX, info.PosY, info.PosZ);

                if (agent.GetPathDistanceToPoint(ref pos) > 5f)
                {
                    agent.TeleportToPosition(pos);
                }

                //otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;
                //otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;

                //InformationManager.DisplayMessage(new InformationMessage(ch1.ToString()));

                agent.EventControlFlags = 0U;
                if (info.crouchMode)
                {

                    agent.EventControlFlags |= Agent.EventControlFlag.Crouch;
                }
                else
                {
                    agent.EventControlFlags |= Agent.EventControlFlag.Stand;
                }


                agent.LookDirection = new Vec3(info.LookDirectionX, info.LookDirectionY, info.LookDirectionZ);
                agent.MovementInputVector = new Vec2(info.InputVectorX, info.InputVectorY);
                uint eventFlag = info.EventFlag;
                if (eventFlag == 1u)
                {
                    agent.EventControlFlags |= Agent.EventControlFlag.Dismount;
                }
                if (eventFlag == 2u)
                {
                    agent.EventControlFlags |= Agent.EventControlFlag.Mount;
                }
                if (eventFlag == 0x400u)
                {
                    //InformationManager.DisplayMessage(new InformationMessage("Toggled"));
                    agent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
                }


                if (agent.HasMount)
                {
                    agent.MountAgent.SetMovementDirection(new Vec2(info.MountInputVectorX, info.MountInputVectorY));

                    //Currently not doing anything afaik
                    //if (otherAgent.MountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || otherAgent.MountAgent.GetCurrentAction(1).Index != mCacheIndex2)
                    //{
                    //    string mActionName2 = MBAnimation.GetActionNameWithCode(mCacheIndex2);
                    //    otherAgent.MountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: (ulong)mFlags2, startProgress: mProgress2);
                    //}
                    //else
                    //{
                    //    otherAgent.MountAgent.SetCurrentActionProgress(1, mProgress2);
                    //}
                }


                if (agent.GetCurrentAction(0) == ActionIndexCache.act_none || agent.GetCurrentAction(0).Index != info.Action0Index)
                {
                    string actionName1 = MBAnimation.GetActionNameWithCode(info.Action0Index);
                    agent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (ulong)info.Action0Flag, startProgress: info.Action0Progress);

                }
                else
                {
                    agent.SetCurrentActionProgress(0, info.Action0Progress);
                }
                agent.MovementFlags = 0U;

                if ((int)info.Action0CodeType >= (int)Agent.ActionCodeType.DefendAllBegin && (int)info.Action0CodeType <= (int)Agent.ActionCodeType.DefendAllEnd)

                {
                    agent.MovementFlags = (Agent.MovementControlFlag)info.MovementFlag;
                    return;
                }



                //// we either don't have an action so set it to the new one or the receive action is different than our current action

                if ((Agent.ActionCodeType)info.Action1CodeType != Agent.ActionCodeType.BlockedMelee)
                {
                    if (agent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.GetCurrentAction(1).Index != info.Action1Index)
                    {
                        string actionName2 = MBAnimation.GetActionNameWithCode(info.Action1Index);
                        agent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (ulong)info.Action1Flag, startProgress: info.Action1Progress);

                    }
                    else
                    {
                        agent.SetCurrentActionProgress(1, info.Action1Progress);
                    }
                }
                else
                {

                    agent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true, startProgress: 100);
                }

                //otherAgent.MovementFlags = 0U;
                //otherAgent.MovementFlags = (Agent.MovementControlFlag)movementFlag;

                //InformationManager.DisplayMessage(new InformationMessage(Agent.Main.Position + ""));

                //if (health != otherAgent.Health)
                //{
                //    //InformationManager.DisplayMessage(new InformationMessage("otherAgent.Health: " + otherAgent.Health));
                //    //InformationManager.DisplayMessage(new InformationMessage("damageTaken: " + damageTaken));
                //    //InformationManager.DisplayMessage(new InformationMessage("health: " + health));

                //    if (otherAgent.Health < 0)
                //    {
                //        otherAgent.MakeDead(true, otherAgent.GetCurrentAction(1)); //Which action do we require or what does it do?
                //    }
                //}


                //if (eventFlag != 0)
                //{
                //    otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;

                //}

                //if (otherAgent.GetCurrentAction(1) != ActionIndexCache.act_none && otherAgent.CurrentGuardMode == Agent.GuardMode.None)
                //{
                //    otherAgent.EventControlFlags = Agent.EventControlFlag.Stand;
                //}


                /*
                if (otherAgent == Agent.ActionCodeType.)
                {
                    InformationManager.DisplayMessage(new InformationMessage(otherAgent.CrouchMode.ToString()));
                    otherAgent.EventControlFlags = Agent.EventControlFlag.Stand;
                } */

                // if (otherAgent.GetCurrentAction(1) != ActionIndexCache.act_none)
                // {
                //    InformationManager.DisplayMessage(new InformationMessage(otherAgent.GetCurrentActionType(1).ToString()));
                // }


                //otherAgent.EventControlFlags = (Agent.EventControlFlag)eventFlag;
                //otherAgent.SetMovementDirection(new Vec2(moveX, moveY));
                //otherAgent.AttackDirectionToMovementFlag(direction);
                //otherAgent.DefendDirectionToMovementFlag(direction);

                // otherAgent.EnforceShieldUsage(direction);
                //InformationManager.DisplayMessage(new InformationMessage("Received: " + ((Agent.EventControlFlag)eventFlag).ToString()));



                //InformationManager.DisplayMessage(new InformationMessage("Received : X: " +  lookDirectionX + " Y: " + lookDirectionY + " | Z: " + lookDirectionZ));



                //InformationManager.DisplayMessage(new InformationMessage("Receiving: " + ((Agent.EventControlFlag)eventFlag).ToString()));

                //otherAgent.SetTargetPositionAndDirection(targetPosition, targetDirection);


                //otherAgent.SetCurrentActionProgress(1, progress2);

                //string actionName3 = MBAnimation.GetActionNameWithCode(cacheIndex3);
                //otherAgent.SetActionChannel(2, ActionIndexCache.Create(actionName3), additionalFlags: (ulong)flags3, startProgress: progress3);
                //otherAgent.SetCurrentActionProgress(2, progress3);





            }

        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();



            new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop").PatchAll();

            //skip intro
            FieldInfo splashScreen = TaleWorlds.MountAndBlade.Module.CurrentModule.GetType().GetField("_splashScreenPlayed", BindingFlags.Instance | BindingFlags.NonPublic);
            splashScreen.SetValue(TaleWorlds.MountAndBlade.Module.CurrentModule, true);


            // pass /server or /client to start as either or
            Thread thread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                listener = new EventBasedNetListener();
                client = new NetManager(listener);
                client.Start();
                client.Connect("localhost" /* host ip or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);
                listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
                {
                    MissionsShared.MessageType messageType = (MessageType)dataReader.GetUInt();
                    if (messageType == MissionsShared.MessageType.PlayerSync)
                    {
                        byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                        Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);
                        FromServerTickMessage message;
                        MemoryStream stream = new MemoryStream(serializedLocation);
                        message = Serializer.DeserializeWithLengthPrefix<FromServerTickMessage>(stream, PrefixStyle.Fixed32BigEndian);

                        // get all the client info that does not belong to me
                        List<FromServerTickPayload> serverPaylod = message.ClientTicks.Where(client => client.ClientId != myPeerId).ToList();


                        // grab the first client's first agent -- needs to be changed.


                        foreach (FromServerTickPayload payload in message.ClientTicks.Where(client => client.ClientId != myPeerId))
                        {
                            if (!playerTickInfo.ContainsKey(payload.ClientId))
                            {
                                return;
                            }

                            ConcurrentDictionary<uint, Agent> playerTickClientDict = playerTickInfo[payload.ClientId];
                            foreach (PlayerTickInfo tickInfo in payload.PlayerTick)
                            {
                                // if it contains the agent, update it
                                if (playerTickClientDict.ContainsKey(tickInfo.Id))
                                {
                                    //GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                                    //if (gameEntity != null)
                                    //{

                                    //    playerTickClientDict[tickInfo.Id] = SpawnAgent(CharacterObject.PlayerCharacter, gameEntity.GetFrame());
                                    //}
                                    agentUpdateState[tickInfo.Id] = new AgentUpdate(tickInfo, playerTickClientDict[tickInfo.Id]);
                                    //UpdatePlayerTick(tickInfo, playerTickClientDict[tickInfo.Id]);
                                }
                                // it doesn't contain the agent so it add to be spawned
                                else
                                {
                                    agentSpawnQueue.Enqueue(new AgentState(payload.ClientId, tickInfo.Id));
                                    playerTickInfo[payload.ClientId][tickInfo.Id] = null;
                                }

                            }

                        }

                        //PlayerTickInfo info = message.ClientTicks.Where(client => client.ClientId != myPeerId).First().PlayerTick.First();





                    }


                    else if (messageType == MessageType.EnterLocation)
                    {
                        while (!dataReader.EndOfData)
                        {

                            int peerId = dataReader.GetInt();
                            if (peerId == myPeerId) continue;
                            if (playerTickInfo.ContainsKey(peerId)) continue;
                            playerTickInfo[peerId] = new ConcurrentDictionary<uint, Agent>();
                            //InformationManager.DisplayMessage(new InformationMessage("Entered Location: " + peerId));
                        }
                        //playerTickInfo[id] = new ConcurrentDictionary<uint, Agent>();

                    }

                    else if (messageType == MessageType.ExitLocation)
                    {
                        int peerId = dataReader.GetInt();
                        despawnAgentQueue.Enqueue(peerId);
                    }
                    else if (messageType == MessageType.ConnectionId)
                    {
                        myPeerId = dataReader.GetInt();
                    }
                    else if (messageType == MessageType.BoardGame)
                    {
                        byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                        Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);
                        BoardGameMoveEvent message;
                        MemoryStream stream = new MemoryStream(serializedLocation);
                        message = Serializer.DeserializeWithLengthPrefix<BoardGameMoveEvent>(stream, PrefixStyle.Fixed32BigEndian);
                        boardGameQueue.Enqueue(message);
                    }
                    // received something from server

                    dataReader.Recycle();
                };
                while (true)
                {
                    client.PollEvents();
                    Thread.Sleep(5); // approx. 60hz


                    FromClientTickMessage message = new FromClientTickMessage();
                    List<PlayerTickInfo> agentsList = new List<PlayerTickInfo>();
                    agentsList.Add(playerMainTickInfo);
                    message.AgentsTickInfo = agentsList;
                    MemoryStream stream = new MemoryStream();
                    Serializer.SerializeWithLengthPrefix<FromClientTickMessage>(stream, message, PrefixStyle.Fixed32BigEndian);
                    MemoryStream strm = new MemoryStream();
                    message.AgentCount = 1;
                    using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(strm))
                    {
                        writer.Write((uint)MessageType.PlayerSync);
                        writer.Write(stream.ToArray());
                    }
                    client.SendToAll(strm.ToArray(), DeliveryMethod.Unreliable);

                }

                client.Stop();
            });
            thread.Start();

            //string[] array = Utilities.GetFullCommandLineString().Split(' ');
            //foreach (string argument in array)
            //{
            //    if (argument.ToLower() == "/server")
            //    {
            //        isServer = true;
            //        thread = new Thread(StartServer);
            //    }
            //    else if (argument.ToLower() == "/client")
            //    {
            //        thread = new Thread(StartClient);
            //    }
            //}
            //thread.IsBackground = true;
            //thread.Start();




        }


        private void StartArenaFight()
        {
            //reset teams if any exists

            Mission.Current.ResetMission();

            //
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
            //// get a random spawn point
            MatrixFrame randomElement = _initialSpawnFrames.GetRandomElement();
            ////remove the point so no overlap
            //_initialSpawnFrames.Remove(randomElement);
            ////find another spawn point
            //randomElement2 = randomElement;


            //// spawn an instance of the player (controlled by default)
            SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, true);
            //MyAgent = _player;


            ////spawn another instance of the player, uncontroller (this should get synced when someone joins)
            //_otherAgent = SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement2, false);


            //otherAgentHealth = _otherAgent.Health;


            //// Our agent's pointer; set it to 0 first
            //playerPtr = UIntPtr.Zero;


            //// other agent's pointer
            //otherAgentPtr = (UIntPtr)_otherAgent.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_otherAgent);


            //// Find out agent's pointer from our agent instance
            //playerPtr = (UIntPtr)_player.GetType().GetField("_pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_player);


            //// set the weapons to the available weapons
            //_player.WieldInitialWeapons();
            //_otherAgent.WieldInitialWeapons();


            ////// From MBAPI, get the private interface IMBAgent
            //FieldInfo IMBAgentField = typeof(MBAPI).GetField("IMBAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            //// get the set and get method of position
            //MethodInfo getPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("GetPosition");
            //MethodInfo setPositionMethod = IMBAgentField.GetValue(null).GetType().GetMethod("SetPosition");


            //// set the delegates to the method pointers. In case Agent class isn't enough we can invoke IMAgent directly.
            //getPosition = (Func<UIntPtr, Vec3>)Delegate.CreateDelegate
            //    (typeof(Func<UIntPtr, Vec3>), IMBAgentField.GetValue(null), getPositionMethod);

            //setPosition = (PositionRefDelegate)Delegate.CreateDelegate(typeof(PositionRefDelegate), IMBAgentField.GetValue(null), setPositionMethod);
        }


        public void SpawnAgentAtLocation()
        {

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
            Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);                             //this spawns an archer
            //Agent agent = mission.SpawnAgent(agentBuildData2.InitialDirection(vec).NoHorses(true).Equipment(CharacterObject.Find("conspiracy_guardian").Equipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);    //this spawns a spearman
            agent.FadeIn();
            if (isMain)
            {
                agent.Controller = Agent.ControllerType.Player;

            }
            else
            {

                agent.Controller = Agent.ControllerType.None;

            }

            return agent;
        }



        // get the scene ID, pass it to the server
        [HarmonyPatch(typeof(Mission), "AfterStart")]
        public class CampaignMissionPatch
        {


            public static void Postfix()
            {

                string scene = Mission.Current.SceneName;
                currentScene = scene;
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.EnterLocation);
                writer.Put(scene);
                client.SendToAll(writer, DeliveryMethod.ReliableSequenced);

                // InformationManager.DisplayMessage(new InformationMessage(Mission.Current.SceneName));

                // InformationManager.DisplayMessage(new InformationMessage(scene));
            }
        }

        //[HarmonyPatch(typeof(Mission), "AfterStart")]
        //public class CampaignPatch
        //{


        //    public static void Postfix()
        //    {
        //        foreach (Agent a in Mission.Current.Agents)
        //        {
        //            //InformationManager.DisplayMessage(new InformationMessage(a.Character.Id.ToString()));
        //        }

        //    }
        //}


        [HarmonyPatch(typeof(Mission), "OnAgentHit")]
        public class MissionOnAgentHitPatch
        {
            private static int damageDone;
            public static int DamageDone
            {
                get
                {
                    return damageDone;
                }
            }

            public static void Postfix(int damage)
            {
                //InformationManager.DisplayMessage(new InformationMessage(damage.ToString()));
                damageDone = damage;
            }
        }

        [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CalculateEffectiveMissileSpeed")]
        public class CalculateEffectiveMissileSpeedPatch
        {
            public static void Postfix(float __result)
            {
                OnAgentShootMissilePatch.num = __result;
            }
        }

        [HarmonyPatch(typeof(Mission), "AddMissileAux")]
        public class AddMissileAuxPatch
        {
            public static void Postfix(int __result, Vec3 direction)
            {
                OnAgentShootMissilePatch.num3 = __result;
                OnAgentShootMissilePatch.direction = direction;
            }
        }

        [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
        public class OnAgentShootMissilePatch
        {
            public static CreateMissile Message;
            public static int num3;
            public static Vec3 direction;
            public static float num;
            public static void Postfix(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity,
                Mat3 orientation, bool hasRigidBody, bool isPrimaryWeaponShot, int forcedMissileIndex)
            {
                if (shooterAgent.Equals(MySubModule.MyAgent))
                {
                    MySubModule.MyAgentShot = true;
                    Message = new CreateMissile(num3, shooterAgent, weaponIndex, MissionWeapon.Invalid, position, direction, num, orientation, hasRigidBody, null, isPrimaryWeaponShot);
                }
            }
        }

        [HarmonyPatch(typeof(MissionState), "FinishMissionLoading")]
        public class OnSceneCreatedPatch
        {
            public static void Postfix()
            {
                GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                if (gameEntity != null)
                {
                    //InformationManager.DisplayMessage(new InformationMessage(gameEntity.GetGlobalFrame().ToString()));
                    //_player.TeleportToPosition(gameEntity.GetGlobalFrame().origin);
                }
                else
                {
                    //InformationManager.DisplayMessage(new InformationMessage("It's null!"));
                }
                //_player = Mission.Current.MainAgent;
            }
        }
        [HarmonyPatch(typeof(Mission), "FinalizeMission")]
        public class OnMissionExit
        {
            public static void Postfix()
            {


                agentUpdateState.Clear();
                playerTickInfo.Clear();
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.ExitLocation);
                writer.Put(currentScene);
                client.SendToAll(writer, DeliveryMethod.ReliableSequenced);
                //GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                //if (gameEntity != null)
                //{
                //    InformationManager.DisplayMessage(new InformationMessage(gameEntity.GetGlobalFrame().ToString()));
                //    //_player.TeleportToPosition(gameEntity.GetGlobalFrame().origin);
                //}
                //else
                //{
                //    InformationManager.DisplayMessage(new InformationMessage("It's null!"));
                //}
                //_player = Mission.Current.MainAgent;
            }
        }

        //[HarmonyPatch(typeof(BoardGameAIBase), nameof(BoardGameAIBase.CanMakeMove))]
        //public class Board
        //{
        //    static bool Postfix(bool result)
        //    {
        //        return false;
        //    }
        //}

        [HarmonyPatch(typeof(BoardGameBase), "HandlePlayerInput")]
        public class HandlePlayerInputPatch
        {
            static void Postfix(ref Move __result)
            {
                if (!__result.IsValid) { return; }
                InformationManager.DisplayMessage(new InformationMessage(((Tile2D)__result.GoalTile).X.ToString()));
                MissionBoardGameHandler boardGameHandler = Mission.Current.GetMissionBehavior<MissionBoardGameHandler>();

                BoardGameMoveEvent boardGameMoveEvent = new BoardGameMoveEvent();
                boardGameMoveEvent.fromIndex = boardGameHandler.Board.PlayerTwoUnits.IndexOf(__result.Unit);
                boardGameMoveEvent.toTileX = ((Tile2D)__result.GoalTile).X;
                boardGameMoveEvent.toTileY = ((Tile2D)__result.GoalTile).Y;

                var netDataWriter = new NetDataWriter();
                netDataWriter.Put((uint)MessageType.BoardGame);

                using (var memoryStream = new MemoryStream())
                {
                    Serializer.SerializeWithLengthPrefix<BoardGameMoveEvent>(memoryStream, boardGameMoveEvent, PrefixStyle.Fixed32BigEndian);
                    netDataWriter.Put(memoryStream.ToArray());
                }

                client.SendToAll(netDataWriter, DeliveryMethod.ReliableSequenced);
            }
        }

        //[HarmonyPatch(typeof(BoardGameAITablut), nameof(BoardGameAITablut.CanMakeMove))]
        //public class OnCanMakeMove
        //{
        //    static bool Prefix(ref bool __result)
        //    {
        //        //InformationManager.DisplayMessage(new InformationMessage("BoardGameAITablut CanMakeMoveFalse"));
        //        __result = false;
        //        return true;
        //    }
        //}

        /*
        [HarmonyPatch(typeof(BoardGameTablut), nameof(BoardGameTablut.AIMakeMove))]
        public class TablutOverride
        {
            static bool Prefix(ref bool __result)
            {
                InformationManager.DisplayMessage(new InformationMessage("BoardGameAITablut AIMakeMove"));
                __result = false;
                return true;
            }
        }*/

        protected override void OnApplicationTick(float dt)
        {
            //InformationManager.DisplayMessage(new InformationMessage("Peer ID: " + myPeerId.ToString()));






            // Press slash next to spawn in the arena
            //if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            //{
            //    // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
            //    battleLoaded = true;
            //    Mission.Current.AddMissionBehavior(new CustomMissionBehavior());
            //    StartArenaFight();
            //}

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


            if (Mission.Current == null || !Mission.Current.IsLoadingFinished)
            {
                return;
            }

            while (!despawnAgentQueue.IsEmpty())
            {
                int clientId = -1;
                despawnAgentQueue.TryDequeue(out clientId);
                foreach (Agent agent in playerTickInfo[clientId].Values)
                {
                    agent.FadeOut(false, true);
                }

                foreach (uint agentId in playerTickInfo[clientId].Keys)
                {
                    agentUpdateState.TryRemove(agentId, out _);
                }

                playerTickInfo[clientId].Clear();
                playerTickInfo.TryRemove(clientId, out _);


            }
            while(!boardGameQueue.IsEmpty())
            {

                boardGameQueue.TryDequeue(out BoardGameMoveEvent moveEvent);
                InformationManager.DisplayMessage(new InformationMessage(moveEvent.fromIndex.ToString()));
            }

            while (!agentSpawnQueue.IsEmpty())
            {
                AgentState agentState;
                agentSpawnQueue.TryDequeue(out agentState);
                GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                if (gameEntity == null) return;
                playerTickInfo[agentState.clientId][agentState.id] = SpawnAgent(CharacterObject.PlayerCharacter, gameEntity.GetFrame());

            }
            foreach (AgentUpdate agentUpdate in agentUpdateState.Values)
            {
                if (agentUpdate.agent != null && agentUpdate.playerTickInfo != null)
                {
                    //InformationManager.ClearAllMessages();
                    //InformationManager.DisplayMessage(new InformationMessage("Received Update: " + agentUpdate.playerTickInfo));
                    UpdatePlayerTick(agentUpdate.playerTickInfo, agentUpdate.agent);
                }

            }
            if (Mission.Current != null && Mission.Current.MainAgent != null && !Mission.Current.IsMissionEnding)
            {
                try
                {
                    Vec3 myPos = Mission.Current.MainAgent.Position;
                    //Vec3 myPos = Vec3.Invalid;
                    uint movementFlag = (uint)Mission.Current.MainAgent.MovementFlags;
                    uint eventFlag = (uint)Mission.Current.MainAgent.EventControlFlags;
                    Vec2 movementDirection = Mission.Current.MainAgent.GetMovementDirection();
                    Vec2 inputVector = Mission.Current.MainAgent.MovementInputVector;
                    ActionIndexCache cache0 = ActionIndexCache.act_none;
                    float progress0 = 0f;
                    AnimFlags flags0 = 0;
                    ActionIndexCache cache1 = ActionIndexCache.act_none;
                    float progress1 = 0f;
                    AnimFlags flags1 = 0;
                    Vec3 lookDirection = Mission.Current.MainAgent.LookDirection;
                    Agent.ActionCodeType actionTypeCh0 = Agent.ActionCodeType.Other;
                    Agent.ActionCodeType actionTypeCh1 = Agent.ActionCodeType.Other;
                    //int damage = MissionOnAgentHitPatch.DamageDone;
                    mCache1 = ActionIndexCache.act_none;
                    if (Mission.Current.MainAgent.Health > 0f)
                    {
                        cache0 = Mission.Current.MainAgent.GetCurrentAction(0);
                        progress0 = Mission.Current.MainAgent.GetCurrentActionProgress(0);
                        flags0 = Mission.Current.MainAgent.GetCurrentAnimationFlag(0);
                        cache1 = Mission.Current.MainAgent.GetCurrentAction(1);
                        progress1 = Mission.Current.MainAgent.GetCurrentActionProgress(1);
                        flags1 = Mission.Current.MainAgent.GetCurrentAnimationFlag(1);
                        actionTypeCh0 = Mission.Current.MainAgent.GetCurrentActionType(0);
                        actionTypeCh1 = Mission.Current.MainAgent.GetCurrentActionType(1);

                        if (Mission.Current.MainAgent.HasMount)
                        {
                            mInputVector = Mission.Current.MainAgent.MountAgent.GetMovementDirection();
                            mFlags1 = Mission.Current.MainAgent.MountAgent.GetCurrentAnimationFlag(1);
                            mProgress1 = Mission.Current.MainAgent.MountAgent.GetCurrentActionProgress(1);
                            mCache1 = Mission.Current.MainAgent.MountAgent.GetCurrentAction(1);
                        }

                    }
                    else
                    {
                        mCache1 = ActionIndexCache.act_none;
                    }
                    playerMainTickInfo.PosX = myPos.X;
                    playerMainTickInfo.PosY = myPos.Y;
                    playerMainTickInfo.PosZ = myPos.Z;
                    playerMainTickInfo.MovementFlag = movementFlag;
                    playerMainTickInfo.EventFlag = eventFlag;
                    playerMainTickInfo.MovementDirectionX = movementDirection.X;
                    playerMainTickInfo.MovementDirectionY = movementDirection.Y;
                    playerMainTickInfo.InputVectorX = inputVector.X;
                    playerMainTickInfo.InputVectorY = inputVector.Y;
                    playerMainTickInfo.Action0CodeType = (int)actionTypeCh0;
                    playerMainTickInfo.Action0Index = cache0.Index;
                    playerMainTickInfo.Action0Progress = progress0;
                    playerMainTickInfo.Action0Flag = (ulong)flags0;
                    playerMainTickInfo.Action1CodeType = (int)actionTypeCh1;
                    playerMainTickInfo.Action1Index = cache1.Index;
                    playerMainTickInfo.Action1Progress = progress1;
                    playerMainTickInfo.Action1Flag = (ulong)flags1;
                    playerMainTickInfo.LookDirectionX = lookDirection.X;
                    playerMainTickInfo.LookDirectionY = lookDirection.Y;
                    playerMainTickInfo.LookDirectionZ = lookDirection.Z;
                    playerMainTickInfo.crouchMode = Mission.Current.MainAgent.CrouchMode;
                }
                catch { }

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


                            method.Invoke(null, new object[] { 15801 });
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

            if (Input.IsKeyReleased(InputKey.Numpad1))
            {
                MissionBoardGameHandler boardGameHandler = Mission.Current.GetMissionBehavior<MissionBoardGameHandler>();
                boardGameHandler.SetBoardGame(boardGameHandler.CurrentBoardGame);

                boardGameHandler.StartBoardGame();
            }

            if (Input.IsKeyReleased(InputKey.Numpad2))
            {
                MissionBoardGameHandler boardGameHandler = Mission.Current.GetMissionBehavior<MissionBoardGameHandler>();
                //typeof(BoardGameAIBase).GetProperty("_state").SetValue(boardGameHandler.AIOpponent, 4, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
                InformationManager.DisplayMessage(new InformationMessage(boardGameHandler.AIOpponent.ToString()));
                InformationManager.DisplayMessage(new InformationMessage(boardGameHandler.CurrentBoardGame.ToString()));
            }

            if (Input.IsKeyReleased(InputKey.Numpad3))
            {
                MissionBoardGameHandler boardGameHandler = Mission.Current.GetMissionBehavior<MissionBoardGameHandler>();
                

                /*
                var movePawnToTileDelayed = typeof(BoardGameTablut).GetMethod("MovePawnToTileDelayed",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                movePawnToTileDelayed?.Invoke(boardGame, new object[] { randomUnit, randomTile, true, false, 0.25f });
                */

                /*
                MissionBoardGameHandler boardGameHandler = Mission.Current.GetMissionBehavior<MissionBoardGameHandler>();
                BoardGameTablut boardGame = (BoardGameTablut)boardGameHandler.Board;
                BoardGameAITablut aiOpponent = (BoardGameAITablut)boardGameHandler.AIOpponent;

                var randomTile = boardGame.Tiles.GetRandomElement();
                var randomUnit = boardGame.PlayerTwoUnits.GetRandomElement();

                InformationManager.DisplayMessage(new InformationMessage("Move pawn on AI opponent!"));
                boardGame.AIMakeMove(new Move(randomUnit, randomTile));*/
            }
        }
    }
}