//using GameInterface.Services.Registry;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.Core;
//using TaleWorlds.ObjectSystem;

//namespace GameInterface.Services.BasicCharacterObjects
//{
//    internal class CultureObjectRegistry : RegistryBase<BasicCultureObject>
//    {

//        private const string CultureStringIdPrefix = "CoopCulture";
//        private int InstanceCounter = 0;

//        public override IEnumerable<Type> ManagedTypes => new Type[]
//        {
//            typeof(BasicCultureObject),
//            typeof(CultureObject)
//        };

//        public CultureObjectRegistry(IRegistryCollection collection) : base(collection) { }

//        public override void RegisterAll()
//        {
//            var objectManager = MBObjectManager.Instance;

//            if (objectManager == null)
//            {
//                Logger.Error("Unable to register objects when CampaignObjectManager is null");
//                return;
//            }

//            foreach (var culture in objectManager.GetObjectTypeList<CultureObject>())
//            {
//                RegisterExistingObject(culture.StringId, culture);
//            }
//        }

//        protected override string GetNewId(BasicCultureObject culture)
//        {
//            culture.StringId = $"{CultureStringIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
//            return culture.StringId;
//        }
//    }
//}
