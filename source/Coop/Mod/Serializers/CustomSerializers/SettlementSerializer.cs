using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class SettlementSerializer : ICustomSerializer
    {
        private string settlementId;
        public SettlementSerializer(Settlement settlement)
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

                /// <summary>
                /// Serialized Natively Non Serializable Objects (SNNSO)
                /// </summary>
                Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "<Culture>k__BackingField":
                        culture = CoopObjectManager.GetGuid((CultureObject)value);
                        break;
                    case "<ClaimedBy>k__BackingField":

                        break;
                    case "_nextLocatable":

                        break;
                    case "_settlementComponents":

                        break;
                    case "_boundVillages":

                        break;
                    case "_name":

                        break;
                    case "_lastAttackerParty":
                        // Assigned by SetHeroReference on deserialization

                        break;
                    case "_siegeEngineMissiles":

                        break;
                    case "<Town>k_BackingField":

                        break;
                    case "<Village>k__BackingField":

                        break;
                    case "<Hideout>k_BackingField":

                        break;
                    case "<SiegeLanes>k_BackingField":

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
            if(settlementId != null)
            {
                return Settlement.Find(settlementId);
            }

            return null;
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}