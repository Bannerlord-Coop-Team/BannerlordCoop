using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class VillageSerializer : ICustomSerializer
    {
        Guid culture;
        public VillageSerializer(Village village) : base(village)
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(village);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "<Culture>k__BackingField":
                        culture = CoopObjectManager.GetGuid((CultureObject)value);
                        break;
                    case "<ClaimedBy>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_nextLocatable":
                        foreach (Guid supporters in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            Supporters.Add(supporters);
                        }
                        break;
                    case "_settlementComponents":
                        foreach (Guid companions in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            Companions.Add(companions);
                        }
                        break;
                    case "_boundVillages":
                        foreach (Guid commanderheroes in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            CommanderHeroes.Add(commanderheroes);
                        }
                        break;
                    case "_name":
                        basictroop = CoopObjectManager.GetGuid(value);
                        break;
                    case "_lastAttackerParty":
                        // Assigned by SetHeroReference on deserialization
                        leader = CoopObjectManager.GetGuid(value);
                        break;
                    case "_siegeEngineMissiles":
                        SNNSO.Add(fieldInfo, new BannerSerializer((Banner)value));
                        break;
                    case "<Town>k_BackingField":
                        home = CoopObjectManager.GetGuid(value);
                        break;
                    case "<Village>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<Hideout>k_BackingField":
                        SNNSO.Add(fieldInfo, new DefaultPartyTemplateSerializer((PartyTemplateObject)value));
                        break;
                    case "<SiegeLanes>k_BackingField":
                        kingdom = CoopObjectManager.GetGuid(value);
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();

        }

        public object Deserialize()
        {
            if(villageId != null)
            {
                return Settlement.Find(villageId);
            }

            return null;
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}