using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class ItemRosterSerializer : ICustomSerializer
    {
        readonly List<byte[]> data = new List<byte[]>();

        public ItemRosterSerializer(ItemRoster roster)
        {
        
            foreach (ItemRosterElement item in roster)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        typeof(ItemRosterElement)
                            .GetMethod("SerializeTo", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(item, new object[] { writer });
                        data.Add(stream.ToArray());
                    }
                }
            }

        }

            public object Deserialize()
            {
                ItemRoster newRoster = new ItemRoster();
                MethodInfo addItem = typeof(ItemRoster).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (byte[] element in data)
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        stream.Read(element, 0, element.Length);
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            ItemRosterElement newItem = new ItemRosterElement();
                            typeof(ItemRosterElement)
                            .GetMethod("DeserializeFrom", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(newItem, new object[] { reader });
                            addItem.Invoke(newRoster, new object[] { newItem });
                        }
                    }
                }

                return newRoster;
            }
        }
}