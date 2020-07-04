using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class ClanSerializer : CustomSerializer
    {
        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        PlayerHeroSerializer _leader;
        public ClanSerializer(Clan clan, PlayerHeroSerializer leader) : base(clan)
        {
            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                object value = fieldInfo.GetValue(clan);

                if (value == null)
                {
                    continue;
                }

                switch (fieldInfo.Name)
                {
                    case "<Culture>k__BackingField":
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)value));
                        break;
                    case "<LastFactionChangeTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<SupporterNotables>k__BackingField":
                        foreach (Hero hero in (MBReadOnlyList<Hero>)value)
                        {
                            throw new Exception("Should be no Supporters");
                        }
                        break;
                    case "<Companions>k__BackingField":
                        foreach (Hero hero in (MBReadOnlyList<Hero>)value)
                        {
                            throw new Exception("Should be no compainions");
                        }
                        break;
                    case "<CommanderHeroes>k__BackingField":
                        foreach (Hero hero in (MBReadOnlyList<Hero>)value)
                        {
                            throw new Exception("Should be no Commanders");
                        }
                        break;
                    case "_basicTroop":
                        SNNSO.Add(fieldInfo, new BasicTroopSerializer((CharacterObject)value));
                        break;
                    case "_leader":
                        _leader = leader;
                        break;
                    case "_banner":
                        SNNSO.Add(fieldInfo, new BannerSerializer((Banner)value));
                        break;
                    case "_home":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<NotAttackableByPlayerUntilTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }
        }

        public override object Deserialize()
        {
            Clan newClan = new Clan();

            return base.Deserialize(newClan);
        }
    }
}
