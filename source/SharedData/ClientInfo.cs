using System;
using System.Linq;
using System.Text;
using ProtoBuf;
using SharedData.Extensions;

namespace SharedData
{
    [ProtoContract]
    public class ClientInfo
    {
        public ClientInfo(Guid clientId, Version version)
        {
            if (clientId == Guid.Empty) throw new ArgumentNullException($"{nameof(clientId)} is invalid, use Guid.NewGuid().");
            if (version == null) throw new ArgumentNullException($"{nameof(version)} cannot be null.");

            ClientId = clientId;
            ModVersion = version;
        }

        private readonly static char Delimiter = '%';

        [ProtoMember(1)]
        public Guid ClientId { get; }
        [ProtoMember(2)]
        public Version ModVersion { get; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            string[] items = new string[]
            {
                ClientId.ToString(),
                ModVersion.ToString(),
            };

            ValidateItems(items);

            stringBuilder.AppendJoin(Delimiter, items);

            return stringBuilder.ToString();
        }
        private void ValidateItems(string[] items)
        {
            foreach (string item in items)
            {
                if (item.Contains(Delimiter)) throw new InvalidCastException($"{item} cannot contain {Delimiter}");
            }
        }

        public static bool TryParse(string token, out ClientInfo clientInfo)
        {
            clientInfo = null;

            try
            {
                if (token == null) return false;
                if (token.Length == 0) return false;

                string[] values = token.Split(Delimiter);

                if (values.Length != 2) return false;

                Guid guid = new Guid(values[0]);
                Version modVersion = Version.Parse(values[1]);

                if (guid == Guid.Empty) return false;
                if (modVersion == null) return false;

                clientInfo = new ClientInfo(guid, modVersion);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}