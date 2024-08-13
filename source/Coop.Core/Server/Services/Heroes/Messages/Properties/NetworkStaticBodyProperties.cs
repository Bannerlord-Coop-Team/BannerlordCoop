using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Heroes.Messages.Properties;

[ProtoContract(SkipConstructor = true)]
public class NetworkStaticBodyProperties(
    
    string id,
    string target,
    ulong keyParty1,
    ulong keyParty2,
    ulong keyParty3,
    ulong keyParty4,
    ulong keyParty5,
    ulong keyParty6,
    ulong keyParty7,
    ulong keyParty8) : ITargetCommand
{

    [ProtoMember(1)]
    public string Id { get; } = id;
    
    [ProtoMember(2)]
    public string Target { get; } = target;
    
    [ProtoMember(3)]
    public ulong KeyParty1 { get; set; } = keyParty1;

    [ProtoMember(4)]
    public ulong KeyParty2 { get; set; } = keyParty2;

    [ProtoMember(5)]
    public ulong KeyParty3 { get; set; } = keyParty3;

    [ProtoMember(6)]
    public ulong KeyParty4 { get; set; } = keyParty4;

    [ProtoMember(7)]
    public ulong KeyParty5 { get; set; } = keyParty5;

    [ProtoMember(8)]
    public ulong KeyParty6 { get; set; } = keyParty6;

    [ProtoMember(9)]
    public ulong KeyParty7 { get; set; } = keyParty7;

    [ProtoMember(10)]
    public ulong KeyParty8 { get; set; } = keyParty8;
}