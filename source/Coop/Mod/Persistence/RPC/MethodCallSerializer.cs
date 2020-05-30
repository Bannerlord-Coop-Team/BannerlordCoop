using RailgunNet.System.Encoding;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public static class MethodCallSerializer
    {
        [Encoder]
        public static void WriteMethodCall(this RailBitBuffer buffer, MethodCall pack)
        {
            buffer.WriteInt(pack.Id.InternalValue);
            buffer.EncodeEventArg(pack.Instance);
            buffer.WriteInt(pack.Arguments.Count);
            foreach (Argument arg in pack.Arguments)
            {
                buffer.EncodeEventArg(arg);
            }
        }

        [Decoder]
        public static MethodCall ReadMethodCall(this RailBitBuffer buffer)
        {
            MethodCall pack = new MethodCall();
            pack.Id = new MethodId(buffer.ReadInt());
            pack.Instance = buffer.DecodeEventArg();
            int iNumberOfArguments = buffer.ReadInt();
            for (int i = 0; i < iNumberOfArguments; ++i)
            {
                pack.Arguments.Add(buffer.DecodeEventArg());
            }

            return pack;
        }
    }
}
