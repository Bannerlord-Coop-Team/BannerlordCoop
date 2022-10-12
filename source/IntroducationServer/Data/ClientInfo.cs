using System;
using System.Linq;
using System.Text;
using Common.Extensions;

namespace IntroducationServer.Data
{
    public class ClientInfo
    {
        public ClientInfo(Guid clientId, Version version, string instanceName)
        {
            if (clientId == Guid.Empty) throw new ArgumentNullException($"{nameof(clientId)} is invalid, use Guid.NewGuid().");
            if (version == null) throw new ArgumentNullException($"{nameof(version)} cannot be null.");
            if (instanceName == string.Empty) throw new ArgumentNullException($"{nameof(instanceName)} cannot be empty.");
            if (instanceName == null) throw new ArgumentNullException($"{nameof(instanceName)} cannot be null.");

            ClientId = clientId;
            ModVersion = version;
            InstanceName = instanceName;
        }

        private readonly static char Delimiter = '%';

        public Guid ClientId { get; }
        public Version ModVersion { get; }
        public string InstanceName { get; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            string[] items = new string[]
            {
                ClientId.ToString(),
                ModVersion.ToString(),
                InstanceName.ToString(),
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

                if (values.Length != 3) return false;

                Guid guid = new Guid(values[0]);
                Version modVersion = Version.Parse(values[1]);
                string instanceName = values[2];

                if (guid == Guid.Empty) return false;
                if (modVersion == null) return false;
                if (instanceName == null) return false;
                if (instanceName == string.Empty) return false;

                clientInfo = new ClientInfo(guid, modVersion, instanceName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}