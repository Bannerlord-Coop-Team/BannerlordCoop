#if DEBUG
using Common.Commands;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Monsters.Commands;

internal sealed class MonsterRoundTripCoopCommand : ICoopCommand
{
    private readonly IBinaryPackageFactory factory;
    private readonly IObjectManager objectManager;

    public MonsterRoundTripCoopCommand(IBinaryPackageFactory factory, IObjectManager objectManager)
    {
        this.factory = factory;
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.monster";
    public string Name => "roundtrip";
    public string Description => "Checks loaded horse and camel reference serialization without changing game state.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var output = new StringBuilder();
        const string missingId = "Monster_issue3737_unresolved";
        if (objectManager.Contains(missingId))
            return new CoopCommandResult(false, "The unresolved test id already exists.", "command_failed");

        foreach (var stringId in new[] { "horse", "camel" })
        {
            var monster = MBObjectManager.Instance.GetObject<Monster>(stringId);
            if (monster == null || !objectManager.TryGetId(monster, out var id) || id != "Monster_" + stringId)
                return new CoopCommandResult(false, $"Missing stable Monster_{stringId} registration.", "command_failed");
            var item = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .FirstOrDefault(candidate => ReferenceEquals(candidate.HorseComponent?.Monster, monster));
            if (item == null || !objectManager.TryGetId(item, out var itemId))
                return new CoopCommandResult(false, $"No registered mount item for {stringId}.", "command_failed");

            var direct = RoundTrip<MonsterBinaryPackage>(monster);
            var horse = RoundTrip<HorseComponentBinaryPackage>(item.HorseComponent);
            var existing = RoundTrip<ItemObjectBinaryPackage>(item);
            var fallback = RoundTrip<ItemObjectBinaryPackage>(item);
            fallback.stringId = "ItemObject_issue3737_unresolved";
            if (objectManager.Contains(fallback.stringId))
                return new CoopCommandResult(false, "The fallback test id already exists.", "command_failed");
            var clone = fallback.Unpack<ItemObject>(factory);
            var missing = RoundTrip<MonsterBinaryPackage>(monster);
            missing.StringId = missingId;
            var missingHorse = RoundTrip<HorseComponentBinaryPackage>(item.HorseComponent);
            missingHorse.MonsterId = missingId;

            if (direct.StringId != id || !ReferenceEquals(monster, direct.Unpack<Monster>(factory)) ||
                !ReferenceEquals(monster, horse.Unpack<HorseComponent>(factory).Monster) ||
                !ReferenceEquals(item, existing.Unpack<ItemObject>(factory)) ||
                ReferenceEquals(item, clone) || clone.HorseComponent == null ||
                ReferenceEquals(item.HorseComponent, clone.HorseComponent) ||
                !ReferenceEquals(monster, clone.HorseComponent.Monster) ||
                clone.HorseComponent.BodyLength != item.HorseComponent.BodyLength ||
                clone.HorseComponent.ChargeDamage != item.HorseComponent.ChargeDamage ||
                missing.Unpack<Monster>(factory) != null || missingHorse.Unpack<HorseComponent>(factory).Monster != null)
                return new CoopCommandResult(false, $"Reference round trip failed for {stringId}.", "command_failed");

            output.AppendLine($"PASS monster={stringId} id={id} item={itemId} direct=same horse=same fallback=clone/monster-same existing=same unresolved=null");
        }

        return new CoopCommandResult(true, output.ToString());
    }

    private TPackage RoundTrip<TPackage>(object value) where TPackage : IBinaryPackage =>
        BinaryPackageSerializer.Deserialize<TPackage>(
            BinaryPackageSerializer.Serialize(factory.GetBinaryPackage(value)));
}
#endif
