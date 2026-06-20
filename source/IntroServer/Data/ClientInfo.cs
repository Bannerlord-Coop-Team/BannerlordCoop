using IntroServer.Extensions;
using ProtoBuf;
using System;
using System.Linq;
using System.Text;

namespace IntroServer.Data
{
    [ProtoContract]
    public class ClientInfo
    {
        public ClientInfo(string clientId, Version version)
        {
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException($"{nameof(clientId)} is invalid, it cannot be null or empty.");
            if (version == null) throw new ArgumentNullException($"{nameof(version)} cannot be null.");

            ClientId = clientId;
            ModVersion = version;
        }

        private readonly static char Delimiter = '%';

        [ProtoMember(1)]
        public string ClientId { get; }
        [ProtoMember(2)]
        public Version ModVersion { get; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            string[] items = new string[]
            {
                ClientId,
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

                string clientId = values[0];
                Version modVersion = Version.Parse(values[1]);

                if (string.IsNullOrEmpty(clientId)) return false;
                if (modVersion == null) return false;

                clientInfo = new ClientInfo(clientId, modVersion);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}