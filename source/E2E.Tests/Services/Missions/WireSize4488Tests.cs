using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Surrogates;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

// TEMP #4488 measurement, removed before the PR leaves draft. The same file runs on the baseline and the candidate
// and writes the serialized size of each payload, so the two outputs diff field battle payloads byte for byte.
public class WireSize4488Tests
{
    private readonly ProtoBufSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());
    private readonly ITestOutputHelper output;

    public WireSize4488Tests(ITestOutputHelper output)
    {
        this.output = output;
        _ = new SurrogateCollection();
    }

    [Fact]
    public void WriteWireSizes()
    {
        var rows = new List<string>();
        void Row(string name, int bytes) => rows.Add(name + "\t" + bytes);

        using (var fixture = new MissionEngineFixture())
        {
            // imperial_legionary in melee: sword in the main hand, shield in the off hand, on foot.
            AgentActionData idle = Legionary(action1Index: -1, sword: true);
            AgentActionData attacking = Legionary(action1Index: 1187, sword: true);
            AgentActionData idleEquip = Legionary(action1Index: -1, sword: true, equipment: true);
            AgentActionData attackingEquip = Legionary(action1Index: 1187, sword: true, equipment: true);
            AgentActionData unarmedIdleEquip = Legionary(action1Index: -1, sword: false, equipment: true);

            Row("action.entry.idle", Packet(idle));
            Row("action.entry.attacking", Packet(attacking));
            Row("action.entry.idle+equipment", Packet(idleEquip));
            Row("action.entry.attacking+equipment", Packet(attackingEquip));
            Row("action.entry.emptyhands+equipment", Packet(unarmedIdleEquip));
            Row("action.packet.8.idle", Packet(Enumerable.Repeat(idle, 8).ToArray()));
            Row("action.packet.8.half-attacking", Packet(new[] { idle, attacking, idle, attacking, idle, attacking, idle, attacking }));
            Row("action.packet.8.half-attacking+2equipment", Packet(new[] { idleEquip, attacking, idle, attackingEquip, idle, attacking, idle, attacking }));
            Row("action.packet.8.all-equipment", Packet(new[] { idleEquip, attackingEquip, idleEquip, attackingEquip, idleEquip, attackingEquip, idleEquip, attackingEquip }));
        }

        Guid victim = Guid.NewGuid();
        var position = new Vec3(312.4f, 187.9f, 12.6f);
        Row("hit.blood", Message(new NetworkMeleeHitPresentation(victim, false, MeleeHitPresentationKind.Blood, 13,
            position, WeaponClass.OneHandedSword, 7, 0.62f)));
        Row("hit.shield", Message(new NetworkMeleeHitPresentation(victim, false, MeleeHitPresentationKind.ShieldImpact, 21,
            position, WeaponClass.OneHandedSword, 9, 0.48f)));
        Row("hit.bodyimpact-sound", Message(new NetworkMeleeHitPresentation(victim, false, MeleeHitPresentationKind.BodyImpact, -1,
            position, WeaponClass.Undefined, -1, 0f, 412, 2f)));

        Row("siege.ram", Message(new NetworkSiegeMachineState(4021, 2400f, 0, -1, -1, 31.5f, false, -1, -1000f, -1000f,
            hostEpoch: 3, senderControllerId: "testclient", authorityRevision: 2)));
        Row("siege.ladder", Message(new NetworkSiegeMachineState(4022, -1f, -1, -1, 2, -1f, false, -1, -1000f, -1000f,
            hostEpoch: 3, senderControllerId: "testclient", authorityRevision: 2)));
        Row("siege.gate", Message(new NetworkSiegeMachineState(4023, 1800f, 0, 1, -1, -1f, false, -1, -1000f, -1000f,
            hostEpoch: 3, senderControllerId: "testclient", authorityRevision: 2)));
        Row("siege.onager", Message(new NetworkSiegeMachineState(4024, 900f, 0, -1, -1, -1f, false, 2, 1.31f, 0.42f,
            hostEpoch: 3, senderControllerId: "testclient", authorityRevision: 2)));
        Row("siege.stonepile", Message(new NetworkSiegeMachineState(4025, -1f, -1, -1, -1, -1f, false, -1, -1000f, -1000f,
            hostEpoch: 3, stoneAmmo: 18, senderControllerId: "testclient", authorityRevision: 2)));

        string tag = System.Environment.GetEnvironmentVariable("WIRESIZE_TAG") ?? "untagged";
        string path = Path.Combine(Path.GetTempPath(), "wiresize4488-" + tag + ".tsv");
        File.WriteAllLines(path, rows);
        foreach (string row in rows) output.WriteLine(row);
        output.WriteLine(path);
    }

    private AgentActionData Legionary(int action1Index, bool sword, bool equipment = false)
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var mirror = new MirrorAgent
        {
            Controller = AgentControllerType.AI,
            Action0Index = 412,
            Action0Progress = 0.37f,
            Action0CodeType = Agent.ActionCodeType.Other,
            Action1Index = action1Index,
            Action1Progress = action1Index < 0 ? 0f : 0.21f,
            Action1CodeType = action1Index < 0 ? Agent.ActionCodeType.Idle : Agent.ActionCodeType.Other,
            MovementFlags = Agent.MovementControlFlag.Forward,
            PrimaryWieldedItemIndex = sword ? EquipmentIndex.Weapon0 : EquipmentIndex.None,
            OffhandWieldedItemIndex = sword ? EquipmentIndex.Weapon1 : EquipmentIndex.None,
        };
        AgentMirror.Bind(agent, mirror);
        var data = new AgentActionData(agent);
        return equipment ? data.WithEquipment(3, new AgentEquipmentData(agent)) : data;
    }

    private int Packet(params AgentActionData[] actions) =>
        serializer.Serialize(new AgentActionPacket(
            "testclient",
            actions.Select(_ => Guid.NewGuid()).ToArray(),
            actions,
            actions.Select((_, i) => 1000L + i).ToArray(),
            battleHostEpoch: 3)).Length;

    private int Message(Common.Messaging.IMessage message) => MessagePacket.Create(message, serializer).Data.Length;
}
