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
        SettlementSerializer[] settlements;
        TownSerializer[] towns;
        VillageSerializer[] villages;
        HeroSerializer[] heros;
        MobilePartySerializer[] parties;
        ClanSerializer[] clans;

        Dictionary<Guid, string> SettlementIds;

        public GameData()
        {
            heros = CoopObjectManager.GetObjects<Hero>().Where(hero => hero != Hero.MainHero).Select(hero => new HeroSerializer(hero)).ToArray();
            parties = CoopObjectManager.GetObjects<MobileParty>().Select(party => new MobilePartySerializer(party)).ToArray();
            settlements = CoopObjectManager.GetObjects<Settlement>().Select(settlement => new SettlementSerializer(settlement)).ToArray();
            towns = CoopObjectManager.GetObjects<Town>().Select(town => new TownSerializer(town)).ToArray();
            villages = CoopObjectManager.GetObjects<Village>().Select(village => new VillageSerializer(village)).ToArray();
            clans = CoopObjectManager.GetObjects<Clan>().Select(clan => new ClanSerializer(clan)).ToArray();

            SettlementIds = CoopObjectManager.GetTypeGuids<Settlement>()
                .ToDictionary(
                    guid => guid, 
                    guid => CoopObjectManager.GetObject<Settlement>(guid).Name.ToString());
        }

        public void Unpack()
        {
            // Deserialize everything
            foreach (var hero in heros)
            {
                hero.Deserialize();
            }

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

            foreach (var clan in clans)
            {
                clan.Deserialize();
            }

            foreach (var party in parties)
            {
                party.Deserialize();
            }

            ValidateIds();

            // Resolve all references
            foreach (var hero in heros)
            {
                hero.ResolveReferenceGuids();
            }

            foreach (var party in parties)
            {
                party.ResolveReferenceGuids();
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
