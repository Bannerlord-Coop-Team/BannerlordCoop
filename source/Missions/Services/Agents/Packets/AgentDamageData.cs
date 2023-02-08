using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentDamageData : INetworkEvent
    {
        public AgentDamageData(Guid attackerAgentId, Guid victimAgentId, double damage, AttackCollisionData attackCollisionData, Blow blow)
        {
            AttackerAgentId = attackerAgentId;
            VictimAgentId = victimAgentId;
            Damage = damage;
            AttackCollisionData = attackCollisionData;
            Blow = blow;

        }

        public Blow Blow
        {
            get { return UnpackBlow(); }
            set { _packedBlow = PackBlow(value); }
        }

        public AttackCollisionData AttackCollisionData
        {
            get { return UnpackAttackCollisionData(); }
            set { _packedAttackCollisionData = PackAttackCollisionData(value); }
        }


        private Blow _blowObject;
        private AttackCollisionData _attackCollisionDataObject;
        private bool isBlowUnpacked = false;
        private bool isAttackCollisionDataUnpacked = false;


        private byte[] PackBlow(Blow blow)
        {
            var factory = new BinaryPackageFactory();
            var blowPackage = new BlowBinaryPackage(blow, factory);
            blowPackage.Pack();

            return BinaryFormatterSerializer.Serialize(blowPackage);
        }

        private Blow UnpackBlow()
        {
            if (isBlowUnpacked) return _blowObject;

            var factory = new BinaryPackageFactory();
            var blow = BinaryFormatterSerializer.Deserialize<BlowBinaryPackage>(_packedBlow);
            blow.BinaryPackageFactory = factory;

            _blowObject = blow.Unpack<Blow>();
            isBlowUnpacked = true;

            return _blowObject;
        }

        private byte[] PackAttackCollisionData(AttackCollisionData attackCollisionData)
        {
            var factory = new BinaryPackageFactory();
            var attackCollisionDataPackage = new AttackCollisionDataBinaryPackage(attackCollisionData, factory);
            attackCollisionDataPackage.Pack();

            return BinaryFormatterSerializer.Serialize(attackCollisionDataPackage);
        }

        private AttackCollisionData UnpackAttackCollisionData()
        {
            if (isAttackCollisionDataUnpacked) return _attackCollisionDataObject;

            var factory = new BinaryPackageFactory();
            var attackCollisionPackage = BinaryFormatterSerializer.Deserialize<AttackCollisionDataBinaryPackage>(_packedAttackCollisionData);
            attackCollisionPackage.BinaryPackageFactory = factory;

            _attackCollisionDataObject = attackCollisionPackage.Unpack<AttackCollisionData>();
            isAttackCollisionDataUnpacked = true;
            return _attackCollisionDataObject;
        }




        [ProtoMember(1)]
        public Guid AttackerAgentId { get; }
        [ProtoMember(2)]
        public Guid VictimAgentId { get; }

        [ProtoMember(3)]
        public double Damage { get; }

        [ProtoMember(4)]
        private byte[] _packedAttackCollisionData;

        [ProtoMember(5)]
        private byte[] _packedBlow;
    }
}
