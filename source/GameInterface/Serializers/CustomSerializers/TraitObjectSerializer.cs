//using GameInterface.Serializers;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.CharacterDevelopment;
//using TaleWorlds.ObjectSystem;

//namespace Coop.Mod.Serializers.Custom
//{
//    [Serializable]
//    public class TraitObjectSerializer : CustomSerializerBase
//    {
//        string StringId;
//        public TraitObjectSerializer(MBObjectBase obj) : base(obj)
//        {
//            StringId = obj.StringId;
//        }

//        public override object Deserialize()
//        {

//            TraitObject newTraitObject = new TraitObject(StringId);
//            return base.Deserialize(newTraitObject);
//        }

//        public override void ResolveReferences()
//        {
//            // No references
//        }
//    }
//}
