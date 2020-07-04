using System;
using JetBrains.Annotations;
using Network.Protocol;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using Sync.Store;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Type of an argument used in a <see cref="EventMethodCall" />.
    ///     ATTENTION: The persistence is intended to reliably transfer very small amount of data
    ///     that is to be applied at a synchronized point in time on all client. Maximum payload
    ///     data in a single event is tiny <see cref="RailgunNet.RailConfig.MAXSIZE_EVENT" />.
    ///     Instead of sending data, only send a reference to it in the already synchronized
    ///     game state. To transfer new game state use a <see cref="EPacket.Sync" /> message.
    ///     To add a new argument type:
    ///     1. Add enum entry
    ///     2. Extended <see cref="Argument" /> to store the new type in some way
    ///     3. Implement <see cref="ArgumentFactory.Resolve" />
    ///     4. Implement <see cref="ArgumentFactory.Create" />
    ///     5. Implement encoder & decoder in <see cref="ArgumentSerializer" />
    ///     6. Add case for the new type in <see cref="Argument.ToString" />
    /// </summary>
    public enum EventArgType
    {
        Null,
        EntityReference,
        MBGUID,
        Int,
        StoreObjectId
    }

    public struct Argument
    {
        public static Argument Null = new Argument
        {
            EventType = EventArgType.Null
        };

        public EventArgType EventType { get; private set; }
        public EntityId? RailId { get; }
        public MBGUID? MbGUID { get; }

        public int? Int { get; }
        public ObjectId? StoreObjectId { get; }

        public Argument(int i) : this()
        {
            EventType = EventArgType.Int;
            Int = i;
        }

        public Argument([NotNull] RailEntityBase entity) : this(entity.Id)
        {
        }

        public Argument(EntityId id) : this()
        {
            if (id == EntityId.INVALID)
            {
                throw new Exception("Invalid entity. Cannot reference it in an event argument.");
            }

            EventType = EventArgType.EntityReference;
            RailId = id;
        }

        public Argument(MBGUID guid) : this()
        {
            EventType = EventArgType.MBGUID;
            MbGUID = guid;
        }

        public Argument(ObjectId id) : this()
        {
            EventType = EventArgType.StoreObjectId;
            StoreObjectId = id;
        }

        public override string ToString()
        {
            switch (EventType)
            {
                case EventArgType.Null:
                    return "null";
                case EventArgType.EntityReference:
                    return RailId.ToString();
                case EventArgType.MBGUID:
                    return $"MBGUID {MbGUID}";
                case EventArgType.Int:
                    return Int.ToString();
                case EventArgType.StoreObjectId:
                    return $"{StoreObjectId.ToString()}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
