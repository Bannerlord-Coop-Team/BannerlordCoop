using RailgunNet.System.Encoding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers.Encoders
{
    /// <summary>
    ///     RailGun Serializer for Guid
    /// </summary>
    public static class SystemGuid
    {

        [Encoder]
        public static void WriteGuid(this RailBitBuffer buffer, Guid guid)
        {
            buffer.WriteByteArray(guid.ToByteArray());
        }

        [Decoder]
        public static Guid ReadGuid(this RailBitBuffer buffer)
        {
            return new Guid(buffer.ReadByteArray());
        }
    }
}
