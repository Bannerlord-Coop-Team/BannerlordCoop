using Common;
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

        public Guid PlayerHeroId { get; set; }

        public bool RequiresCharacterCreation => !Coop.IsServer;

        //CharacterObjectSerializer[] characterObjects;
        //ClanSerializer[] clans;

        // TODO 
        //KingdomSerializer[] kingdoms;
        public SettlementSerializer[] settlements;
        public TownSerializer[] towns;
        public VillageSerializer[] villages;
        public HeroSerializer[] heros;
        public MobilePartySerializer[] parties;
        public ClanSerializer[] clans;
        public KingdomSerializer[] kingdoms;

        Dictionary<Guid, string> SettlementIds;

        public GameData()
        {
            //.Where(hero => hero != Hero.MainHero)
            heros = CoopObjectManager.GetObjects<Hero>().Select(hero => new HeroSerializer(hero)).ToArray();
            parties = CoopObjectManager.GetObjects<MobileParty>().Select(party => new MobilePartySerializer(party)).ToArray();
            settlements = CoopObjectManager.GetObjects<Settlement>().Select(settlement => new SettlementSerializer(settlement)).ToArray();
            towns = CoopObjectManager.GetObjects<Town>().Select(town => new TownSerializer(town)).ToArray();
            villages = CoopObjectManager.GetObjects<Village>().Select(village => new VillageSerializer(village)).ToArray();
            clans = CoopObjectManager.GetObjects<Clan>().Select(clan => new ClanSerializer(clan)).ToArray();
            kingdoms = CoopObjectManager.GetObjects<Kingdom>().Select(kingdom => new KingdomSerializer(kingdom)).ToArray();

            SettlementIds = CoopObjectManager.GetTypeGuids<Settlement>()
                .ToDictionary(
                    guid => guid, 
                    guid => CoopObjectManager.GetObject<Settlement>(guid).Name.ToString());
        }

        public void Unpack()
        {
            foreach (var settlement in settlements)
            {
                settlement.Deserialize();
            }

            foreach (var town in towns)
            {
                town.Deserialize();
            }

            foreach (var village in villages)
            {
                village.Deserialize();
            }

            // Deserialize everything
            foreach (var hero in heros)
            {
                hero.Deserialize();
            }

            foreach (var clan in clans)
            {
                clan.Deserialize();
            }

            foreach (var party in parties)
            {
                party.Deserialize();
            }

            foreach (var kingdom in kingdoms)
            {
                kingdom.Deserialize();
            }

            ValidateIds();

            // Resolve all references
            foreach (var hero in heros)
            {
                hero.ResolveReferenceGuids();
            }

            foreach (var settlement in settlements)
            {
                settlement.ResolveReferenceGuids();
            }

            foreach (var town in towns)
            {
                town.ResolveReferenceGuids();
            }

            foreach (var village in villages)
            {
                village.ResolveReferenceGuids();
            }

            foreach (var clan in clans)
            {
                clan.ResolveReferenceGuids();
            }

            foreach (var party in parties)
            {
                party.ResolveReferenceGuids();
            }

            foreach (var kingdom in kingdoms)
            {
                kingdom.ResolveReferenceGuids();
            }
        }

        private void ValidateIds()
        {
            Dictionary<Guid, string> unknownIds = new Dictionary<Guid, string>();
            foreach (var settlementId in SettlementIds)
            {
                try
                {
                    CoopObjectManager.GetObject(settlementId.Key);
                }
                catch (Exception)
                {
                    unknownIds.Add(settlementId.Key, settlementId.Value);
                }
            }

            if(unknownIds.Count > 0)
            {
                throw new Exception($"Cannot find Ids, {unknownIds}");
            }
        }
    }
}
