using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Common.Messaging;
using Common;
using Missions.Messages;
using LiteNetLib;
using Serilog;
using Common.Logging;
using Missions.Services.Network;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Encounters;
using System.Text.RegularExpressions;

namespace Missions.Services
{
    internal class CoopArenaController : MissionBehavior
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        public CoopArenaController(IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry)
        {
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;

            messageBroker.Subscribe<MissionJoinInfo>(Handle_JoinInfo);
        }

        ~CoopArenaController()
        {
            _messageBroker.Unsubscribe<MissionJoinInfo>(Handle_JoinInfo);
        }

        public override void AfterStart()
        {
            AddPlayerToArena();
        }

        private void Handle_JoinInfo(MessagePayload<MissionJoinInfo> payload)
        {
            Logger.Debug("Received join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            MissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                joinInfo.CharacterObject.IsPlayerCharacter ? "Player" : "Agent",
                joinInfo.CharacterObject.Name, newAgentId, netPeer.EndPoint);

            Agent newAgent = SpawnAgent(startingPos, joinInfo.CharacterObject);
            _agentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
        }

        public Agent AddPlayerToArena()
        {
            Mission.Current.PlayerTeam = Mission.Current.AttackerTeam;

            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }

            // get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();


            // spawn an instance of the player (controlled by default)
            return SpawnPlayerAgent(CharacterObject.PlayerCharacter, randomElement);
        }

        // Spawn an agent based on its character object and frame. For now, Main agent character object is used
        // This should be the real character object in the future
        private Agent SpawnPlayerAgent(CharacterObject character, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData = agentBuildData.Team(Mission.Current.PlayerAllyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec)
                .NoHorses(true)
                .Equipment(CreateRandomEquipment())
                .TroopOrigin(new SimpleAgentOrigin(character, -1, null, default)), false, 0);
            agent.FadeIn();
            agent.Controller = Agent.ControllerType.Player;
            return agent;
        }

        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(startingPos);
            agentBuildData.Team(Mission.Current.PlayerAllyTeam);
            agentBuildData.InitialDirection(Vec2.Forward);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(CreateRandomEquipment());
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
            agentBuildData.Controller(Agent.ControllerType.None);

            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            });

            return agent;
        }

        // DEBUG METHOD: Starts an Arena fight
        public void StartArenaFight()
        {
            // reset teams if any exists
            Mission.Current.ResetMission();

            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            // players is defender team
            Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


            // find areas of spawn
            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }
            // get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();


            // spawn an instance of the player (controlled by default)
            SpawnPlayerAgent(CharacterObject.PlayerCharacter, randomElement);
        }

        public Equipment CreateRandomEquipment()
        {
            Equipment equipment = new Equipment();
            // Tried the MBRandom but it didn't work
            var random = new Random();
            //List of all items in the game
            List<ItemObject> allItems = Game.Current.ObjectManager.GetObjectTypeList<ItemObject>().ToList();
            //List of each equippable item type
            List<ItemObject> CapeItems = new List<ItemObject>();
            List<ItemObject> BodyArmorItems = new List<ItemObject>();
            List<ItemObject> HeadArmorItems = new List<ItemObject>();
            List<ItemObject> LegArmorItems = new List<ItemObject>();
            List<ItemObject> HandArmorItems = new List<ItemObject>();
            List<ItemObject> OneHandedWeaponItems = new List<ItemObject>();
            List<ItemObject> TwoHandedWeaponItems = new List<ItemObject>();
            List<ItemObject> PolearmWeaponItems = new List<ItemObject>();
            List<ItemObject> ThrownItems = new List<ItemObject>();
            List<ItemObject> BowItems = new List<ItemObject>();
            List<ItemObject> ArrowsItems = new List<ItemObject>();
            List<ItemObject> CrossbowItems = new List<ItemObject>();
            List<ItemObject> BoltItems = new List<ItemObject>();
            List<ItemObject> ShieldItems = new List<ItemObject>();
            //creates list of "loadouts"
            List<Equipment> ArmorLoadoutList = new List<Equipment>();

            //Categorize Weapons and Clothes into item types
            for (int i = 0; i <= allItems.Count - 1; i++)
            {
                switch (allItems[i].ItemType.ToString())
                {
                    case "Cape":
                        CapeItems.Add(allItems[i]);
                        break;
                    case "HeadArmor":
                        HeadArmorItems.Add(allItems[i]);
                        break;
                    case "BodyArmor":
                        BodyArmorItems.Add(allItems[i]);
                        break;
                    case "LegArmor":
                        LegArmorItems.Add(allItems[i]);
                        break;
                    case "HandArmor":
                        HandArmorItems.Add(allItems[i]);
                        break;
                    case "OneHandedWeapon":
                        OneHandedWeaponItems.Add(allItems[i]);
                        break;
                    case "TwoHandedWeapon":
                        TwoHandedWeaponItems.Add(allItems[i]);
                        break;
                    case "Polearm":
                        PolearmWeaponItems.Add(allItems[i]);
                        break;
                    case "Thrown":
                        ThrownItems.Add(allItems[i]);
                        break;
                    case "Bow":
                        BowItems.Add(allItems[i]);
                        break;
                    case "Arrows":
                        ArrowsItems.Add(allItems[i]);
                        break;
                    case "Crossbow":
                        CrossbowItems.Add(allItems[i]);
                        break;
                    case "Bolts":
                        BoltItems.Add(allItems[i]);
                        break;
                    case "Shield":
                        ShieldItems.Add(allItems[i]);
                        break;
                }
            }

            //Randomization of each type of weapon
            EquipmentElement OneHandedWeapon = new EquipmentElement(OneHandedWeaponItems[random.Next(0, OneHandedWeaponItems.Count - 1)]);
            EquipmentElement TwoHandedWeapon = new EquipmentElement(TwoHandedWeaponItems[random.Next(0, TwoHandedWeaponItems.Count - 1)]);
            EquipmentElement PolearmWeapon = new EquipmentElement(PolearmWeaponItems[random.Next(0, PolearmWeaponItems.Count - 1)]);
            EquipmentElement ThrownWeapon = new EquipmentElement(ThrownItems[random.Next(0, ThrownItems.Count - 1)]);
            EquipmentElement BowWeapon = new EquipmentElement(BowItems[random.Next(0, BowItems.Count - 1)]);
            EquipmentElement Arrows = new EquipmentElement(ArrowsItems[random.Next(0, ArrowsItems.Count - 1)]);
            EquipmentElement Crossbow = new EquipmentElement(CrossbowItems[random.Next(0, CrossbowItems.Count - 1)]);
            EquipmentElement Bolts = new EquipmentElement(BoltItems[random.Next(0, BoltItems.Count - 1)]);
            EquipmentElement Shield = new EquipmentElement(ShieldItems[random.Next(0, ShieldItems.Count - 1)]);

            //Loadout Weapons
            switch (random.Next(0, 5))
            {
                case 0:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, OneHandedWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, Shield);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, BowWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)3, Arrows);
                    break;
                case 1:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, PolearmWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, Shield);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, BowWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)3, Arrows);
                    break;
                case 2:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, TwoHandedWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, Shield);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, Crossbow);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)3, Bolts);
                    break;
                case 3:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, TwoHandedWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, ThrownWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, OneHandedWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)3, Shield);
                    break;
                case 4:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, BowWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, Arrows);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, TwoHandedWeapon);
                    break;
                case 5:
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)0, PolearmWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)1, Shield);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)2, OneHandedWeapon);
                    equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)3, ThrownWeapon);
                    break;

            }



            Console.WriteLine(equipment);
            //Console.WriteLine(test);
            Console.WriteLine(", ", BodyArmorItems);
            Console.WriteLine(", ", LegArmorItems);
            //Console.WriteLine(test);

            //Randomization of armor/clothing
            EquipmentElement HeadArmor = new EquipmentElement(HeadArmorItems[random.Next(0, HeadArmorItems.Count - 1)]);
            EquipmentElement BodyArmor = new EquipmentElement(BodyArmorItems[random.Next(0, BodyArmorItems.Count - 1)]);
            EquipmentElement LegArmor = new EquipmentElement(LegArmorItems[random.Next(0, LegArmorItems.Count - 1)]);
            EquipmentElement HandArmor = new EquipmentElement(HandArmorItems[random.Next(0, HandArmorItems.Count - 1)]);
            EquipmentElement CapeArmor = new EquipmentElement(CapeItems[random.Next(0, CapeItems.Count - 1)]);

            equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)5, HeadArmor);
            equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)6, BodyArmor);
            equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)7, LegArmor);
            equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)8, HandArmor);
            equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)9, CapeArmor);

            return equipment;
        }
    }
}