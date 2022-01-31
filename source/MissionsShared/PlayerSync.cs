using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace MissionsShared
{
    public class PlayerTickSync
    {

        public float posX { get;  set; }
        public float posY { get; set; }
        public float posZ { get; set; }
        public uint movementFlag { get; set; }
        public uint eventFlag { get; set; }
        public float inputVectorX { get; set; }
        public float inputVectorY { get; set; }
        public float action1Progress { get; set; }
        public ulong action1Flag { get; set; }
        public int action1Index { get; set; }
        public int action1CodeType { get; set; }
        public float action2Progress { get; set; }
        public ulong action2Flag { get; set; }
        public int action2Index { get; set; }
        public int action2CodeType { get; set; }
        public float lookDirectionX { get; set; }
        public float lookDirectionY { get; set; }
        public float lookDirectionZ { get; set; }
        public bool crouchMode { get; set; }

        public PlayerTickSync() { }
        public PlayerTickSync(float posX, float posY, float posZ, uint movementFlag, uint eventFlag, float inputVectorX,
            float inputVectorY, float action1Progress, ulong action1Flag, int action1Index, int action1CodeType, float action2Progress, ulong action2Flag, int action2Index, int action2CodeType,
            float lookDirectionX, float lookDirectionY, float lookDirectionZ, bool crouchMode)
            
        {
            this.posX = posX;  
            this.posY = posY;  
            this.posZ = posZ;
            this.movementFlag = movementFlag;
            this.eventFlag = eventFlag;
            this.inputVectorX = inputVectorX;
            this.inputVectorY = inputVectorY;
            this.action1Progress = action1Progress;
            this.action1Flag = action1Flag;
            this.action1Index = action1Index;
            this.action1CodeType = action1CodeType;
            this.action2Flag = action2Flag;
            this.action2Index = action2Index;
            this.action2CodeType = action2CodeType;
            this.action2Progress = action2Progress;
            this.lookDirectionX = lookDirectionX;
            this.lookDirectionY = lookDirectionY;
            this.lookDirectionZ = lookDirectionZ;
            this.crouchMode = crouchMode;

        }

        public static dynamic Cast(dynamic obj, Type castTo)
        {
            return Convert.ChangeType(obj, castTo);
        }
        public byte[] Serialize(MemoryStream stream)
        {
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
            {
                foreach (FieldInfo field in typeof(PlayerTickSync).GetFields(BindingFlags.Instance |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Public))
                {
                    var currField = field.GetValue(this);
                    writer.Write(Cast(currField, field.FieldType));

                }
            }
            return stream.ToArray();
        }

        public void Deserialize(byte [] data)
        {

                int index = 0;
            foreach (FieldInfo field in typeof(PlayerTickSync).GetFields(BindingFlags.Instance |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Public))
            {
                if (field.FieldType == typeof(int))
                {
                    field.SetValue(this, BitConverter.ToInt32(data, index));
                    index += sizeof(int);
                }
                else if (field.FieldType == typeof(uint))
                {
                    field.SetValue(this, BitConverter.ToUInt32(data, index));
                    index += sizeof(uint);
                }
                else if (field.FieldType == typeof(float))
                {
                    field.SetValue(this, BitConverter.ToSingle(data, index));
                    index += sizeof(float);
                }
                else if (field.FieldType == typeof(ulong))
                {
                    field.SetValue(this, BitConverter.ToUInt64(data, index));
                    index += sizeof(ulong);
                }
                else if (field.FieldType == typeof(bool))
                {
                    field.SetValue(this, BitConverter.ToBoolean(data, index));
                    index += sizeof(bool);
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (FieldInfo field in typeof(PlayerTickSync).GetFields(BindingFlags.Instance |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Public))
            {
                var currField = field.GetValue(this);
                sb.Append("[").Append(field.Name).Append(":").Append(currField).Append("]");

            }
            return sb.ToString();
        }

    }



}




