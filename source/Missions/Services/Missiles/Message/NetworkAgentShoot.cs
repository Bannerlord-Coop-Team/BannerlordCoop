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
using System.Reflection;
using Autofac;

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
        public Vec3 Orientations{ get; }
        [ProtoMember(5)]
        public Vec3 Orientationf { get; }
        [ProtoMember(6)]
        public Vec3 Orientationu { get; }
        [ProtoMember(7)]
        public bool HasRigidBody { get; }

        public ItemObject ItemObject
        {
            get { return UnpackItemObject(); }
            set { _packedItemObject = PackItemObject(value); }
        }
        private ItemObject _itemObject;
        [ProtoMember(8)]
        private byte[] _packedItemObject;
        private readonly IBinaryPackageFactory packageFactory;

        public ItemModifier ItemModifier
        {
            get { return UnpackItemModifier(); }
            set { _packedItemModifier = PackItemModifier(value); }
        }
        private ItemModifier _itemModifier;
        [ProtoMember(9)]
        private byte[] _packedItemModifier;
        [ProtoMember(10)]
        private bool isItemModifierNull = false;
        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }
        }
        private Banner _banner;
        [ProtoMember(11)]
        private byte[] _packedBanner;
        [ProtoMember(12)]
        private bool isBannerNull = false;

        [ProtoMember(13)]
        public int MissileIndex { get; }

        [ProtoMember(14)]
        public float BaseSpeed { get; }
        [ProtoMember(15)]
        public float Speed { get; }

        public NetworkAgentShoot(Guid agentGuid, Vec3 position, Vec3 velocity, Vec3 orientationS, Vec3 orientationF, Vec3 orientationU, bool hasRigidBody, ItemObject itemObject, ItemModifier itemModifier, Banner banner, int missileIndex, float baseSpeed, float speed)
        {
            AgentGuid = agentGuid;
            Position = position;
            Velocity = velocity;
            Orientations = orientationS;
            Orientationf = orientationF;
            Orientationu = orientationU;
            HasRigidBody = hasRigidBody;
            ItemObject = itemObject;
            ItemModifier = itemModifier;
            Banner = banner;
            MissileIndex = missileIndex;
            BaseSpeed = baseSpeed;
            Speed = speed;
        }

        private ItemObject UnpackItemObject()
        {
            if (_itemObject != null) return _itemObject;

            var itemObject = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(_packedItemObject);

            _itemObject = itemObject.Unpack<ItemObject>(packageFactory);

            return _itemObject;
        }

        private byte[] PackItemObject(ItemObject value)
        {
            var itemObject = new ItemObjectBinaryPackage(value, packageFactory);
            itemObject.Pack();

            return BinaryFormatterSerializer.Serialize(itemObject);
        }

        private ItemModifier UnpackItemModifier()
        {
            if (_itemModifier != null) return _itemModifier;

            if (isItemModifierNull) return null;

            var ItemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);

            _itemModifier = ItemModifier.Unpack<ItemModifier>(packageFactory);

            return _itemModifier;
        }

        private byte[] PackItemModifier(ItemModifier value)
        {
            if (value == null)
            {
                isItemModifierNull = true;
                return Array.Empty<byte>();
            }
            var ItemModifier = new ItemModifierBinaryPackage(value, packageFactory);
            ItemModifier.Pack();

            return BinaryFormatterSerializer.Serialize(ItemModifier);
        }

        private Banner UnpackBanner()
        {
            if (_banner != null) return _banner;
            if (isBannerNull) return null;

            var Banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);
            _banner = Banner.Unpack<Banner>(packageFactory);

            return _banner;
        }

        private byte[] PackBanner(Banner value)
        {
            if (value == null)
            {
                isBannerNull = true;
                return Array.Empty<byte>();
            }
            var Banner = new BannerBinaryPackage(value, packageFactory);
            Banner.Pack();

            return BinaryFormatterSerializer.Serialize(Banner);
        }
    }
}