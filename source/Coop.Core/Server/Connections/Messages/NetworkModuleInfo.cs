using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Core.Server.Connections.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkModuleInfo
{
    [ProtoMember(1)]
    public string Id { get; }
    [ProtoMember(2)]
    public bool IsOfficial { get; }
    [ProtoMember(3)]
    public NetworkApplicationVersion Version { get; }

    public NetworkModuleInfo(string id, bool isOfficial, ApplicationVersion version)
    {
        Id = id;
        IsOfficial = isOfficial;
        Version = new NetworkApplicationVersion((int) version.ApplicationVersionType, version.Major, version.Minor, version.Revision, version.ChangeSet);
    }
}