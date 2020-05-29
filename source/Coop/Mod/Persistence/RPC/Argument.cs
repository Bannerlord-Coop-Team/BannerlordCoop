using System;
using JetBrains.Annotations;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    public enum EventArgType
    {
        Null,
        EntityReference,
        MBGUID
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

        public Argument([NotNull] RailEntityBase entity) : this(entity.Id)
        {
        }

        public Argument(EntityId id)
        {
            if (id == EntityId.INVALID)
            {
                throw new Exception("Invalid entity. Cannot reference it in an event argument.");
            }

            EventType = EventArgType.EntityReference;
            RailId = id;
            MbGUID = null;
        }

        public Argument(MBGUID guid)
        {
            EventType = EventArgType.MBGUID;
            MbGUID = guid;
            RailId = null;
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
