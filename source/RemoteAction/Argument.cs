using System;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace RemoteAction
{
    /// <summary>
    ///     Wrapper for an argument used in a remote action.
    /// 
    ///     ATTENTION: The used state transfer library, Railgun, is intended to reliably distribute
    ///     very small amount of data that is to be applied at a synchronized point in time on all clients.
    ///     Maximum payload data in a single event is tiny <see cref="RailgunNet.RailConfig.MAXSIZE_EVENT" />.
    ///     Larger objects need be transferred using a <see cref="Sync.Store.RemoteStore" /> and then referenced
    ///     in a <see cref="EventArgType.StoreObjectId" />. The <see cref="ArgumentFactory.Create" />
    ///     can take care of this.
    /// 
    ///     To add a new argument type:
    ///     1. Add enum entry
    ///     2. Extended <see cref="Argument" /> to store the new type in some way
    ///     3. Implement <see cref="ArgumentFactory.Resolve" />
    ///     4. Implement <see cref="ArgumentFactory.Create" />
    ///     5. Implement encoder & decoder in <see cref="ArgumentSerializer" />
    ///     6. Add case for the new type in <see cref="Argument.ToString" />
    ///     7. Add new argument type to hash <see cref="Argument.GetHashCode" />
    /// </summary>
    public enum EventArgType
    {
        Null,
        MBObjectManager,
        MBObject,
        Int,
        Float,
        StoreObjectId,
        CurrentCampaign
    }

    /// <summary>
    ///     Type union for arguments to a RPC.
    /// </summary>
    public readonly struct Argument
    {
        public static Argument Null = new Argument(EventArgType.Null);

        public static Argument CurrentCampaign = new Argument(EventArgType.CurrentCampaign);

        public static Argument MBObjectManager = new Argument(EventArgType.MBObjectManager);

        private Argument(EventArgType eType) : this()
        {
            EventType = eType;
        }

        public EventArgType EventType { get; }
        public MBGUID? MbGUID { get; }

        public int? Int { get; }
        public float? Float { get; }
        public ObjectId? StoreObjectId { get; }

        public Argument(int i) : this()
        {
            EventType = EventArgType.Int;
            Int = i;
        }

        public Argument(float f) : this()
        {
            EventType = EventArgType.Float;
            Float = f;
        }

        public Argument(MBGUID guid) : this()
        {
            EventType = EventArgType.MBObject;
            MbGUID = guid;
        }

        public Argument(ObjectId id) : this()
        {
            EventType = EventArgType.StoreObjectId;
            StoreObjectId = id;
        }

        public override int GetHashCode()
        {
            int hash = (int) EventType;
            int? argHash = null;
            switch (EventType)
            {
                case EventArgType.Null:
                    break;
                case EventArgType.MBObjectManager:
                    break;
                case EventArgType.MBObject:
                    argHash = MbGUID.Value.GetHashCode();
                    break;
                case EventArgType.Int:
                    argHash = Int.Value.GetHashCode();
                    break;
                case EventArgType.Float:
                    argHash = Float.Value.GetHashCode();
                    break;
                case EventArgType.StoreObjectId:
                    argHash = StoreObjectId.Value.GetHashCode();
                    break;
                case EventArgType.CurrentCampaign:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (argHash.HasValue)
            {
                hash = (hash * 397) ^ argHash.Value;
            }
            
            return hash;
        }

        public override string ToString()
        {
            switch (EventType)
            {
                case EventArgType.Null:
                    return "null";
                case EventArgType.MBObjectManager:
                    return "MBObjectManager";
                case EventArgType.MBObject:
                    object obj = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject(MbGUID.Value);
                    if (obj is MobileParty party)
                    {
                        return String.Format(
                            "\"{0, 4}:{1}\"",
                            party.Party.Index,
                            party.Party.Name.ToString());
                    }
                    return $"\"{obj}\"";
                case EventArgType.Int:
                    return Int.ToString();
                case EventArgType.Float:
                    return Float.ToString();
                case EventArgType.StoreObjectId:
                    return $"{StoreObjectId.ToString()}";
                case EventArgType.CurrentCampaign:
                    return "Campaign.Current";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
    }
}
