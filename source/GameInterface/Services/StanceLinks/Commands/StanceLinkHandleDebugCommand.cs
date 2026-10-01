#if DEBUG
using Common;
using Common.Commands;
using Common.Network;
using Common.Messaging;
using HarmonyLib;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.StanceLinks.Messages;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Commands;

public sealed class StanceLinkHandleDebugCommand : ICoopCommand
{
    private static IObjectManager preparedManager;
    private static volatile StanceLink preparedStance;
    private static string preparedId;
    private static volatile uint preparedHandle;
    private static int receivedMessages;

    public string Prefix => "coop.debug.stance_link";
    public string Name => "handle_fixture";
    public string Description => "Prepares, inspects, sends, checks, or restores the missing stance handle fixture.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "prepare, state, send, conflicts, or restore."),
        new ExpectedArgs("faction1", "First registered kingdom id, e.g. Kingdom_vlandia."),
        new ExpectedArgs("faction2", "Second registered kingdom id, e.g. Kingdom_empire."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 3) return Failed("Expected action and two kingdom ids.");
        var action = args[0];
        if (action != "prepare" && action != "state" && action != "send" && action != "conflicts" && action != "restore")
            return Failed("Unknown fixture action.");
        if (action == "send" && ModInformation.IsClient) return Failed("send requires the server.");
        if (action != "send" && action != "state" && ModInformation.IsServer)
            return Failed("Setup and conflict checks require a client.");
        if (!ContainerProvider.TryResolve<IObjectManager>(out var manager)
            || !manager.TryGetObject<Kingdom>(args[1], out var faction1)
            || !manager.TryGetObject<Kingdom>(args[2], out var faction2))
            return Failed("Registered kingdoms and ObjectManager are required.");
        var stance = FactionManager.Instance._stances.GetStance(faction1, faction2);
        if (stance == null || !manager.TryGetId(stance, out var id)) return Failed("An existing registered stance is required.");
        if (id != $"StanceLink_{StanceLinkHandler.GetStanceLinkKey(faction1, faction2)}")
            return Failed("The stance registration does not match the production key.");
        manager.TryGetHandle(stance, out var handle);
        if (action == "send")
        {
            if (handle == 0 || !manager.TryGetId(faction1, out var firstId)
                || !manager.TryGetId(faction2, out var secondId)
                || !ContainerProvider.TryResolve<INetwork>(out var network))
                return Failed("Authoritative stance handle, faction ids and network are required.");
            network.SendAll(new StanceLinkConstructed(firstId, secondId, stance.StanceType, handle));
            return Passed($"STANCE_HANDLE_SENT id={id} handle={handle}");
        }
        if (action == "prepare")
        {
            if (preparedStance != null) return Failed("Restore the active fixture first.");
            if (handle == 0) return Failed("Preparation requires an existing nonzero handle.");
            preparedManager = manager;
            preparedStance = stance;
            preparedId = id;
            preparedHandle = handle;
            Interlocked.Exchange(ref receivedMessages, 0);
            try { RemoveHandleForSetup(manager, stance, handle); }
            catch (Exception error) { return Failed($"Setup failed: {error.Message}; restore is required."); }
        }
        if (action == "conflicts" || action == "restore")
        {
            if (!ReferenceEquals(preparedManager, manager) || !ReferenceEquals(preparedStance, stance) || preparedId != id)
                return Failed("No matching prepared fixture.");
            if (action == "restore")
            {
                if (!manager.AddExisting(id, stance, preparedHandle)) return Failed("Original handle restoration failed.");
                preparedStance = null;
                preparedManager = null;
            }
            else
            {
                if (handle == 0)
                {
                    uint occupied = manager.GetHandleMap().Values.FirstOrDefault(value => value != preparedHandle);
                    if (occupied == 0 || !manager.TryGetObject<object>(occupied, out var occupant)
                        || manager.AddExisting(id, stance, occupied)
                        || manager.TryGetHandle(stance, out _)
                        || !manager.TryGetObject<object>(occupied, out var retained) || !ReferenceEquals(occupant, retained))
                        return Failed("An occupied handle changed the missing-handle registration.");
                }
                else
                {
                    if (handle != preparedHandle) return Failed("The production message has not bound the original handle.");
                    var other = new object();
                    var alternateId = id + "_fixture_conflict";
                    var replacementHandle = handle == uint.MaxValue ? handle - 1 : handle + 1;
                    if (manager.Contains(alternateId)) return Failed("Fixture alternate id is already occupied.");
                    bool rejected = !manager.AddExisting(alternateId, stance, handle)
                        && !manager.AddExisting(id, other, handle)
                        && !manager.AddExisting(id, stance, 0)
                        && !manager.AddExisting(id, stance, replacementHandle)
                        && !manager.AddExisting(alternateId, other, handle);
                    if (!rejected || manager.Contains(alternateId) || manager.Contains(other))
                        return Failed("Conflicting registration was accepted or changed the registry.");
                }
            }
        }
        manager.TryGetHandle(stance, out handle);
        bool sameString = manager.TryGetObject<StanceLink>(id, out var byString) && ReferenceEquals(stance, byString);
        bool sameNumeric = manager.TryGetObject<StanceLink>(handle, out var byHandle) && ReferenceEquals(stance, byHandle);
        bool samePrepared = ReferenceEquals(preparedStance, stance);
        if (!sameString || (handle != 0 && !sameNumeric)) return Failed("Registry lookup changed the stance instance.");
        return Passed($"STANCE_HANDLE_STATE action={action} id={id} handle={handle} sameString={sameString} sameNumeric={sameNumeric} samePrepared={samePrepared} received={Volatile.Read(ref receivedMessages)}");
    }

    internal static void RemoveHandleForSetup(IObjectManager manager, object stance, uint handle)
    {
        if (ModInformation.IsServer) throw new InvalidOperationException("Missing-handle setup requires a client.");
        // Only DEBUG setup accesses our non-public indexes; production Remove preserves retired handles.
        var type = typeof(global::GameInterface.Services.ObjectManager.ObjectManager);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gate = type.GetField("_gate", flags).GetValue(manager);
        var forward = (ConcurrentDictionary<uint, object>)type.GetField("handleObjs", flags).GetValue(manager);
        var reverse = type.GetField("objsHandles", flags).GetValue(manager);
        lock (gate)
        {
            if (handle == 0 || !manager.TryGetHandle(stance, out var actual) || actual != handle
                || !forward.TryGetValue(handle, out var mapped) || !ReferenceEquals(mapped, stance))
                throw new InvalidOperationException("Setup handle does not identify the existing stance.");
            if (!(bool)reverse.GetType().GetMethod("Remove").Invoke(reverse, new[] { stance }))
                throw new InvalidOperationException("Reverse handle removal failed.");
            if (!forward.TryRemove(handle, out _)) throw new InvalidOperationException("Forward handle removal failed.");
        }
    }

    [HarmonyPatch(typeof(StanceLinkHandler), "HandleStanceLinkConstructed")]
    private static class ReceiveObserver
    {
        [HarmonyPostfix]
        private static void Postfix(MessagePayload<StanceLinkConstructed> payload)
        {
            // The production handler has queued its apply before this receipt is observed.
            if (ModInformation.IsClient && preparedStance != null && payload.What.StanceLinkHandle == preparedHandle)
                Interlocked.Increment(ref receivedMessages);
        }
    }

    private static CoopCommandResult Passed(string output) => new CoopCommandResult(true, output);
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
