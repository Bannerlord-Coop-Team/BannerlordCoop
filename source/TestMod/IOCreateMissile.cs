using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CoopTestMod
{
    //Represents a "serializer" of the CreateMissile class
    //Later, we have to convert (create a new class) CreateMissile message into a Serializable message class.
    public static class IOCreateMissile
    {
        public static CreateMissile Read(byte[] bytes,Agent agent,int StartIndex)
        {
            int missileIndex = BitConverter.ToInt32(bytes, StartIndex);
            EquipmentIndex WeaponIndex = (EquipmentIndex)BitConverter.ToInt32(bytes, StartIndex + 4);
            Vec3 Position = new Vec3(
                new Vec2(
                    BitConverter.ToSingle(bytes, StartIndex + 8), 
                    BitConverter.ToSingle(bytes, StartIndex + 12)), 
                BitConverter.ToSingle(bytes, StartIndex + 16));
            Vec3 Direction = new Vec3(
                new Vec2(
                    BitConverter.ToSingle(bytes, StartIndex + 20),
                    BitConverter.ToSingle(bytes, StartIndex + 24)),
                BitConverter.ToSingle(bytes, StartIndex + 28));
            float Speed = BitConverter.ToSingle(bytes, StartIndex + 32);
            Mat3 Orientation =
                new Mat3
                (
                new Vec3(
                new Vec2(
                    BitConverter.ToSingle(bytes, StartIndex + 36),
                    BitConverter.ToSingle(bytes, StartIndex + 40)),
                BitConverter.ToSingle(bytes, StartIndex + 44)),
                new Vec3(
                new Vec2(
                    BitConverter.ToSingle(bytes, StartIndex + 48),
                    BitConverter.ToSingle(bytes, StartIndex + 52)),
                BitConverter.ToSingle(bytes, StartIndex + 56)),
                new Vec3(
                new Vec2(
                    BitConverter.ToSingle(bytes, StartIndex + 60),
                    BitConverter.ToSingle(bytes, StartIndex + 64)),
                BitConverter.ToSingle(bytes, StartIndex + 68))
                );
            bool HasRigidBody = BitConverter.ToBoolean(bytes, StartIndex + 72);
            bool IsPrimaryWeaponShot = BitConverter.ToBoolean(bytes, StartIndex + 73);

            return new CreateMissile(
                missileIndex,
                agent,
                WeaponIndex,
                MissionWeapon.Invalid,
                Position,
                Direction,
                Speed,
                Orientation,
                HasRigidBody,
                null,
                IsPrimaryWeaponShot);
        }

        public static void Write(System.IO.BinaryWriter writer, CreateMissile Message)
        {
            Vec3 Pos = Message.Position;
            Vec3 Dir = Message.Direction;
            Mat3 Ori= Message.Orientation;
            Vec3 S = Ori.s;
            Vec3 F = Ori.f;
            Vec3 U = Ori.u;

            writer.Write(Message.MissileIndex);
            writer.Write((int)Message.WeaponIndex);
            writer.Write(Pos.X);
            writer.Write(Pos.Y);
            writer.Write(Pos.Z);
            writer.Write(Dir.X);
            writer.Write(Dir.Y);
            writer.Write(Dir.Z);
            writer.Write(Message.Speed);
            writer.Write(S.X);
            writer.Write(S.Y);
            writer.Write(S.Z);
            writer.Write(F.X);
            writer.Write(F.Y);
            writer.Write(F.Z);
            writer.Write(U.X);
            writer.Write(U.Y);
            writer.Write(U.Z);
            writer.Write(Message.HasRigidBody);
            writer.Write(Message.IsPrimaryWeaponShot);

        }
    }
}
