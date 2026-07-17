using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface INetworkWorldItemRegistry
{
    Guid GetOrCreateId(SpawnedItemEntity item);
    void Register(Guid itemId, SpawnedItemEntity item);
    bool TryGet(Guid itemId, out SpawnedItemEntity item);
    IReadOnlyDictionary<Guid, SpawnedItemEntity> GetAll();
    void Remove(Guid itemId);
    void Clear();
}

public sealed class NetworkWorldItemRegistry : INetworkWorldItemRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, SpawnedItemEntity> idToItem = new();
    private readonly Dictionary<SpawnedItemEntity, Guid> itemToId = new();

    public Guid GetOrCreateId(SpawnedItemEntity item)
    {
        if (item == null) return Guid.Empty;
        lock (gate)
        {
            if (itemToId.TryGetValue(item, out Guid existing)) return existing;
            Guid itemId = item.Id.CreatedAtRuntime ? Guid.NewGuid() : FromSceneId(item.Id.Id);
            RegisterUnsafe(itemId, item);
            return itemId;
        }
    }

    public void Register(Guid itemId, SpawnedItemEntity item)
    {
        if (itemId == Guid.Empty || item == null) return;
        lock (gate)
        {
            RegisterUnsafe(itemId, item);
        }
    }

    public bool TryGet(Guid itemId, out SpawnedItemEntity item)
    {
        lock (gate)
        {
            return idToItem.TryGetValue(itemId, out item);
        }
    }

    public IReadOnlyDictionary<Guid, SpawnedItemEntity> GetAll()
    {
        lock (gate)
        {
            return new Dictionary<Guid, SpawnedItemEntity>(idToItem);
        }
    }

    public void Remove(Guid itemId)
    {
        lock (gate)
        {
            if (!idToItem.TryGetValue(itemId, out var item)) return;
            idToItem.Remove(itemId);
            itemToId.Remove(item);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            idToItem.Clear();
            itemToId.Clear();
        }
    }

    private void RegisterUnsafe(Guid itemId, SpawnedItemEntity item)
    {
        if (idToItem.TryGetValue(itemId, out var previous))
            itemToId.Remove(previous);
        if (itemToId.TryGetValue(item, out Guid previousId))
            idToItem.Remove(previousId);
        idToItem[itemId] = item;
        itemToId[item] = itemId;
    }

    private static Guid FromSceneId(int missionObjectId)
    {
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(missionObjectId).CopyTo(bytes, 0);
        bytes[15] = 1;
        return new Guid(bytes);
    }
}
