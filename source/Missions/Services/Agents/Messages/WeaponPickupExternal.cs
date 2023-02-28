using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using Serilog.Core;
using System;
using System.Diagnostics;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class WeaponPickupExternal : INetworkEvent
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

        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }    
        }
        [ProtoMember(5)]
        private byte[] _packedBanner;
        private Banner _banner;

        public WeaponPickupExternal(Guid agentId, EquipmentIndex equipmentIndex, ItemObject weaponObject, ItemModifier itemModifier, Banner banner)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }

        public WeaponPickupExternal() { }

        private byte[] PackItemObject(ItemObject itemObject)
        {
            var factory = new BinaryPackageFactory();
            var itemObjectPackage = new ItemObjectBinaryPackage(itemObject, factory);
            itemObjectPackage.Pack();
            return BinaryFormatterSerializer.Serialize(itemObjectPackage);
        }

        private ItemObject UnpackItemObject()
        {
            if (_itemObject != null) return _itemObject;

            var factory = new BinaryPackageFactory();
            var itemObject = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(_packedItemObject);
            itemObject.BinaryPackageFactory = factory;

            _itemObject = itemObject.Unpack<ItemObject>();
            return _itemObject;
        }

        private byte[] PackItemModifier(ItemModifier itemModifier)
        {
            var factory = new BinaryPackageFactory();
            var itemModifierPackage = new ItemModifierBinaryPackage(itemModifier, factory);
            itemModifierPackage.Pack();

            return BinaryFormatterSerializer.Serialize(itemModifierPackage);
        }

        private ItemModifier UnpackItemModifier()
        {
            if (_itemModifier != null) return _itemModifier;

            var factory = new BinaryPackageFactory();
            var itemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);
            itemModifier.BinaryPackageFactory = factory;

            _itemModifier = itemModifier.Unpack<ItemModifier>();
            return _itemModifier;
        }

        private byte[] PackBanner(Banner banner)
        {
            var factory = new BinaryPackageFactory();
            var bannerPackage = new BannerBinaryPackage(banner, factory);
            bannerPackage.Pack();

            return BinaryFormatterSerializer.Serialize(bannerPackage);
        }

        private Banner UnpackBanner()
        {
            if(_banner != null) return _banner;

            var factory = new BinaryPackageFactory();
            var banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);
            banner.BinaryPackageFactory = factory;

            _banner = banner.Unpack<Banner>();

            return _banner;
        }
    }
}