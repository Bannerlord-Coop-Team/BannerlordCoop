using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using RailgunNet.System.Encoding;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Coop.Mod.Data
{
    [Serializable]
    public class PartyData : IEquatable<PartyData>
    {
        string name;
        int gold;
        uint id;
        public PartyData(MobileParty party)
        {
            name = party.Name.ToString();
            gold = party.PartyTradeGold;
            id = party.Id.InternalValue;
        }

        public bool Equals(PartyData other)
        {
            return name == other.name &&
                gold == other.gold &&
                id == other.id;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(name, gold, id).GetHashCode();
        }
    }

    /// <summary>
    ///     Railgun encoder & decoder for the party data.
    /// </summary>
    public static class PartyDataSerializer
    {

        [Encoder]
        public static void WriteMovementState(this RailBitBuffer buffer, PartyData partyData)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, partyData);
                buffer.WriteByteArray(stream.ToArray());
            }
        }

        [Decoder]
        public static PartyData ReadMovementState(this RailBitBuffer buffer)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream(buffer.ReadByteArray()))
            {
                return (PartyData)formatter.Deserialize(stream);
            }
        }
    }

    public class PartyDataComparer : IEqualityComparer<PartyData>
    {
        public bool Equals(PartyData x, PartyData y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(PartyData obj)
        {
            return obj.GetHashCode();
        }
    }
}
