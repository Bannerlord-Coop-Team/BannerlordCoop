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
using HarmonyLib;
using NetworkMessages.FromServer;
using SandBox;
using LiteNetLib;
using MissionsShared;
using ProtoBuf;
using System.Collections.Concurrent;
using LiteNetLib.Utils;

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

    public class MySubModule : MBSubModuleBase
    {




        private Socket sender;
        private Socket receiver;
        //private static MethodInfo OnAgentShootMissileMethod = typeof(Mission).GetMethod("OnAgentShootMissile",BindingFlags.NonPublic|BindingFlags.Instance);
        private static ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>> playerTickInfo = new ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>>();
        List<MatrixFrame> _initialSpawnFrames;
        float t;
        AgentBuildData agentBuildData;
        AgentBuildData agentBuildData2;
        bool subModuleLoaded = false;
        bool battleLoaded = false;
        bool isServer = false;

        ConcurrentQueue<AgentState> agentSpawnQueue = new ConcurrentQueue<AgentState>();
        ConcurrentQueue<int> despawnAgentQueue = new ConcurrentQueue<int>();


        ConcurrentQueue<(int, int, string, bool)> agentCreationQueue = new ConcurrentQueue<(int, int, string, bool)>();



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


        private static ConcurrentDictionary<string, AgentUpdate> agentUpdateState = new ConcurrentDictionary<string, AgentUpdate>();

        private PlayerTickInfo playerMainTickInfo = new PlayerTickInfo();


        private static ConcurrentQueue<(int, string, string, float)> damageQueue = new ConcurrentQueue<(int, string, string, float)>();


        private static ConcurrentDictionary<int, PlayerTickInfo> hostPlayerTickInfo = new ConcurrentDictionary<int, PlayerTickInfo>();


        public class AgentState
        {
            public int clientId;
            public string id;

            public AgentState(int clientId, string id)
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

        private Agent SpawnAgent(CharacterObject character, MatrixFrame frame)
        {
            agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData2 = agentBuildData.Team(Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
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

                if (agent.GetPathDistanceToPoint(ref pos) > 1f)
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

                if ((int)info.Action1CodeType >= (int)Agent.ActionCodeType.DefendAllBegin && (int)info.Action1CodeType <= (int)Agent.ActionCodeType.DefendAllEnd)

                {
                    agent.MovementFlags = (Agent.MovementControlFlag)info.MovementFlag;
                    return;
                }


                if ((EquipmentIndex)info.MainHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.MainHand))
                {
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, (EquipmentIndex)info.MainHandIndex, false, false, agent.WieldedWeapon.CurrentUsageIndex);
                }

                if ((EquipmentIndex)info.OffHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.OffHand))
                {
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, (EquipmentIndex)info.OffHandIndex, false, false, agent.WieldedOffhandWeapon.CurrentUsageIndex);
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

                            ConcurrentDictionary<string, Agent> playerTickClientDict = playerTickInfo[payload.ClientId];
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
                            playerTickInfo[peerId] = new ConcurrentDictionary<string, Agent>();
                            //InformationManager.DisplayMessage(new InformationMessage("Entered Location: " + peerId));
                        }
                        //playerTickInfo[id] = new ConcurrentDictionary<uint, Agent>();

                    }

                    else if (messageType == MessageType.ExitLocation)
                    {
                        int clientId = dataReader.GetInt();
                        despawnAgentQueue.Enqueue(clientId);
                    }
                    else if (messageType == MessageType.ConnectionId)
                    {
                        myPeerId = dataReader.GetInt();
                    }

                    else if (messageType == MessageType.PlayerDamage)
                    {
                        int peerId = dataReader.GetInt();

                        string effectedId = dataReader.GetString();
                        string effectorId = dataReader.GetString();
                        int damage = dataReader.GetInt();
                        damageQueue.Enqueue((peerId, effectedId, effectorId, damage));


                    }

                    else if (messageType == MessageType.AddAgent)
                    {
                        int index = dataReader.GetInt();
                        string id = dataReader.GetString();
                        agentCreationQueue.Enqueue((myPeerId, index, id, true));
                    }
                    // received something from server

                    dataReader.Recycle();
                };
                while (true)
                {
                    client.PollEvents();
                    Thread.Sleep(5); // approx. 60hz


                    FromClientTickMessage message = new FromClientTickMessage();
                    List<PlayerTickInfo> agentsList = hostPlayerTickInfo.Values.ToList();
                    message.AgentsTickInfo = agentsList;
                    MemoryStream stream = new MemoryStream();
                    Serializer.SerializeWithLengthPrefix<FromClientTickMessage>(stream, message, PrefixStyle.Fixed32BigEndian);
                    MemoryStream strm = new MemoryStream();
                    message.AgentCount = hostPlayerTickInfo.Count;

                    if (message.AgentCount <= 0)
                    {
                        continue;
                    }
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
            //SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, false);


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

                agent.Controller = Agent.ControllerType.AI;

            }

            return agent;
        }





        [HarmonyPatch(typeof(Mission), "DecideAgentHitParticles")]
        public class AgentBloodPatch
        {



            static bool Prefix()
            {
                return false; // make sure you only skip if really necessary
            }
            //public static void Postfix(Agent affectedAgent, Agent affectorAgent, float damagedHp)
            //{
            //    NetDataWriter writer = new NetDataWriter();
            //    writer.Put((uint)MessageType.PlayerDamage);
            //    writer.Put(affectedAgent.Character.Id.SubId);
            //    writer.Put(affectorAgent.Character.Id.SubId);
            //    writer.Put(damagedHp);
            //    client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
            //    InformationManager.DisplayMessage(new InformationMessage("Damaged: " + damagedHp));
            //}
        }

        [HarmonyPatch(typeof(Mission), "RegisterBlow")]
        public class AgentDamagePatch
        {



            static bool Prefix(Agent attacker, Agent victim, GameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
            {
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.PlayerDamage);
                if(b.Position == Vec3.One)
                {
                    InformationManager.DisplayMessage(new InformationMessage("This is a server message processing..."));
                    return true;
                }
                if((attacker.Team != Mission.Current.PlayerTeam || !ClientAgentManager.Instance().IsNetworkAgent(victim.Index)))
                {
                    InformationManager.DisplayMessage(new InformationMessage("This is a damage to a local agent, ignoring..."));
                    return true;
                }
                writer.Put(ClientAgentManager.Instance().GetIdFromIndex(victim.Index));
                writer.Put(ClientAgentManager.Instance().GetIdFromIndex(attacker.Index));
                InformationManager.DisplayMessage(new InformationMessage("Sending Damage: " + b.InflictedDamage + " to server "));
                writer.Put(b.InflictedDamage);
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                return false; // make sure you only skip if really necessary
            }
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
            }
        }


        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        public class CampaignAgentSpawnedPatch
        {


            public static void Postfix(AgentBuildData agentBuildData, bool spawnFromAgentVisuals, int formationTroopCount, ref Agent __result)
            {
                //if (__result.Origin.IsUnderPlayersCommand)
                //{
                //    NetDataWriter writer = new NetDataWriter();
                //    writer.Put(__result.Index);
                //}
                if (Mission.Current == null || Mission.Current.PlayerTeam == null) return;
                try
                {
                    if (__result.Team != Mission.Current.PlayerTeam)
                    {
                        return;
                    }
                }
                catch { }
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.AddAgent);
                writer.Put(__result.Index);
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                InformationManager.DisplayMessage(new InformationMessage("Created Agent: " + __result.Name + " which is under my command? " + (__result.Team == Mission.Current.PlayerTeam)));

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

                ClientAgentManager.Instance().ClearAll();
                hostPlayerTickInfo.Clear();
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



        protected override void OnApplicationTick(float dt)
        {
            //InformationManager.DisplayMessage(new InformationMessage("Peer ID: " + myPeerId.ToString()));


            if (Mission.Current != null && Agent.Main != null)
            {
                //InformationManager.DisplayMessage(new InformationMessage(Agent.Main.GetWieldedItemIndex(Agent.HandIndex.MainHand)));
                //Agent.Main.SetWieldedItemIndexAsClient()
                //    Agent.Main.WieldedWeapon.CurrentUsageIndex
            }



            //Press slash next to spawn in the arena
            if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            {
                // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
                battleLoaded = true;
                Mission.Current.AddMissionBehavior(new CustomMissionBehavior());
                StartArenaFight();
            }

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

            if (Input.IsKeyReleased(InputKey.Numpad3))
            {
                string scene = Mission.Current.SceneName;
                currentScene = scene;
                foreach (Agent a in Mission.Current.AllAgents)
                {
                    if (a == null || a.Origin == null)
                    {
                        continue;
                    }
                    InformationManager.DisplayMessage(new InformationMessage("Player: " + a.Name + a.Origin.IsUnderPlayersCommand));
                }

                // InformationManager.DisplayMessage(new InformationMessage(Mission.Current.SceneName));

                InformationManager.DisplayMessage(new InformationMessage("Loaded Scene: " + scene));
                InformationManager.DisplayMessage(new InformationMessage("Is Field Battle: " + Mission.Current.IsFieldBattle.ToString()));
            }


            while (!despawnAgentQueue.IsEmpty())
            {
                int clientId ;
                despawnAgentQueue.TryDequeue(out clientId);

                foreach (string agentId in playerTickInfo[clientId].Keys)
                {
                    int index = ClientAgentManager.Instance().GetIndexFromId(agentId);
                    Mission.Current.FindAgentWithIndex(index).FadeOut(false, true);
                    agentUpdateState.TryRemove(agentId, out _);

                }

                playerTickInfo[clientId].Clear();
                playerTickInfo.TryRemove(clientId, out _);


            }


            while (!agentSpawnQueue.IsEmpty())
            {
                AgentState agentState;
                agentSpawnQueue.TryDequeue(out agentState);
                GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                if (gameEntity == null) return;
                Agent agent = SpawnAgent(CharacterObject.PlayerCharacter, gameEntity.GetFrame());
                NetworkAgent networkAgent = new NetworkAgent(agentState.clientId, agent.Index, agentState.id, agent, false);
                ClientAgentManager.Instance().AddNetworkAgent(networkAgent);
                //uint id = agent.Character.Id.SubId;
                playerTickInfo[agentState.clientId][agentState.id] = agent;

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

            while (!damageQueue.IsEmpty())
            {
                (int, string, string, float) d;
                damageQueue.TryDequeue(out d);
                InformationManager.DisplayMessage(new InformationMessage("Damaged from: " + d.Item1 + " from agent : " + d.Item2 + " to agent: " + d.Item3 + " of " + d.Item4));
                Agent effectectedAgent = Mission.Current.FindAgentWithIndex(ClientAgentManager.Instance().GetIndexFromId(d.Item2));
                Agent effectorAgent = Mission.Current.FindAgentWithIndex(ClientAgentManager.Instance().GetIndexFromId(d.Item3));
                Blow b = new Blow();
                b.InflictedDamage = (int)d.Item4;
                b.OwnerId = effectorAgent.Index;
                b.Position = Vec3.One;
                effectectedAgent.RegisterBlow(b);

            }


            while (!agentCreationQueue.IsEmpty())
            {
                (int, int, string, bool) agentCreationState;
                agentCreationQueue.TryDequeue(out agentCreationState);
                Agent agent = Mission.Current.FindAgentWithIndex(agentCreationState.Item2);
                NetworkAgent networkAgent = new NetworkAgent(agentCreationState.Item1, agentCreationState.Item2, agentCreationState.Item3, agent, agentCreationState.Item4);
                ClientAgentManager.Instance().AddNetworkAgent(networkAgent);
                InformationManager.DisplayMessage(new InformationMessage("A new agent was added from the network with Peer ID: " + agentCreationState.Item1 + " | Index: " + agentCreationState.Item2 + " | Server ID: " + agentCreationState.Item3 + " | Network Host: " + agentCreationState.Item4));
            }

            if (Input.IsKeyReleased(InputKey.Numpad5))
            {

                Blow b = new Blow(Mission.Current.MainAgent.Index);
                b.InflictedDamage = 20;
                //_player.Health = 0;
                Mission.Current.MainAgent.RegisterBlow(b);



            }

            if (Input.IsKeyReleased(InputKey.Numpad6))
            {

                InformationManager.DisplayMessage(new InformationMessage(Mission.Current.FindAgentWithIndex(1).Health.ToString()));



            }



            if (Mission.Current == null || Mission.Current.MainAgent == null || Mission.Current.IsMissionEnding)
            {
                return;
            }

            foreach(NetworkAgent agent in ClientAgentManager.Instance().GetHostNetworkAgents())
            {
                if(agent.Agent == null)
                {
                    return;
                }
                Agent mbAgent = agent.Agent;
                try
                {
                    Vec3 myPos = mbAgent.Position;
                    //Vec3 myPos = Vec3.Invalid;
                    uint movementFlag = (uint)mbAgent.MovementFlags;
                    uint eventFlag = (uint)mbAgent.EventControlFlags;
                    Vec2 movementDirection = mbAgent.GetMovementDirection();
                    Vec2 inputVector = mbAgent.MovementInputVector;
                    ActionIndexCache cache0 = ActionIndexCache.act_none;
                    float progress0 = 0f;
                    AnimFlags flags0 = 0;
                    ActionIndexCache cache1 = ActionIndexCache.act_none;
                    float progress1 = 0f;
                    AnimFlags flags1 = 0;
                    Vec3 lookDirection = mbAgent.LookDirection;
                    Agent.ActionCodeType actionTypeCh0 = Agent.ActionCodeType.Other;
                    Agent.ActionCodeType actionTypeCh1 = Agent.ActionCodeType.Other;
                    //int damage = MissionOnAgentHitPatch.DamageDone;
                    mCache1 = ActionIndexCache.act_none;
                    if (mbAgent.Health > 0f)
                    {
                        cache0 = mbAgent.GetCurrentAction(0);
                        progress0 = mbAgent.GetCurrentActionProgress(0);
                        flags0 = mbAgent.GetCurrentAnimationFlag(0);
                        cache1 = mbAgent.GetCurrentAction(1);
                        progress1 = mbAgent.GetCurrentActionProgress(1);
                        flags1 = mbAgent.GetCurrentAnimationFlag(1);
                        actionTypeCh0 = mbAgent.GetCurrentActionType(0);
                        actionTypeCh1 = mbAgent.GetCurrentActionType(1);

                        if (mbAgent.HasMount)
                        {
                            mInputVector = mbAgent.MountAgent.GetMovementDirection();
                            mFlags1 = mbAgent.MountAgent.GetCurrentAnimationFlag(1);
                            mProgress1 = mbAgent.MountAgent.GetCurrentActionProgress(1);
                            mCache1 = mbAgent.MountAgent.GetCurrentAction(1);
                        }

                    }
                    else
                    {
                        mCache1 = ActionIndexCache.act_none;
                    }
                    PlayerTickInfo tickInfo;
                    if (hostPlayerTickInfo.ContainsKey(mbAgent.Index))
                    {
                        tickInfo = hostPlayerTickInfo[mbAgent.Index];   
                    }
                    else { 
                        tickInfo = new PlayerTickInfo();
                        hostPlayerTickInfo[mbAgent.Index] = tickInfo;
                    }
                    tickInfo.Id = agent.AgentID;
                    tickInfo.PosX = myPos.X;
                    tickInfo.PosY = myPos.Y;
                    tickInfo.PosZ = myPos.Z;
                    tickInfo.MovementFlag = movementFlag;
                    tickInfo.EventFlag = eventFlag;
                    tickInfo.MovementDirectionX = movementDirection.X;
                    tickInfo.MovementDirectionY = movementDirection.Y;
                    tickInfo.InputVectorX = inputVector.X;
                    tickInfo.InputVectorY = inputVector.Y;
                    tickInfo.Action0CodeType = (int)actionTypeCh0;
                    tickInfo.Action0Index = cache0.Index;
                    tickInfo.Action0Progress = progress0;
                    tickInfo.Action0Flag = (ulong)flags0;
                    tickInfo.Action1CodeType = (int)actionTypeCh1;
                    tickInfo.Action1Index = cache1.Index;
                    tickInfo.Action1Progress = progress1;
                    tickInfo.Action1Flag = (ulong)flags1;
                    tickInfo.LookDirectionX = lookDirection.X;
                    tickInfo.LookDirectionY = lookDirection.Y;
                    tickInfo.LookDirectionZ = lookDirection.Z;
                    tickInfo.crouchMode = Mission.Current.MainAgent.CrouchMode;
                    tickInfo.MainHandIndex = (int)Mission.Current.MainAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                    tickInfo.OffHandIndex = (int)Mission.Current.MainAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);

                }
                catch { }
            }

            

        }



    }
}