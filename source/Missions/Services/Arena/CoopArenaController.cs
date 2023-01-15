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
        
        //creates random equipment for characters
        public Equipment CreateRandomEquipment(bool hasHorse)
        {
            Equipment equipment = new Equipment();
            IDictionary<string, List<ItemObject>> itemDict = InitializeItemDictionary();
            List<string> weaponLoadout = GenerateWeaponLoadout();
            List<string> armorLoadout = new List<string>() { "HeadArmor", "BodyArmor", "LegArmor", "HandArmor", "Cape" };
            List<EquipmentElement> randomWeaponLoadout = SelectRandomItemsForLoadout(weaponLoadout, itemDict);
            List<EquipmentElement> randomArmorLoadout = SelectRandomItemsForLoadout(armorLoadout, itemDict);
            AddWeaponsToEquipment(randomWeaponLoadout, equipment);
            AddArmorToEquipment(randomArmorLoadout, equipment);

            if (hasHorse)
            {
                List<string> horseLoadout = new List<string>() { "Horse", "HorseHarness" };
                List<EquipmentElement> randomHorseLoadout = SelectRandomItemsForLoadout(horseLoadout, itemDict);
                AddHorseToEquipment(randomHorseLoadout, equipment);
            }
       
            return equipment;
        }

        //creates dictionary containing all items in game and categorized by itemType
        public IDictionary<string, List<ItemObject>> InitializeItemDictionary()
        {
            List<ItemObject> AllItems = Game.Current.ObjectManager.GetObjectTypeList<ItemObject>().ToList();
            IDictionary<string, List<ItemObject>> result = new Dictionary<string, List<ItemObject>>();

            for (int i = 0; i <= AllItems.Count - 1; i++)
            {
                bool KeyExists = result.ContainsKey(AllItems[i].ItemType.ToString());
                if (!KeyExists)
                {
                    result.Add(AllItems[i].ItemType.ToString(), new List<ItemObject>());
                }
                result[AllItems[i].ItemType.ToString()].Add(AllItems[i]);

            }

            return result;

        }

        //selects a random item according to the loadout
        public List<EquipmentElement> SelectRandomItemsForLoadout(List<string> loadout, IDictionary<string, List<ItemObject>> itemDict)
        {
            List<EquipmentElement> result = new List<EquipmentElement>();
            var random = new Random();
            foreach (string loadoutItem in loadout)
            {
                int randomItemIndex = random.Next(0, itemDict[loadoutItem].Count - 1);
                EquipmentElement randomItem = new EquipmentElement(itemDict[loadoutItem][randomItemIndex]);
                result.Add(randomItem);
            }
            return result;
        }

        //creates a loadout for weapons
        public List<string> GenerateWeaponLoadout()
        {
            var random = new Random();
            List<List<string>> weaponLoadouts = new List<List<string>>();
            //you can add more loadouts by copying the line below and changing the string values in the brackets
            //weapon itemType include: OneHandedWeapon, TwoHandedWeapon, Polearm, Thrown, Bow, Arrows, Crossbow, Bolts, Shield
            weaponLoadouts.Add(new List<string> { "Bow", "Arrows", "Thrown"});
            weaponLoadouts.Add(new List<string> { "TwoHandedWeapon" });
            weaponLoadouts.Add(new List<string> { "Polearm" });
            weaponLoadouts.Add(new List<string> { "OneHandedWeapon", "Thrown" });
            weaponLoadouts.Add(new List<string> { "OneHandedWeapon", "Shield" });

            return weaponLoadouts[random.Next(0, weaponLoadouts.Count - 1)];
        }

        //casts itemObjects to EquipmentElement
        public void AddWeaponsToEquipment(List<EquipmentElement> weaponLoadout, Equipment equipment)
        {

            for (int i = 0; i < weaponLoadout.Count; ++i)
            {
                equipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, weaponLoadout[i]);
            }
        }

        //casts itemObjects to EquipmentElement
        public void AddArmorToEquipment(List<EquipmentElement> armorLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, armorLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, armorLoadout[1]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, armorLoadout[2]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, armorLoadout[3]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, armorLoadout[4]);
        }

        //casts itemObjects to EquipmentElement
        public void AddHorseToEquipment(List<EquipmentElement> horseLoadout, Equipment equipment)
        {
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, horseLoadout[0]);
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, horseLoadout[1]);
            
        }
    }
}