#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

public interface INavalLabSessionStore
{
    NavalLabManifest Current { get; }
    bool Contains(string instanceId);
    bool IsParticipant(string instanceId, string controllerId);
    void Install(NavalLabManifest manifest);
    bool BeginOperation(Guid id, string contents);
    bool BeginEmergencyOperation(Guid id, string kind);
    void RecordReceipt(Guid id, string controller, string status);
    object InspectOperation(Guid id);
    bool TryInspectOperation(Guid id, string contents, out object receipt);
    string CampaignWriteBlocker { get; }
    void RejectCampaignWrite(string reason);
}

// One server/client session owns this bounded in-memory fixture store.
public sealed class NavalLabSessionStore : INavalLabSessionStore
{
    private sealed class Operation
    {
        public string Contents;
        public DateTime Deadline;
        public readonly Dictionary<string, string> Receipts = new Dictionary<string, string>();
    }
    private readonly Dictionary<Guid, Operation> operations = new Dictionary<Guid, Operation>();
    private readonly Dictionary<string, Guid> emergencyIds = new Dictionary<string, Guid>();
    private volatile NavalLabManifest current;
    private string campaignWriteBlocker;
    public NavalLabManifest Current => current;
    public string CampaignWriteBlocker => System.Threading.Volatile.Read(ref campaignWriteBlocker);
    public void RejectCampaignWrite(string reason) =>
        System.Threading.Interlocked.CompareExchange(ref campaignWriteBlocker, reason, null);
    public bool Contains(string instanceId) => Current != null && Current.InstanceId == instanceId;
    public bool IsParticipant(string instanceId, string controllerId) => Contains(instanceId)
        && Current.Controllers.Contains(controllerId);
    public void Install(NavalLabManifest manifest)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (Current != null && (Current.Mode != manifest.Mode || Current.IncarnationId != manifest.IncarnationId
            || !Current.Controllers.SequenceEqual(manifest.Controllers)
            || !Current.Combatants.SequenceEqual(manifest.Combatants)
            || !Current.Ships.SequenceEqual(manifest.Ships)))
            throw new InvalidOperationException("The initial physics probe cannot replace its immutable manifest.");
        current = manifest;
    }
    public bool BeginOperation(Guid id, string contents)
    {
        if (id == Guid.Empty) throw new ArgumentException("An operation id is required.", nameof(id));
        if (operations.TryGetValue(id, out var existing))
        {
            if (existing.Contents != contents) throw new InvalidOperationException("Conflicting operation id.");
            return false;
        }
        if (operations.Count - emergencyIds.Count >= 64) throw new InvalidOperationException("The lab operation budget is exhausted.");
        operations.Add(id, new Operation { Contents = contents, Deadline = DateTime.UtcNow.AddSeconds(30) });
        return true;
    }
    public bool BeginEmergencyOperation(Guid id, string kind)
    {
        if (id == Guid.Empty) throw new ArgumentException("An operation id is required.", nameof(id));
        if (kind != "hold" && kind != "stop") throw new ArgumentException("Not an emergency action.", nameof(kind));
        if (emergencyIds.TryGetValue(kind, out var existing))
        {
            if (existing != id) throw new InvalidOperationException("Emergency action already allocated; query its receipt.");
            return false;
        }
        if (operations.ContainsKey(id)) throw new InvalidOperationException("Conflicting operation id.");
        emergencyIds.Add(kind, id);
        operations.Add(id, new Operation { Contents = kind, Deadline = DateTime.UtcNow.AddSeconds(30) });
        return true;
    }

    public void RecordReceipt(Guid id, string controller, string status)
    {
        if (!operations.TryGetValue(id, out var operation)) return;
        if (operation.Receipts.TryGetValue(controller, out var existing) && existing != status)
            throw new InvalidOperationException("Conflicting terminal receipt.");
        operation.Receipts[controller] = status;
    }
    public bool TryInspectOperation(Guid id, string contents, out object receipt)
    {
        receipt = null;
        if (!operations.TryGetValue(id, out var operation)) return false;
        if (operation.Contents != contents) throw new InvalidOperationException("Conflicting operation id.");
        receipt = InspectOperation(id);
        return true;
    }

    public object InspectOperation(Guid id)
    {
        if (!operations.TryGetValue(id, out var operation)) return new { status = "unknown" };
        return new
        {
            operationId = id,
            contents = operation.Contents,
            expired = DateTime.UtcNow > operation.Deadline,
            receipts = operation.Receipts.ToArray()
        };
    }
}
#endif
