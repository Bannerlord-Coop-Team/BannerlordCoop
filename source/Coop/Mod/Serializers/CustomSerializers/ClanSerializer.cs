using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        Hero _leader;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
       
        public ClanSerializer(Clan clan) : base(clan)
        {
            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(clan);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
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
                        // Assigned by SetHeroReference on deserialization
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
                    case "_defaultPartyTemplate":
                        SNNSO.Add(fieldInfo, new DefaultPartyTemplateSerializer((PartyTemplateObject)value));
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        /// <summary>
        /// For assigning PlayerHeroSerializer reference for deserialization
        /// </summary>
        /// <param name="leader">PlayerHeroSerializer used by _leader</param>
        public void SetHeroReference(Hero leader)
        {
            _leader = leader;
        }

        public override object Deserialize()
        {

            Clan newClan = MBObjectManager.Instance.CreateObject<Clan>();

            // Circular referenced object needs assignment before deserialize
            if (_leader == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            // Circular referenced objects
            newClan.GetType().GetField("_leader", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(newClan, _leader);

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newClan, entry.Value.Deserialize());
            }
            
            return base.Deserialize(newClan);
        }
    }
}
