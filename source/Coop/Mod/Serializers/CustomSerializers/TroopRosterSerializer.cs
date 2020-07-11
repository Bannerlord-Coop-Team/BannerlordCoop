using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.ServiceModel.Channels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class TroopRosterSerializer : ICustomSerializer
    {
        readonly List<byte[]> data = new List<byte[]>();
        int versionNumber;
        public TroopRosterSerializer(TroopRoster roster)
        {
            versionNumber = roster.VersionNo;
            foreach (TroopRosterElement troop in roster)
            {
                // TaleWorlds BinaryWriter
                BinaryWriter writer = new BinaryWriter();
                // Have to get method info different due to the method being an explicit interface implementation
                MethodInfo serializeTo = typeof(TroopRosterElement)
                    .GetInterfaceMap(typeof(ISerializableObject))
                    .InterfaceMethods.First((methodInfo) => { return methodInfo.Name == "SerializeTo"; });
                serializeTo.Invoke(troop, new object[] { writer });
                data.Add(writer.Data);
            }

        }

        public object Deserialize()
        {
            TroopRoster newRoster = new TroopRoster();

            typeof(TroopRoster)
                .GetField("<VersionNo>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(newRoster, versionNumber);

            List<TroopRosterElement> troops = new List<TroopRosterElement>();
            foreach (byte[] element in data)
            {
                TroopRosterElement newTroop = new TroopRosterElement();
                // TaleWorlds BinaryReader
                BinaryReader reader = new BinaryReader(element);
                // Have to get method info different due to the method being an explicit interface implementation
                MethodInfo deserializeFrom = typeof(TroopRosterElement)
                    .GetInterfaceMap(typeof(ISerializableObject))
                    .InterfaceMethods.First((methodInfo) => { return methodInfo.Name == "DeserializeFrom"; });
                deserializeFrom.Invoke(newTroop, new object[] { reader });
                troops.Add(newTroop);
            }

            typeof(TroopRoster)
                .GetField("data", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newRoster, troops.ToArray());

            return newRoster;
        }
    }
}