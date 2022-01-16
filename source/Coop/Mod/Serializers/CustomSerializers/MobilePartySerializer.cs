using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class MobilePartySerializer : CustomSerializerWithGuid
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        public MobileParty mobileParty;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        List<Guid> attachedParties = new List<Guid>();

        public MobilePartySerializer(MobileParty mobileParty) : base(mobileParty)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(mobileParty);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "<Id>k__BackingField":
                        // Ignore current MB id
                        break;
                    case "<Ai>k__BackingField":
                        // PartyAi
                        // NOTE may not be needed due to player control
                        break;
                    case "<Party>k__BackingField":
                        SNNSO.Add(fieldInfo, new PartyBaseSerializer((PartyBase)value));
                        break;
                    case "_disorganizedUntilTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_ignoredUntilTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_initiativeRestoreTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_targetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_moveTargetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_aiPathLastFace":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<Path>k__BackingField":
                        SNNSO.Add(fieldInfo, new NavigationPathSerializer((NavigationPath)value));
                        break;
                    case "_partiesAroundPosition":
                        SNNSO.Add(fieldInfo, new MobilePartiesAroundPositionListSerializer((MobilePartiesAroundPositionList)value));
                        break;
                    case "_nextAiCheckTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<CurrentNavigationFace>k__BackingField":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<AttachedParties>k__BackingField":
                        MBReadOnlyList<MobileParty> attachedParties = (MBReadOnlyList<MobileParty>)value;
                        foreach(MobileParty attachedParty in attachedParties)
                        {
                            this.attachedParties.Add(CoopObjectManager.GetGuid(attachedParty));
                        }
                        break;
                    case "<StationaryStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_partyComponent":
                        SNNSO.Add(fieldInfo, new PartyComponentSerializer((PartyComponent)value));
                        break;
                    case "_pureSpeedExplainer":
                        // TODO Joke Fix this
                        break;
                    case "<TaleWorlds.CampaignSystem.ILocatable<TaleWorlds.CampaignSystem.MobileParty>.NextLocatable>k__BackingField":
                        // TODO Joke Fix this
                        break;
                    case "<AiBehaviorObject>k__BackingField":
                        break;
                    case "<LastVisitedSettlement>k__BackingField":
                    case "_currentSettlement":
                    case "_actualClan":
                    case "_targetSettlement":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;
                }
            }

            // TODO manage collections

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }
        }

        public override object Deserialize()
        {
            mobileParty = new MobileParty();

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(mobileParty, entry.Value.Deserialize());
            }

            return base.Deserialize(mobileParty);
        }

        public override void ResolveReferenceGuids()
        {
            if (mobileParty == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Value.ResolveReferenceGuids();
            }

            foreach(KeyValuePair<FieldInfo, Guid> reference in references)
            {
                reference.Key.SetValue(mobileParty, CoopObjectManager.GetObject(reference.Value));
            }

            List<MobileParty> attachedParties = this.attachedParties
                .Select(x => CoopObjectManager.GetObject<MobileParty>(x)).ToList();

            mobileParty.GetType()
                .GetField("<AttachedParties>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, new MBReadOnlyList<MobileParty>(attachedParties));
        }
    }
}
