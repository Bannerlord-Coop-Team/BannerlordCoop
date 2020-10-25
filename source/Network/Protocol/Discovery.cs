namespace Network.Protocol
{
    /// <summary>
    ///     Payload serializer for a discovery packet.
    /// </summary>
    public static class Discovery
    {
        private const int MagicNumber = 0x13376ED6;
        private static readonly byte[] Payload;

        static Discovery()
        {
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(MagicNumber);
            Payload = writer.ToArray();
        }

        /// <summary>
        ///     Get the payload that is being broadcast by the server.
        /// </summary>
        /// <returns>Payload</returns>
        public static byte[] GetPayload()
        {
            return Payload;
        }

        /// <summary>
        ///     Check if the <see cref="reader"/> contains a discovery packet. If so, deserialize
        ///     it. Otherwise leave the reader as it is.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Was a discovery packet read?</returns>
        public static bool TryDeserialize(ByteReader reader)
        {
            int i = reader.PeekInt32();
            if (i != MagicNumber) return false;
            
            reader.Binary.ReadInt32();
            return true;
        }
    }
}
