using System;
using NLog;
using RailgunNet;
using RailgunNet.System.Encoding;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Railgun encoders and decoders for the remote procedure calls.
    /// </summary>
    public static class MethodCallSerializer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

            if (buffer.ByteSize > RailConfig.MAXSIZE_EVENT)
            {
                // Railgun will not be able to pack the event into a frame. This means the RPC will
                // never be synchronized. This is a fundamental issue which cannot be ignored!
                //
                // What to do now?
                // - Are all arguments necessary? 
                // - Can one or more arguments instead be stored in a RemoteStore and referenced
                //   via the store id (uint32)?
                // - If the size cannot be reduced, RailgunNet.RailConfig.MAXSIZE_EVENT needs to be
                //   increased. This also means that fewer events will fit into one frame. That will
                //   also increase the risk of starvation of the event queue, leading to dropped
                //   events.
                Logger.Fatal(
                    "Serialized RPC {call} is too large to be sent through Railgun. This is a fundamental configuration issue, synchronization cannot continue.",
                    pack);
                throw new ArgumentOutOfRangeException(
                    $"Serialized RPC {pack} is too large to be sent through Railgun.");
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
