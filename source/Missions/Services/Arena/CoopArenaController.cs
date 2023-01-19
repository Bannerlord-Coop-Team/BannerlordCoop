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
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.Extensions;
using System.Runtime.CompilerServices;

namespace Missions.Services
{
    internal class CoopArenaController : MissionBehavior
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private static readonly IDictionary<ItemObject.ItemTypeEnum, List<ItemObject>> itemDict = InitializeItemDictionary();
        private static readonly ItemObject.ItemTypeEnum[] weaponLoadout = GenerateWeaponLoadout();
        private static readonly ItemObject.ItemTypeEnum[] armorLoadout = new ItemObject.ItemTypeEnum[5] { ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.Cape, ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.LegArmor };
        private static readonly ItemObject.ItemTypeEnum[] horseLoadout = new ItemObject.ItemTypeEnum[2] { ItemObject.ItemTypeEnum.Horse, ItemObject.ItemTypeEnum.HorseHarness };

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
                .Equipment(CreateRandomEquipment(false))
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
            agentBuildData.Equipment(CreateRandomEquipment(false));
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

        /// <summary>
        /// Creates random equipment for characters
        /// </summary>
        /// <param name="hasHorse">Boolean for whether character has a horse</param>
        /// <returns>Equipment object for the character</returns>
        private static Equipment CreateRandomEquipment(bool hasHorse)
        {
            Equipment equipment = new Equipment();
            List<EquipmentElement> randomWeaponLoadout = SelectRandomItemsForLoadout(weaponLoadout, itemDict);
            List<EquipmentElement> randomArmorLoadout = SelectRandomItemsForLoadout(armorLoadout, itemDict);
            AddWeaponsToEquipment(randomWeaponLoadout, equipment);
            AddArmorToEquipment(randomArmorLoadout, equipment);

            
            if (hasHorse)
            {
                List<EquipmentElement> randomHorseLoadout = SelectRandomItemsForLoadout(horseLoadout, itemDict);
                AddHorseToEquipment(randomHorseLoadout, equipment);
            }

            return equipment;
        }
        /// <summary>
        /// Creates dictionary containing all items in game and categorized by itemType
        /// </summary>
        /// <returns>A dictionary</returns>
        private static IDictionary<ItemObject.ItemTypeEnum, List<ItemObject>> InitializeItemDictionary()
        {
            IEnumerable<ItemObject> allItems = Game.Current.ObjectManager.GetObjectTypeList<ItemObject>();
            IDictionary<ItemObject.ItemTypeEnum, List<ItemObject>> result = new Dictionary<ItemObject.ItemTypeEnum, List<ItemObject>>();

            foreach (var item in allItems)
            {
                bool keyExists = result.ContainsKey(item.ItemType);
                if (!keyExists)
                {
                    result.Add(item.ItemType, new List<ItemObject>());
                }
                result[item.ItemType].Add(item);
            }

            return result;

        }
        /// <summary>
        /// Creates a loadout for weapons
        /// </summary>
        /// <returns>An array containing all the ItemTypes of the weapons in the loadout</returns>
        private static ItemObject.ItemTypeEnum[] GenerateWeaponLoadout()
        {
            var random = new Random();
            ItemObject.ItemTypeEnum[][] weaponLoadouts = new ItemObject.ItemTypeEnum[5][];
            weaponLoadouts[0] = new ItemObject.ItemTypeEnum[] {ItemObject.ItemTypeEnum.TwoHandedWeapon};
            weaponLoadouts[1] = new ItemObject.ItemTypeEnum[] { ItemObject.ItemTypeEnum.Polearm};
            weaponLoadouts[2] = new ItemObject.ItemTypeEnum[] { ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.Thrown };
            weaponLoadouts[3] = new ItemObject.ItemTypeEnum[] { ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Thrown };
            weaponLoadouts[4] = new ItemObject.ItemTypeEnum[] { ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.Shield};

            return weaponLoadouts[random.Next(weaponLoadouts.Count())];
        }

        /// <summary>
        /// Selects a random item according to the loadout
        /// </summary>
        /// <param name="loadout">The loadout array that contains all the ItemTypes to identify which ItemType to randomize</param>
        /// <param name="itemDict">A dictionary of all the items in the game categorized into ItemType</param>
        /// <returns>List of EquipmentElements to be equipped onto character</returns>
        private static List<EquipmentElement> SelectRandomItemsForLoadout(ItemObject.ItemTypeEnum[] loadout, IDictionary<ItemObject.ItemTypeEnum, List<ItemObject>> itemDict)
        {
            List<EquipmentElement> result = new List<EquipmentElement>();
            var random = new Random();
            foreach (ItemObject.ItemTypeEnum loadoutItem in loadout)
            {
                int randomItemIndex = random.Next(itemDict[loadoutItem].Count);
                EquipmentElement randomItem = new EquipmentElement(itemDict[loadoutItem][randomItemIndex]);
                result.Add(randomItem);
            }
            return result;
        }
        /// <summary>
        /// Adds weapons to the equipment
        /// </summary>
        /// <param name="weaponLoadout">List of EquipmentElement weapons to add to the character</param>
        /// <param name="equipment">The equipment object</param>
        private static void AddWeaponsToEquipment(List<EquipmentElement> weaponLoadout, Equipment equipment)
        {

            for (int i = 0; i < weaponLoadout.Count; ++i)
            {
                equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, weaponLoadout[i]);
            }
        }

        /// <summary>
        /// Adds armor to the equipment
        /// </summary>
        /// <param name="armorLoadout">List of EquipmentElement armor to add to the character</param>
        /// <param name="equipment">The equipment object</param>
        private static void AddArmorToEquipment(List<EquipmentElement> armorLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, armorLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, armorLoadout[1]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, armorLoadout[2]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, armorLoadout[3]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, armorLoadout[4]);
        }

        /// <summary>
        /// Adds horse and horse harness to the equipment
        /// </summary>
        /// <param name="horseLoadout">List of EquipmentElement horse and horse harness to add to the character</param>
        /// <param name="equipment">The equipment object</param>
        private static void AddHorseToEquipment(List<EquipmentElement> horseLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, horseLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, horseLoadout[1]);
            
        }
    }
}