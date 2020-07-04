using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class MobilePartySerializer : CustomSerializer
    {
        [NonSerialized]
        public MobileParty mobileParty;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        List<string> attachedPartiesNames = new List<string>();
        string name;
        
        public MobilePartySerializer(MobileParty mobileParty, PlayerHeroSerializer hero) : base(mobileParty)
        {
            name = mobileParty.Name.ToString();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                object value = fieldInfo.GetValue(mobileParty);

                if (value == null)
                {
                    continue;
                }

                switch (fieldInfo.Name)
                {
                    case "_currentSettlement":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<LastVisitedSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<Ai>k__BackingField":
                        // PartyAi
                        break;
                    case "<Party>k__BackingField":
                        SNNSO.Add(fieldInfo, new PartyBaseSerializer((PartyBase)value, this , hero));
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
                            attachedPartiesNames.Add(attachedParty.Name.ToString());
                        }
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }
        }
        public override object Deserialize()
        {
            MobileParty newMobileParty = MobileParty.Create(name);

            foreach(FieldInfo fieldInfo in SerializableObjects.Keys)
            {
                fieldInfo.SetValue(newMobileParty, SerializableObjects[fieldInfo]);
            }

            foreach(FieldInfo fieldInfo in SNNSO.Keys)
            {
                fieldInfo.SetValue(newMobileParty, SNNSO[fieldInfo]);
            }

            MobileParty.All.FindIndex((party) => { return true; });
            MobileParty clientParty = MBObjectManager.Instance.CreateObject<MobileParty>("");
            clientParty.SetAsMainParty();
            throw new NotImplementedException();
        }
    }
}
