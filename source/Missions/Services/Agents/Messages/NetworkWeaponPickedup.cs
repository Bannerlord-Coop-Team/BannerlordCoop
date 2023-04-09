using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization.Native;
using ProtoBuf;
using Serilog.Core;
using System;
using System.Diagnostics;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkWeaponPickedup : INetworkEvent
    {   
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public ItemObject ItemObject
        {
            get { return UnpackItemObject(); }
            set { _packedItemObject = PackItemObject(value); }
        }
        [ProtoMember(3)]
        private byte[] _packedItemObject;
        private ItemObject _itemObject;

        public ItemModifier ItemModifier
        {
            get { return UnpackItemModifier(); }
            set { _packedItemModifier = PackItemModifier(value); }
        }
        [ProtoMember(4)]
        private byte[] _packedItemModifier;
        private ItemModifier _itemModifier;
        [ProtoMember(5)]
        private bool isItemModifierNull = false;

        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }    
        }
        [ProtoMember(6)]
        private byte[] _packedBanner;
        private Banner _banner;
        [ProtoMember(7)]
        private bool isBannerNull = false;

        public NetworkWeaponPickedup(Guid agentId, EquipmentIndex equipmentIndex, ItemObject weaponObject, ItemModifier itemModifier, Banner banner)
        {
            ContainerBuilder builder = new ContainerBuilder();
            container = builder.Build();

            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }

        public NetworkWeaponPickedup() { }

        IContainer container;

        private byte[] PackItemObject(ItemObject itemObject)
        {
            var factory = container.Resolve<IBinaryPackageFactory>();
            var itemObjectPackage = new ItemObjectBinaryPackage(itemObject, factory);
            itemObjectPackage.Pack();
            return BinaryFormatterSerializer.Serialize(itemObjectPackage);
        }

        private ItemObject UnpackItemObject()
        {
            if (_itemObject != null) return _itemObject;

            var factory = container.Resolve<IBinaryPackageFactory>();
            var itemObject = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(_packedItemObject);
            itemObject.BinaryPackageFactory = factory;

            _itemObject = itemObject.Unpack<ItemObject>(factory);
            return _itemObject;
        }

        private byte[] PackItemModifier(ItemModifier itemModifier)
        {
            if (itemModifier == null)
            {
                isItemModifierNull = true;
                return Array.Empty<byte>();
            }
            var factory = container.Resolve<IBinaryPackageFactory>();

            var itemModifierPackage = new ItemModifierBinaryPackage(itemModifier, factory); 

            itemModifierPackage.Pack();

            return BinaryFormatterSerializer.Serialize(itemModifierPackage);
        }

        private ItemModifier UnpackItemModifier()
        {
            if (_itemModifier != null) return _itemModifier;

            if (isItemModifierNull) return null;

            var factory = container.Resolve<IBinaryPackageFactory>();
            var itemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);
            itemModifier.BinaryPackageFactory = factory;

            _itemModifier = itemModifier.Unpack<ItemModifier>(factory);
            return _itemModifier;
        }

        private byte[] PackBanner(Banner banner)
        {
            if (banner == null)
            {
                isBannerNull = true;
                return Array.Empty<byte>();
            }
            var factory = container.Resolve<IBinaryPackageFactory>();
            var bannerPackage = new BannerBinaryPackage(banner, factory);
            bannerPackage.Pack();

            return BinaryFormatterSerializer.Serialize(bannerPackage);
        }

        private Banner UnpackBanner()
        {
            if(_banner != null) return _banner;
            if (isBannerNull) return null;

            var factory = container.Resolve<IBinaryPackageFactory>();
            var banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);
            banner.BinaryPackageFactory = factory;

            _banner = banner.Unpack<Banner>(factory);

            return _banner;
        }
    }
}