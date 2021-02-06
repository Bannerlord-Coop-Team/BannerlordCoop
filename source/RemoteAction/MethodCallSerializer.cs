using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using RailgunNet;
using RailgunNet.System.Encoding;
using Sync;

namespace RemoteAction
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
            var bufferSizeBefore = buffer.ByteSize;
            buffer.WriteInt(pack.Id.InternalValue);
            buffer.EncodeEventArg(pack.Instance);
            buffer.WriteInt(pack.Arguments.Count());
            foreach (var arg in pack.Arguments) buffer.EncodeEventArg(arg);

            var eventByteSize = buffer.ByteSize - bufferSizeBefore;
            if (eventByteSize > RailConfig.MAXSIZE_EVENT)
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
            var id = new MethodId(buffer.ReadInt());
            var instance = buffer.DecodeEventArg();
            var iNumberOfArguments = buffer.ReadInt();
            var args = new List<Argument>();
            for (var i = 0; i < iNumberOfArguments; ++i) args.Add(buffer.DecodeEventArg());

            return new MethodCall(id, instance, args);
        }
    }
}