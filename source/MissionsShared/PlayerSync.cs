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
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayerTickSync
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


        
        public void TestGetObjectData()
        {
            foreach (FieldInfo field in typeof(PlayerTickSync).GetFields(BindingFlags.Instance |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Public))
            {
                Console.WriteLine(field.Name + " : " + field.GetValue(this));
            }
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
                Console.WriteLine(data.Length);
                int j = 0;
                foreach (FieldInfo field in typeof(PlayerTickSync).GetFields(BindingFlags.Instance |
                                                     BindingFlags.NonPublic |
                                                     BindingFlags.Public))
                {
                    dynamic d = Cast(field.GetValue(this), field.FieldType);

                    index += Marshal.SizeOf(d);
                    
                    Console.WriteLine("Index: " + index);
                }
            
               
                
            
        }
        
    }



}




