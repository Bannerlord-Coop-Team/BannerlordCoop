using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MissionsShared;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CoopTestMod
{
    public class MissionNetworkBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;        
        private EventBasedNetListener listener;
        private static NetManager client;
        private int myPeerId = -1;
        private static string currentScene = "";
        private static ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>> playerTickInfo = new ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>>();
        private static ConcurrentDictionary<string, (PlayerTickInfo, Agent)> agentUpdateState = new ConcurrentDictionary<string, (PlayerTickInfo, Agent)>();
        private static ConcurrentDictionary<int, PlayerTickInfo> hostPlayerTickInfo = new ConcurrentDictionary<int, PlayerTickInfo>();
        private static volatile bool isInMission = false;



        public MissionNetworkBehavior()
        {
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


                    
                    if (messageType == MessageType.EnterLocation)
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
                        if (clientId == myPeerId)
                        {
                            ClientAgentManager.Instance().ClearAll();
                            hostPlayerTickInfo.Clear();
                            agentUpdateState.Clear();
                            playerTickInfo.Clear();
                            return;
                        }
                        MissionTaskManager.Instance().AddTask(clientId, new Action<object>((object obj) => {
                            int cId = (int)obj;

                            foreach (string agentId in playerTickInfo[cId].Keys)
                            {
                                int index = ClientAgentManager.Instance().GetIndexFromId(agentId);
                                Mission.Current.FindAgentWithIndex(index).FadeOut(false, true);
                                agentUpdateState.TryRemove(agentId, out _);

                            }

                            playerTickInfo[cId].Clear();
                            playerTickInfo.TryRemove(cId, out _);
                        }));

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
                        MissionTaskManager.Instance().AddTask((peerId, effectedId, effectorId, damage), new Action<object>((object obj) => {
                            (int, string, string, int) d = ((int, string, string, int))obj;
                            InformationManager.DisplayMessage(new InformationMessage("Damaged from: " + d.Item1 + " from agent : " + d.Item2 + " to agent: " + d.Item3 + " of " + d.Item4));
                            Agent effectectedAgent = Mission.Current.FindAgentWithIndex(ClientAgentManager.Instance().GetIndexFromId(d.Item2));
                            Agent effectorAgent = Mission.Current.FindAgentWithIndex(ClientAgentManager.Instance().GetIndexFromId(d.Item3));
                            Blow b = new Blow();
                            b.InflictedDamage = (int)d.Item4;
                            b.OwnerId = effectorAgent.Index;
                            b.Position = Vec3.One;
                            effectectedAgent.RegisterBlow(b);

                        }));


                    }

                    else if (messageType == MessageType.AddAgent)
                    {
                        int index = dataReader.GetInt();
                        string id = dataReader.GetString();
                        //agentCreationQueue.Enqueue((myPeerId, index, id, true));
                        MissionTaskManager.Instance().AddTask((myPeerId, index, id, true), new Action<object>((object obj) => {

                            (int, int, string, bool) agentCreationState = ((int, int, string, bool))obj;
                            Agent agent = Mission.Current.FindAgentWithIndex(agentCreationState.Item2);
                            NetworkAgent networkAgent = new NetworkAgent(agentCreationState.Item1, agentCreationState.Item2, agentCreationState.Item3, agent, agentCreationState.Item4);
                            ClientAgentManager.Instance().AddNetworkAgent(networkAgent);
                            InformationManager.DisplayMessage(new InformationMessage("A new agent was added from the network with Peer ID: " + agentCreationState.Item1 + " | Index: " + agentCreationState.Item2 + " | Server ID: " + agentCreationState.Item3 + " | Network Host: " + agentCreationState.Item4));

                        }));

                    }
                    else if (messageType == MissionsShared.MessageType.PlayerSync)
                    {
                        if (!isInMission)
                        {
                            return;
                        }
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
                                InformationManager.DisplayMessage(new InformationMessage("Client ID isn't valid"));
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
                                    agentUpdateState[tickInfo.Id] = (tickInfo, playerTickClientDict[tickInfo.Id]);
                                    //UpdatePlayerTick(tickInfo, playerTickClientDict[tickInfo.Id]);
                                }
                                // it doesn't contain the agent so it add to be spawned
                                else
                                {
                                    InformationManager.DisplayMessage(new InformationMessage("No agent was found; a new agent will be queued to spawn for: " + tickInfo.Id));
                                    MissionTaskManager.Instance().AddTask((payload.ClientId, tickInfo.Id), new Action<object>((object obj) =>
                                    {
                                        (int, string) agentState = ((int, string))obj;
                                        GameEntity gameEntity = Mission.Current.Scene.FindEntityWithTag("spawnpoint_player");
                                        if (gameEntity == null) return;
                                        Agent agent = SpawnAgent(CharacterObject.PlayerCharacter, gameEntity.GetFrame());
                                        NetworkAgent networkAgent = new NetworkAgent(agentState.Item1, agent.Index, agentState.Item2, agent, false);
                                        ClientAgentManager.Instance().AddNetworkAgent(networkAgent);
                                        //uint id = agent.Character.Id.SubId;
                                        playerTickInfo[agentState.Item1][agentState.Item2] = agent;
                                        InformationManager.DisplayMessage(new InformationMessage("A new agent was spawned: " + agentState.Item2 + " from client: " + agentState.Item1));
                                    }));
                                    playerTickInfo[payload.ClientId][tickInfo.Id] = null;
                                }

                            }

                        }

                        //PlayerTickInfo info = message.ClientTicks.Where(client => client.ClientId != myPeerId).First().PlayerTick.First();





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
        }




        [HarmonyPatch(typeof(Mission), "DecideAgentHitParticles")]
        public class AgentBloodPatch
        {



            static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(Mission), "RegisterBlow")]
        public class AgentDamagePatch
        {



            static bool Prefix(Agent attacker, Agent victim, GameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
            {
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.PlayerDamage);
                if (b.Position == Vec3.One)
                {
                    InformationManager.DisplayMessage(new InformationMessage("This is a server message processing..."));
                    return true;
                }
                if ((attacker.Team != Mission.Current.PlayerTeam || !ClientAgentManager.Instance().IsNetworkAgent(victim.Index)))
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
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                isInMission = true;
            }
        }


        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        public class CampaignAgentSpawnedPatch
        {


            public static void Postfix(AgentBuildData agentBuildData, bool spawnFromAgentVisuals, int formationTroopCount, ref Agent __result)
            {

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
        [HarmonyPatch(typeof(Mission), "FinalizeMission")]
        public class OnMissionExit
        {
            public static void Postfix()
            {
                isInMission = false;
                hostPlayerTickInfo.Clear();
                agentUpdateState.Clear();
                playerTickInfo.Clear();
                MissionTaskManager.Instance().Clear();
                ClientAgentManager.Instance().ClearAll();
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.ExitLocation);
                writer.Put(currentScene);
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                
            }
        }



        private Agent SpawnAgent(CharacterObject character, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);
            agent.FadeIn();
            agent.Controller = Agent.ControllerType.None;
            return agent;
        }

        private Agent SpawnArenaAgent(CharacterObject character, MatrixFrame frame, bool isMain)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData = agentBuildData.Team(isMain ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);                             //this spawns an archer
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

        public void StartArenaFight()
        {
            //reset teams if any exists

            Mission.Current.ResetMission();

            //
            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            //players is defender team
            Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


            //find areas of spawn

            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }
            //// get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();
            ////remove the point so no overlap
            //_initialSpawnFrames.Remove(randomElement);
            ////find another spawn point
            //randomElement2 = randomElement;


            //// spawn an instance of the player (controlled by default)
            SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, true);

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
                if (agent.HasMount)
                {
                    Vec3 mountPos = new Vec3(info.MountPositionX, info.MountPositionY, info.MountPositionZ);

                    if (agent.MountAgent.GetPathDistanceToPoint(ref mountPos) > 5f)
                    {
                        agent.MountAgent.TeleportToPosition(mountPos);
                    }
                    agent.MountAgent.SetMovementDirection(new Vec2(info.MovementDirectionX, info.MovementDirectionY));

                    //Currently not doing anything afaik
                    if (agent.MountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.MountAgent.GetCurrentAction(1).Index != info.MountAction1Index)
                    {
                        string mActionName2 = MBAnimation.GetActionNameWithCode(info.MountAction1Index);
                        agent.MountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: (ulong)info.MountAction1Flag, startProgress: info.MountAction1Progress);
                    }
                    else
                    {
                        agent.MountAgent.SetCurrentActionProgress(1, info.MountAction1Progress);
                    }
                    agent.MountAgent.LookDirection = new Vec3(info.LookDirectionZ, info.MountLookDirectionY, info.MountLookDirectionZ);
                    agent.MountAgent.MovementInputVector = new Vec2(info.MountInputVectorX, info.MountInputVectorY);
                    return;

                }






            }

        }


        public override void OnMissionTick(float dt)
        {

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

            MissionTaskManager.Instance().ApplyPendingTasks();

            foreach ((PlayerTickInfo, Agent) agentUpdate in agentUpdateState.Values)
            {
                if (agentUpdate.Item1 != null && agentUpdate.Item2 != null)
                {
                    //InformationManager.ClearAllMessages();
                    //InformationManager.DisplayMessage(new InformationMessage("Received Update: " + agentUpdate.playerTickInfo));
                    if (!ClientAgentManager.Instance().ContainsAgent(agentUpdate.Item1.Id))
                    {
                        Mission.Current.FindAgentWithIndex(agentUpdate.Item2.Index).FadeOut(true, true); ;
                        agentUpdateState.TryRemove(agentUpdate.Item1.Id, out _);
                        return;
                    }
                    UpdatePlayerTick(agentUpdate.Item1, agentUpdate.Item2);
                }

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

                InformationManager.DisplayMessage(new InformationMessage("There are ticks for: " + playerTickInfo.Count.ToString()));
                //InformationManager.DisplayMessage(new InformationMessage("There are spawn queues for " + agentSpawnQueue.Count));



            }



            if (Mission.Current == null || Mission.Current.MainAgent == null || Mission.Current.IsMissionEnding)
            {
                return;
            }

            foreach (NetworkAgent agent in ClientAgentManager.Instance().GetHostNetworkAgents())
            {
                if (agent.Agent == null)
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

                    }
                    PlayerTickInfo tickInfo;
                    if (hostPlayerTickInfo.ContainsKey(mbAgent.Index))
                    {
                        tickInfo = hostPlayerTickInfo[mbAgent.Index];
                    }
                    else
                    {
                        tickInfo = new PlayerTickInfo();
                        hostPlayerTickInfo[mbAgent.Index] = tickInfo;
                    }

                    tickInfo.MountAction1Index = ActionIndexCache.act_none.Index;
                    if (mbAgent.HasMount)
                    {
                        tickInfo.MountInputVectorX = mbAgent.MountAgent.MovementInputVector.X;
                        tickInfo.MountInputVectorY = mbAgent.MountAgent.MovementInputVector.Y;
                        tickInfo.MountAction1Flag = (ulong)mbAgent.MountAgent.GetCurrentAnimationFlag(1);
                        tickInfo.MountAction1Progress = mbAgent.MountAgent.GetCurrentActionProgress(1);
                        tickInfo.MountAction1Index = mbAgent.MountAgent.GetCurrentAction(1).Index;
                        tickInfo.MountLookDirectionX = mbAgent.MountAgent.LookDirection.X;
                        tickInfo.MountLookDirectionY = mbAgent.MountAgent.LookDirection.Y;
                        tickInfo.MountLookDirectionZ = mbAgent.MountAgent.LookDirection.Z;
                        tickInfo.MountMovementDirectionX = mbAgent.MountAgent.GetMovementDirection().X;
                        tickInfo.MountMovementDirectionY = mbAgent.MountAgent.GetMovementDirection().Y;
                        tickInfo.MountPositionX = mbAgent.MountAgent.Position.X;
                        tickInfo.MountPositionY = mbAgent.MountAgent.Position.Y;
                        tickInfo.MountPositionZ = mbAgent.MountAgent.Position.Z;

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
