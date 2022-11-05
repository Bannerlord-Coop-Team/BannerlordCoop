using Common;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class PartyBaseSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        PartyBase partyBase;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        public PartyBaseSerializer(PartyBase partyBase) : base(partyBase)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                object value = fieldInfo.GetValue(partyBase);

                if (value == null)
                {
                    continue;
                }

                switch (fieldInfo.Name)
                {
                    case "_visual":
                        // PartyVisual
                        // Generate on server
                        break;
                    case "<MemberRoster>k__BackingField":
                        // TroopRoster
                        SNNSO.Add(fieldInfo, new TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<PrisonRoster>k__BackingField":
                        // TroopRoster
                        SNNSO.Add(fieldInfo, new TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<ItemRoster>k__BackingField":
                        // ItemRoster
                        SNNSO.Add(fieldInfo, new ItemRosterSerializer((ItemRoster)value));
                        break;
                    case "Random":
                        // DeterministicRandom
                        SNNSO.Add(fieldInfo, new DeterministicRandomSerializer((DeterministicRandom)value));
                        break;
                    case "_lastEatingTime":
                        //CampaignTime
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    // References
                    case "<MobileParty>k__BackingField":
                    case "<Settlement>k__BackingField":
                    case "_leader":
                    case "_customOwner":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;
                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }

            FieldInfo indexField = partyBase.GetType().GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic);
            SerializableObjects.Remove(indexField);
        }

        public override object Deserialize()
        {
            partyBase = new PartyBase((MobileParty)null);

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(partyBase, entry.Value.Deserialize());
            }

            IPartyVisual newVisual = Campaign.Current.VisualCreator.PartyVisualCreator.CreatePartyVisual();
            partyBase.GetType().GetField("_visual", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(partyBase, newVisual);

            return base.Deserialize(partyBase);
        }

        public override void ResolveReferenceGuids()
        {
            if (partyBase == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                object obj = CoopObjectManager.GetObject(id);

                field.SetValue(partyBase, obj);
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Value.ResolveReferenceGuids();
            }
        }
    }
}