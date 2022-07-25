using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public struct MissileFireRequest
    {
        [ProtoMember(1)]
        public string agentID;

        [ProtoMember(2)]
        public int weaponIndex;

        [ProtoMember(3)]
        public float posX;

        [ProtoMember(4)]
        public float posY;

        [ProtoMember(5)]
        public float posZ;

        [ProtoMember(6)]
        public float dirX;

        [ProtoMember(7)]
        public float dirY;

        [ProtoMember(8)]
        public float dirZ;

        [ProtoMember(9)]
        public float sx;

        [ProtoMember(10)]
        public float sy;

        [ProtoMember(11)]
        public float sz;

        [ProtoMember(12)]
        public float fx;

        [ProtoMember(13)]
        public float fy;

        [ProtoMember(14)]
        public float fz;

        [ProtoMember(15)]
        public float ux;

        [ProtoMember(16)]
        public float uy;

        [ProtoMember(17)]
        public float uz;

        [ProtoMember(18)]
        public bool hasRigidBody;

        [ProtoMember(19)]
        public float baseSpeed;

        [ProtoMember(20)]
        public float speed;
    }
}
