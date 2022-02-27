using Common;
using Coop.Mod.Serializers;
using Coop.Mod.Serializers.Custom;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Data
{
    [Serializable]
    public class GameData
    {
        [NonSerialized]
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // TODO remove static
        public static Guid SPlayerHeroId { get; set; }
        public Guid PlayerHeroId { get; set; }

        public bool RequiresCharacterCreation => !Coop.IsServer;

        List<CustomSerializerWithGuid[]> data = new List<CustomSerializerWithGuid[]>();

        Dictionary<Guid, string> ExpectedIds = new Dictionary<Guid, string>();

        public GameData()
        {
            //.Where(hero => hero != Hero.MainHero)
            data = new List<CustomSerializerWithGuid[]>
            {
                //CoopObjectManager.GetObjects<Hero>().Select(hero => new HeroSerializer(hero)).ToArray(),
                //CoopObjectManager.GetObjects<MobileParty>().Select(party => new MobilePartySerializer(party)).ToArray(),
                new CustomSerializerWithGuid[] { new HeroSerializer(CoopObjectManager.GetObject<Hero>(SPlayerHeroId))},
                new CustomSerializerWithGuid[] { new MobilePartySerializer(CoopObjectManager.GetObject<Hero>(SPlayerHeroId).PartyBelongedTo)},
                CoopObjectManager.GetObjects<Settlement>().Select(settlement => new SettlementSerializer(settlement)).ToArray(),
                CoopObjectManager.GetObjects<Town>().Select(town => new TownSerializer(town)).ToArray(),
                CoopObjectManager.GetObjects<Village>().Select(village => new VillageSerializer(village)).ToArray(),
                CoopObjectManager.GetObjects<Clan>().Select(clan => new ClanSerializer(clan)).ToArray(),
                CoopObjectManager.GetObjects<Kingdom>().Select(kingdom => new KingdomSerializer(kingdom)).ToArray(),
                CoopObjectManager.GetObjects<CharacterObject>().Select(characterObject => new CharacterObjectSerializer(characterObject)).ToArray()
            };

            AddExpected();
        }

        void AddExpected()
        {
            
            Dictionary<Guid, string>  expectedSettlements = CoopObjectManager.GetTypeGuids<Settlement>()
                .ToDictionary(
                    guid => guid,
                    guid => CoopObjectManager.GetObject<Settlement>(guid).Name.ToString());

            ExpectedIds.Concat(expectedSettlements);

            Dictionary<Guid, string> expectedCharacterObjects = CoopObjectManager.GetTypeGuids<CharacterObject>()
                .ToDictionary(
                    guid => guid,
                    guid => CoopObjectManager.GetObject<CharacterObject>(guid).Name.ToString());

            ExpectedIds.Concat(expectedSettlements);
        }

        public void Unpack()
        {
            // Deserialize everything
            foreach(CustomSerializerWithGuid[] serializers in data)
            {
                foreach(CustomSerializerWithGuid serializer in serializers)
                {
                    serializer.Deserialize();
                }
            }

            ValidateIds();

            // Resolve all references
            foreach (CustomSerializerWithGuid[] serializers in data)
            {
                foreach (CustomSerializerWithGuid serializer in serializers)
                {
                    serializer.ResolveReferenceGuids();
                }
            }
        }

        private void ValidateIds()
        {
            Dictionary<Guid, string> unknownIds = new Dictionary<Guid, string>();
            foreach (var expectedId in ExpectedIds)
            {
                if(CoopObjectManager.GetObject(expectedId.Key) == null)
                {
                    unknownIds.Add(expectedId.Key, expectedId.Value);
                }
            }

            if(unknownIds.Count > 0)
            {
                throw new Exception($"Cannot find Ids, {unknownIds}");
            }
        }
    }
}
