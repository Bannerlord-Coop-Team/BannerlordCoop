//using GameInterface.Serializers;
//using System;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.ObjectSystem;

//namespace GameInterface.Serializers.CustomSerializers
//{
//    [Serializable]
//    public class CultureObjectSerializer : CustomSerializerBase
//    {
//        string stringId;
//        public CultureObjectSerializer(CultureObject culture) : base(culture)
//        {
//            // TODO Find way to work better with other mods
//            stringId = culture.StringId;
//        }

//        public override object Deserialize()
//        {
//            CultureObject cultureObject = MBObjectManager.Instance.GetObject<CultureObject>(stringId);
//            return cultureObject;
//        }

//        public override void ResolveReferences()
//        {
//            // No references
//        }
//    }
//}