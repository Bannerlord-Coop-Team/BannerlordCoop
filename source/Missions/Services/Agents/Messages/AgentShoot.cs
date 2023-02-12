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

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentShoot : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }
        [ProtoMember(2)]
        public EquipmentIndex WeaponIndex { get; }
        [ProtoMember(3)]
        public Vec3 Position { get; }
        [ProtoMember(4)]
        public Vec3 Velocity { get; }

        public Mat3 Orientation
        {
            get { return UnpackMat3(); }
            set { _packedOrientation = PackOrientation(value); }
        }

        private Mat3 _orientation;

        [ProtoMember(5)]
        private byte[] _packedOrientation;

        [ProtoMember(6)]
        public bool HasRigidBody { get; }
        [ProtoMember(7)]
        public int ForcedMissileIndex { get; }

        public AgentShoot(Guid agentGuid, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            AgentGuid = agentGuid;
            WeaponIndex = weaponIndex;
            Position = position;
            Velocity = velocity;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            ForcedMissileIndex = forcedMissileIndex;
        }


        private Mat3 UnpackMat3()
        {
            if (_orientation != null) return _orientation;

            var factory = new BinaryPackageFactory();
            var orientation = BinaryFormatterSerializer.Deserialize<CharacterObjectBinaryPackage>(_packedOrientation);
            orientation.BinaryPackageFactory = factory;

            _orientation = orientation.Unpack<Mat3>();

            return _orientation;
        }

        private byte[] PackOrientation(Mat3 value)
        {
            var factory = new BinaryPackageFactory();
            var orientation = new Mat3BinaryPackage(value, factory);
            orientation.Pack();

            return BinaryFormatterSerializer.Serialize(orientation);
        }
    }
}
