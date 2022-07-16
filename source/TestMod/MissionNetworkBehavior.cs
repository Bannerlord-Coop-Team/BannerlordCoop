using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MissionsShared;
using ProtoBuf;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.ViewModelCollection.BoardGame;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.ScreenSystem;

namespace CoopTestMod
{
    public class MissionNetworkBehavior : MissionBehavior
    {
        // Mission Behavior type, required by MissionBehavior
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;       
        
        // LiteNetLib listener
        private EventBasedNetListener listener;
        // LiteNetLib Client
        public static NetManager client;
        // My Peer from LiteNetLib
        private int myPeerId = -1;
        // Current Cutscene loaded, empty by default
        private static string currentScene = "";
        // Map Client Peer ID to a dictionary of agents. Each agent can be accessed with the server's agent ID
        private static ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>> playerTickInfo = new ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>>();
        // Map server agent ID to a pair of PlayerTickInfo object and the MB Agent Object
        private static ConcurrentDictionary<string, (PlayerTickInfo, Agent)> agentUpdateState = new ConcurrentDictionary<string, (PlayerTickInfo, Agent)>();
        // Map the index of the agent to its player tick info. This will be used in the network thread to send the info
        private static ConcurrentDictionary<int, PlayerTickInfo> hostPlayerTickInfo = new ConcurrentDictionary<int, PlayerTickInfo>();
        // Atomic boolean to indicate if the mission running. This is to avoid using Mission.Current
        private static volatile bool isInMission = false;



        public MissionNetworkBehavior()
        {
            // start harmony patch
            new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop").PatchAll();


            // start network thread
            Thread thread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                listener = new EventBasedNetListener();
                client = new NetManager(listener);
                client.Start();
                client.Connect("localhost" /* host ip or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);

                // register events

                listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
                {
                    // first 32 bits is always the message type
                    MissionsShared.MessageType messageType = (MessageType)dataReader.GetUInt();


                    // different behavior based on message type
                    if (messageType == MessageType.EnterLocation)
                    {
                        // grab all existing peers at the location
                        while (!dataReader.EndOfData)
                        {

                            int peerId = dataReader.GetInt();
                            if (peerId == myPeerId) continue;
                            if (playerTickInfo.ContainsKey(peerId)) continue;
                            playerTickInfo[peerId] = new ConcurrentDictionary<string, Agent>();
                        }

                    }

                    else if (messageType == MessageType.ExitLocation)
                    {
                        int clientId = dataReader.GetInt();
                        // If we are leaving from a mission, remove and clear all network related memory
                        if (clientId == myPeerId)
                        {
                            ClientAgentManager.Instance().ClearAll();
                            hostPlayerTickInfo.Clear();
                            agentUpdateState.Clear();
                            playerTickInfo.Clear();
                            return;
                        }
                        // otherwise, add a task for the game thread to process some client exiting
                        MissionTaskManager.Instance().AddTask(clientId, new Action<object>((object obj) =>
                        {
                            int cId = (int)obj;
                            // loop through the client and remove all of its agents
                            foreach (string agentId in playerTickInfo[cId].Keys)
                            {
                                int index = ClientAgentManager.Instance().GetIndexFromId(agentId);
                                Mission.Current.FindAgentWithIndex(index).FadeOut(false, true);
                                agentUpdateState.TryRemove(agentId, out _);
                                InformationManager.DisplayMessage(new InformationMessage("Removed Agent with Index: " + index));

                            }

                            playerTickInfo[cId].Clear();
                            playerTickInfo.TryRemove(cId, out _);

                        }));

                    }
                    // when connecting, server will return you peer Id
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

                        // retrieve the peer Id of the sender, effected id, effector id, and the damage
                        // then register an event to the game thread to apply the damage. Note: Vec3.One for the position is used as an indicator that this is a server damage.
                        // this must be resolved at a later time
                        MissionTaskManager.Instance().AddTask((peerId, effectedId, effectorId, damage), new Action<object>((object obj) =>
                        {
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

                    else if (messageType == MessageType.BoardGameChallenge)
                    {
                        byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                        Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);
                        BoardGameChallenge message;

                        InformationManager.DisplayMessage(new InformationMessage(serializedLocation.Length.ToString()));

                        MemoryStream stream = new MemoryStream(serializedLocation);
                        message = Serializer.DeserializeWithLengthPrefix<BoardGameChallenge>(stream, PrefixStyle.Fixed32BigEndian);

                        MissionTaskManager.Instance().AddTask((message.ChallengeRequest, message.ChallengeResponse, message.SenderAgentId, message.OtherAgentId), new Action<object>((object obj) =>
                        {
                            (bool, bool, string, string) d = ((bool, bool, string, string))obj;

                            if (d.Item1)
                            {
                                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Accept", "Pussy out",
                                    new Action(() => { AgentInteractionPatch.AcceptGameRequest(d.Item3, d.Item4 ); }), new Action(() => { })));
                            }
                            else if (d.Item2)
                            {
                                BoardGamePlayerInputPatches.PreplaceUnitsPatch.isChallenged = true;

                                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                                boardGameLogic.SetBoardGame(Settlement.CurrentSettlement.Culture.BoardGame);
                                boardGameLogic.SetStartingPlayer(false);
                                boardGameLogic.StartBoardGame();

                                Agent opposingAgent = ClientAgentManager.Instance().GetNetworkAgent(d.Item4).Agent;
                                boardGameLogic.GetType().GetProperty("OpposingAgent", BindingFlags.Public | BindingFlags.Instance).SetValue(boardGameLogic, opposingAgent);

                            }
                        }));
                    }

                    else if (messageType == MessageType.BoardGame)
                    {
                        byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                        Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);
                        BoardGameMoveEvent message;
                        MemoryStream stream = new MemoryStream(serializedLocation);
                        message = Serializer.DeserializeWithLengthPrefix<BoardGameMoveEvent>(stream, PrefixStyle.Fixed32BigEndian);
                        
                        MissionTaskManager.Instance().AddTask((message.toIndex, message.fromIndex), new Action<object>((object obj) =>
                        {
                            (int, int) d = ((int, int))obj;

                            var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                            BoardGameBase boardGame = boardGameLogic.Board;

                            if (boardGame == null)
                                return;

                            var unitToMove = boardGame.PlayerTwoUnits[d.Item2];
                            var goalTile = boardGame.Tiles[d.Item1];

                            if (boardGame is BoardGamePuluc)
                            {
                                if (d.Item1 == 11)
                                {
                                    goalTile = boardGame.Tiles[11];
                                }
                                else
                                {
                                    goalTile = boardGame.Tiles[10 - d.Item1];
                                }
                            }

                            if (unitToMove == null || goalTile == null)
                                return;

                            var boardType = boardGame.GetType();

                            boardType.GetProperty("SelectedUnit", BindingFlags.NonPublic | BindingFlags.Instance)?
                                .SetValue(boardGame, unitToMove);

                            var movePawnToTileMethod = boardType.GetMethod("MovePawnToTile", BindingFlags.NonPublic | BindingFlags.Instance);
                            movePawnToTileMethod?.Invoke(boardGame, new object[] { unitToMove, goalTile, false, true });


                        }));

                        //InformationManager.DisplayMessage(new InformationMessage($"Move received from server, unit id: {message.fromIndex}"));
                    }
                    else if (messageType == MessageType.PawnCapture)
                    {
                        byte[] serializedLocation = new byte[dataReader.RawDataSize - dataReader.Position];
                        Buffer.BlockCopy(dataReader.RawData, dataReader.Position, serializedLocation, 0, dataReader.RawDataSize - dataReader.Position);
                        PawnCapturedEvent message;
                        MemoryStream stream = new MemoryStream(serializedLocation);
                        message = Serializer.DeserializeWithLengthPrefix<PawnCapturedEvent>(stream, PrefixStyle.Fixed32BigEndian);

                        MissionTaskManager.Instance().AddTask((message.fromIndex), new Action<object>((object obj) =>
                        {
                            int d = (int)obj;

                            PawnBase unitToMove;
                            var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                            BoardGameBase boardGame = boardGameLogic.Board;

                            if (boardGame == null)
                                return;

                            if (boardGame is BoardGameSeega)
                            {
                                unitToMove = boardGame.PlayerOneUnits[d];
                            }
                            else
                            {
                                unitToMove = boardGame.PlayerTwoUnits[d];
                            }

                            if (unitToMove == null)
                                return;

                            InformationManager.DisplayMessage(new InformationMessage("Pawn Captured"));
                            boardGame.SetPawnCaptured(unitToMove);

                            //Where else is this used than Seega?
                            if (!(boardGame is BoardGameSeega))
                            {
                                boardGame.GetType().GetMethod("EndTurn", BindingFlags.NonPublic | BindingFlags.Instance)?
                                    .Invoke(boardGame, new object[] { });
                            }

                        }));
                    } else if(messageType == MessageType.BoardGameForfeit)
                    {
                        var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                        boardGameLogic.AIForfeitGame();
                    }
                    else if (messageType == MessageType.AddAgent)
                    {
                        int index = dataReader.GetInt();
                        string id = dataReader.GetString();
                        // register an event for the game thread to add agent
                        // server returns the server generated id and the index it corresponds to in the local game
                        MissionTaskManager.Instance().AddTask((myPeerId, index, id, true), new Action<object>((object obj) =>
                        {

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
                        foreach (FromServerTickPayload payload in message.ClientTicks.Where(client => client.ClientId != myPeerId))
                        {
                            // if no key exists for the client, terminate
                            if (!playerTickInfo.ContainsKey(payload.ClientId))
                            {
                                InformationManager.DisplayMessage(new InformationMessage("Client ID isn't valid"));
                                return;
                            }

                            // retireve the agent information from client ID
                            ConcurrentDictionary<string, Agent> playerTickClientDict = playerTickInfo[payload.ClientId];

                            // loop through each tick into in the payload
                            foreach (PlayerTickInfo tickInfo in payload.PlayerTick)
                            {
                                // if it contains the agent, update it
                                if (playerTickClientDict.ContainsKey(tickInfo.Id))
                                {
                                    // queue the change to the game thread
                                    agentUpdateState[tickInfo.Id] = (tickInfo, playerTickClientDict[tickInfo.Id]);
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
                                    // set the tick info agent to null to avoid accidental override of the old agent
                                    playerTickInfo[payload.ClientId][tickInfo.Id] = null;
                                }

                            }

                        }





                    }
                    // recycle reader
                    dataReader.Recycle();
                };
                while (true)
                {
                    client.PollEvents();
                    Thread.Sleep(5); // approx. 60hz

                    // retrieve local host tick info for all the agent
                    FromClientTickMessage message = new FromClientTickMessage();
                    List<PlayerTickInfo> agentsList = hostPlayerTickInfo.Values.ToList();
                    message.AgentsTickInfo = agentsList;
                    MemoryStream stream = new MemoryStream();
                    Serializer.SerializeWithLengthPrefix<FromClientTickMessage>(stream, message, PrefixStyle.Fixed32BigEndian);
                    MemoryStream strm = new MemoryStream();
                    message.AgentCount = hostPlayerTickInfo.Count;

                    // if no agents exist to send or we are not in a mission, terminate
                    if (message.AgentCount <= 0 || !isInMission)
                    {
                        continue;
                    }
                    using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(strm))
                    {
                        writer.Write((uint)MessageType.PlayerSync);
                        writer.Write(stream.ToArray());
                    }
                    // send unreiable...we don't need data arrival guarantees for the server syncs
                    client.SendToAll(strm.ToArray(), DeliveryMethod.Unreliable);

                }

                client.Stop();
            });
            thread.Start();
        }



        // this patch stops blood from showing
        [HarmonyPatch(typeof(Mission), "DecideAgentHitParticles")]
        public class AgentBloodPatch
        {
            static bool Prefix()
            {
                return false;
            }
        }
        // this patch overrides the damage calculation and applicaiton to the agent
        [HarmonyPatch(typeof(Mission), "RegisterBlow")]
        public class AgentDamagePatch
        {
            static bool Prefix(Agent attacker, Agent victim, GameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
            {
                // all damages must be send to the server
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.PlayerDamage);
                // this is to verify if the damage is from the server.
                // NOTE: this must be changed
                if (b.Position == Vec3.One)
                {
                    InformationManager.DisplayMessage(new InformationMessage("This is a server message processing..."));
                    return true;
                }
                // Check if the damage is local agent to a local agent. 
                // We don't need to let everyone know about this.
                // This should be changed in the future.
                if ((attacker.Team != Mission.Current.PlayerTeam || !ClientAgentManager.Instance().IsNetworkAgent(victim.Index)))
                {
                    InformationManager.DisplayMessage(new InformationMessage("This is a damage to a local agent, ignoring..."));
                    return true;
                }

                // Otherwise the damage is from a network agent to another network (non-local) agent, so send it to the server
                writer.Put(ClientAgentManager.Instance().GetIdFromIndex(victim.Index));
                writer.Put(ClientAgentManager.Instance().GetIdFromIndex(attacker.Index));
                InformationManager.DisplayMessage(new InformationMessage("Sending Damage: " + b.InflictedDamage + " to server "));
                writer.Put(b.InflictedDamage);
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                return false; // override the game damage and don't apply it
            }
        }



        [HarmonyPatch(typeof(MissionBoardGameLogic), "SetGameOver")]
        public class SetGameOverPatch
        {
            static bool Prefix(MissionBoardGameLogic __instance, GameOverEnum gameOverInfo)
            {
                // Closing the view (I don't know if the clear target frame is usefull)
                __instance.Handler?.Uninstall();

                Action eventGameEnded = typeof(MissionBoardGameLogic).GetField("GameEnded", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(__instance) as Action;
                eventGameEnded?.Invoke();

                __instance.Board.Reset();
                typeof(MissionBoardGameLogic).GetProperty("OpposingAgent", BindingFlags.Public | BindingFlags.Instance).SetValue(__instance, null);
                typeof(MissionBoardGameLogic).GetProperty("IsGameInProgress", BindingFlags.Public | BindingFlags.Instance).SetValue(__instance, false);

                return false;
            }
        }
        
        [HarmonyPatch(typeof(MissionBoardGameLogic), "StartConversationWithOpponentAfterGameEnd")]
        public class SetGameOverConversationPatch
        {
            static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.ForfeitGame))]
        public class ForfeitGamePatch
        {
            static void Prefix(MissionBoardGameLogic __instance)
            {
                var otherAgent = __instance.OpposingAgent;
                var otherAgentId = ClientAgentManager.Instance().GetIdFromIndex(otherAgent.Index);

                if (otherAgentId == null)
                    return;

                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint) MessageType.BoardGameForfeit);
                writer.Put(otherAgentId);

                client.SendToAll(writer, DeliveryMethod.ReliableUnordered);
            }
        }

        // Callback for when the mission started; let the server know which scene you are at
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

        // patch for the agent spawn; deconflict network spawn from local spawn
        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        public class CampaignAgentSpawnedPatch
        {


            public static void Postfix(AgentBuildData agentBuildData, bool spawnFromAgentVisuals, int formationTroopCount, ref Agent __result)
            {
                // if the player isn't in a team, continue
                if (Mission.Current == null || Mission.Current.PlayerTeam == null) return;
                try
                {
                    // if the player isn't in our team, we don't care, spawn them as usual
                    if (__result.Team != Mission.Current.PlayerTeam)
                    {
                        return;
                    }
                }
                catch { }
                // if they are in our team, pass them to the server to generate an ID for them and return them back to us.
                NetDataWriter writer = new NetDataWriter();
                writer.Put((uint)MessageType.AddAgent);
                writer.Put(__result.Index);
                client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                InformationManager.DisplayMessage(new InformationMessage("Created Agent: " + __result.Name + " which is under my command? " + (__result.Team == Mission.Current.PlayerTeam)));

            }
        }

        // exiting a mission, clear all network related memory and let the server know
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


        // Spawn an agent based on its character object and frame. For now, Main agent character object is used
        // This should be the real character object in the future
        private Agent SpawnAgent(CharacterObject character, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData = agentBuildData.Team(Mission.Current.PlayerAllyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);
            agent.FadeIn();
            agent.Controller = Agent.ControllerType.None;
            return agent;
        }

        // DEBUG METHOD: To spawn in Arena and test fights
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

        // DEBUG METHOD: Starts an Arena fight
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

        // Update Player tick from the game thread
        private void UpdatePlayerTick(PlayerTickInfo info, Agent agent)
        {
            if (Mission.Current != null && agent != null && Mission.Current.IsLoadingFinished)
            {


                // if the player is dead, dont sync anything
                if (agent.Health <= 0)
                {
                    return;
                }

                // get the position
                Vec3 pos = new Vec3(info.PosX, info.PosY, info.PosZ);

                // if the distance between the local agent and the info passed from the server is greater than 1 unit, teleport the agent
                if (agent.GetPathDistanceToPoint(ref pos) > 1f)
                {
                    agent.TeleportToPosition(pos);
                }


                // Set the agent's flags to 0 ie nothing
                agent.EventControlFlags = 0U;

                // if the agent is crouching, add that event
                if (info.crouchMode)
                {

                    agent.EventControlFlags |= Agent.EventControlFlag.Crouch;
                }
                else
                {
                    agent.EventControlFlags |= Agent.EventControlFlag.Stand;
                }

                // apply the agent's look direction
                agent.LookDirection = new Vec3(info.LookDirectionX, info.LookDirectionY, info.LookDirectionZ);

                // apply the agent's movement input vector...Is this necessary?
                agent.MovementInputVector = new Vec2(info.InputVectorX, info.InputVectorY);

                // Now check the flags given to us from the server
                uint eventFlag = info.EventFlag;
                if (eventFlag == 1u)
                {
                    // dismount
                    agent.EventControlFlags |= Agent.EventControlFlag.Dismount;
                }
                if (eventFlag == 2u)
                {
                    // mount
                    agent.EventControlFlags |= Agent.EventControlFlag.Mount;
                }
                if (eventFlag == 0x400u)
                {
                    // switch weapon
                    agent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
                }




                // apply the animation on channel 0 if none exists
                if (agent.GetCurrentAction(0) == ActionIndexCache.act_none || agent.GetCurrentAction(0).Index != info.Action0Index)
                {
                    string actionName1 = MBAnimation.GetActionNameWithCode(info.Action0Index);
                    agent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (ulong)info.Action0Flag, startProgress: info.Action0Progress);

                }
                // otherwise continue the existing animation
                else
                {
                    agent.SetCurrentActionProgress(0, info.Action0Progress);
                }

                // Set the movement flags to none
                agent.MovementFlags = 0U;

                // Check the action of the agent; if they are defending, apply the defending movement flag
                if ((int)info.Action1CodeType >= (int)Agent.ActionCodeType.DefendAllBegin && (int)info.Action1CodeType <= (int)Agent.ActionCodeType.DefendAllEnd)

                {
                    agent.MovementFlags = (Agent.MovementControlFlag)info.MovementFlag;
                    return;
                }

                // Check if there is a change on the right hand
                if ((EquipmentIndex)info.MainHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.MainHand))
                {
                    // set the weapon to whatever index the server passed
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, (EquipmentIndex)info.MainHandIndex, false, false, agent.WieldedWeapon.CurrentUsageIndex);
                }
                // check if there is a change on the left hand

                if ((EquipmentIndex)info.OffHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.OffHand))
                {
                    // set the index to the weapon wielded
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, (EquipmentIndex)info.OffHandIndex, false, false, agent.WieldedOffhandWeapon.CurrentUsageIndex);
                }


                // Check if there is a melee; this breaks the game if we don't do it.
                if ((Agent.ActionCodeType)info.Action1CodeType != Agent.ActionCodeType.BlockedMelee)
                {
                    // if the animation is none, start it
                    if (agent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.GetCurrentAction(1).Index != info.Action1Index)
                    {
                        string actionName2 = MBAnimation.GetActionNameWithCode(info.Action1Index);
                        agent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (ulong)info.Action1Flag, startProgress: info.Action1Progress);

                    }
                    // otherwise continue it
                    else
                    {
                        agent.SetCurrentActionProgress(1, info.Action1Progress);
                    }
                }
                else
                {
                    // otherwise just cancel it
                    agent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true, startProgress: 100);
                }

                // repeat this process for the mount
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


        public ConcurrentDictionary<int, ConcurrentDictionary<string, Agent>> GetPlayerSyncDict()
        {
            return playerTickInfo;
        }


        public override void OnMissionTick(float dt)
        {
            // if the mission is null or not yet loaded, skip it; This check is not necessary anymore
            if (Mission.Current == null || !Mission.Current.IsLoadingFinished || Mission.Current.MainAgent == null || Mission.Current.IsMissionEnding)
            {
                return;
            }


            // Apply all the pending tasks that have been queued by the network server
            MissionTaskManager.Instance().ApplyPendingTasks();

            // Loop through all the agent updates that have been queued by the server
            foreach ((PlayerTickInfo, Agent) agentUpdate in agentUpdateState.Values)
            {
                if (agentUpdate.Item1 != null && agentUpdate.Item2 != null)
                {
                    // update the game state from this information
                    UpdatePlayerTick(agentUpdate.Item1, agentUpdate.Item2);
                }

            }

            // this is DEBUG
            if (Input.IsKeyReleased(InputKey.Numpad6))
            {
                foreach(string clientId in agentUpdateState.Keys)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Agent Update state has: " + clientId));
                   
                }
            }

            // loop through all the local agents from the client
            foreach (NetworkAgent agent in ClientAgentManager.Instance().GetHostNetworkAgents())
            {
                // if no MB agent has been created yet, skip this update
                if (agent.Agent == null)
                {
                    return;
                }
                // otherwise grab it
                Agent mbAgent = agent.Agent;
                
                // Everything below retrieves the needed data from MBAgent to generate a PlayerTickInfo
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
                    // if  key exists for this agent, retrieve ot
                    if (hostPlayerTickInfo.ContainsKey(mbAgent.Index))
                    {
                        tickInfo = hostPlayerTickInfo[mbAgent.Index];
                    }
                    // otherwise create one
                    else
                    {
                        tickInfo = new PlayerTickInfo();
                        hostPlayerTickInfo[mbAgent.Index] = tickInfo;
                    }

                    // do the same for the mount
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
