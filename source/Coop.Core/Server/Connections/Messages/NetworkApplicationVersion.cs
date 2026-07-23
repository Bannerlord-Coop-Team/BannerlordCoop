using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkApplicationVersion
{
    [ProtoMember(1)]
    public int ApplicationVersionType { get; }
    
    [ProtoMember(2)]
    public int Major { get; }
    
    [ProtoMember(3)]
    public int Minor { get; }
    
    [ProtoMember(4)]
    public int Revision { get; }
    
    [ProtoMember(5)]
    public int ChangeSet { get; }

    public NetworkApplicationVersion(int applicationVersionType, int major, int minor, int revision, int changeSet)
    {
        ApplicationVersionType = applicationVersionType;
        Major = major;
        Minor = minor;
        Revision = revision;
        ChangeSet = changeSet;
    }
}