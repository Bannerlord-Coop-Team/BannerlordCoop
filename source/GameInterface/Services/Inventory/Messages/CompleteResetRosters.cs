using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompleteResetRosters : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetItemRoster1Id;

    [ProtoMember(2)]
    public readonly string TargetItemRoster2Id;

    [ProtoMember(3)]
    public readonly ItemRosterElement[] BackupItemRoster1Elements;

    [ProtoMember(4)]
    public readonly ItemRosterElement[] BackupItemRoster2Elements;

    public CompleteResetRosters(
        string targetItemRoster1Id,
        string targetItemRoster2Id,
        ItemRosterElement[] backupItemRoster1Elements,
        ItemRosterElement[] backupItemRoster2Elements)
    {
        TargetItemRoster1Id = targetItemRoster1Id;
        TargetItemRoster2Id = targetItemRoster2Id;
        BackupItemRoster1Elements = backupItemRoster1Elements;
        BackupItemRoster2Elements = backupItemRoster2Elements;
    }
}