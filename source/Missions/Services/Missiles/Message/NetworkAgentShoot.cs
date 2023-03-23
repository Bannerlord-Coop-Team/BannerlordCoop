using Common.Messaging;
using GameInterface.Serialization.External;
using GameInterface.Serialization;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Common.Serialization;

namespace Missions.Services.Missiles.Message
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkAgentShoot : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }
        [ProtoMember(2)]
        public Vec3 Position { get; }
        [ProtoMember(3)]
        public Vec3 Velocity { get; }
        [ProtoMember(4)]
        public Mat3 Orientation{ get; }
        [ProtoMember(5)]
        public bool HasRigidBody { get; }

        public ItemObject ItemObject
        {
            get { return UnpackItemObject(); }
            set { _packedItemObject = PackItemObject(value); }
        }
        private ItemObject _itemObject;
        [ProtoMember(6)]
        private byte[] _packedItemObject;

        public ItemModifier ItemModifier
        {
            get { return UnpackItemModifier(); }
            set { _packedItemModifier = PackItemModifier(value); }
        }
        private ItemModifier _itemModifier;
        [ProtoMember(7)]
        private byte[] _packedItemModifier;

        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }
        }
        private Banner _banner;
        [ProtoMember(8)]
        private byte[] _packedBanner;

        [ProtoMember(9)]
        public int MissileIndex { get; }

        public NetworkAgentShoot(Guid agentGuid, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, ItemObject itemObject, ItemModifier itemModifier, Banner banner, int missileIndex)
        {
            AgentGuid = agentGuid;
            Position = position;
            Velocity = velocity;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            ItemObject = itemObject;
            ItemModifier = itemModifier;
            Banner = banner;
            MissileIndex = missileIndex;
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

        private byte[] PackItemObject(ItemObject value)
        {
            var factory = new BinaryPackageFactory();
            var itemObject = new ItemObjectBinaryPackage(value, factory);
            itemObject.Pack();

            return BinaryFormatterSerializer.Serialize(itemObject);
        }

        private ItemModifier UnpackItemModifier()
        {
            if (_itemModifier != null) return _itemModifier;

            var factory = new BinaryPackageFactory();
            var ItemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);
            ItemModifier.BinaryPackageFactory = factory;

            _itemModifier = ItemModifier.Unpack<ItemModifier>();

            return _itemModifier;
        }

        private byte[] PackItemModifier(ItemModifier value)
        {
            if (value == null) { value = new ItemModifier(); }
            var factory = new BinaryPackageFactory();
            var ItemModifier = new ItemModifierBinaryPackage(value, factory);
            ItemModifier.Pack();

            return BinaryFormatterSerializer.Serialize(ItemModifier);
        }

        private Banner UnpackBanner()
        {
            if (_banner != null) return _banner;

            var factory = new BinaryPackageFactory();
            var Banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);
            Banner.BinaryPackageFactory = factory;

            _banner = Banner.Unpack<Banner>();

            return _banner;
        }

        private byte[] PackBanner(Banner value)
        {
            if (value == null) { value = new Banner(); }
            var factory = new BinaryPackageFactory();
            var Banner = new BannerBinaryPackage(value, factory);
            Banner.Pack();

            return BinaryFormatterSerializer.Serialize(Banner);
        }
    }
}
