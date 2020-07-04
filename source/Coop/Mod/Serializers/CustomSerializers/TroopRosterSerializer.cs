using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class TroopRosterSerializer : ICustomSerializer
    {
        readonly List<byte[]> data = new List<byte[]>();
        public TroopRosterSerializer(TroopRoster roster)
        {
            foreach(TroopRosterElement troop in roster)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        typeof(TroopRosterElement)
                            .GetMethod("SerializeTo", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(troop, new object[] { writer });
                        data.Add(stream.ToArray());
                    }
                }
            }
            
        }

        public object Deserialize()
        {
            TroopRoster newRoster = new TroopRoster();
            MethodInfo addTroop = typeof(TroopRoster).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach(byte[] element in data)
            {
                using (MemoryStream stream = new MemoryStream()) {
                    stream.Read(element, 0, element.Length);
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        TroopRosterElement newTroop = new TroopRosterElement();
                        typeof(TroopRosterElement)
                        .GetMethod("DeserializeFrom", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(newTroop, new object[] { reader });
                        addTroop.Invoke(newRoster, new object[] { newTroop });
                    }
                }
            }

            return newRoster;
        }
    }
}