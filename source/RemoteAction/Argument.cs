using System;
using System.Linq;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using System.Reflection;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using System.Collections.Generic;

namespace RemoteAction
{
    /// <summary>
    ///     Wrapper for an argument used in a remote action.
    ///     ATTENTION: The used state transfer library, Railgun, is intended to reliably distribute
    ///     very small amount of data that is to be applied at a synchronized point in time on all clients.
    ///     Maximum payload data in a single event is tiny <see cref="RailgunNet.RailConfig.MAXSIZE_EVENT" />.
    ///     Larger objects need be transferred using a <see cref="Sync.Store.RemoteStoreClient" /> and then referenced
    ///     in a <see cref="EventArgType.StoreObjectId" />. The <see cref="ArgumentFactory.Create" />
    ///     can take care of this.
    ///     To add a new argument type:
    ///     1. Add enum entry
    ///     2. Extended <see cref="Argument" /> to store the new type in some way
    ///     3. Implement <see cref="Argument.ToString"/>
    ///     4. Implement <see cref="ArgumentFactory.Resolve" />
    ///     5. Implement <see cref="ArgumentFactory.Create" />
    ///     6. Implement encoder & decoder in <see cref="ArgumentSerializer" />
    ///     7. Add case for the new type in <see cref="Argument.ToString" />
    ///     8. Add new argument type to hash <see cref="Argument.GetHashCode" />
    /// </summary>
    public enum EventArgType
    {
        Null,
        MBObjectManager,
        CoopObjectManagerId,
        Guid,
        Int,
        Float,
        Bool,
        StoreObjectId,
        CurrentCampaign,
        CampaignBehavior,
        PartyComponent,
        SmallObjectRaw
    }

    /// <summary>
    ///     Type union for arguments to a RPC.
    /// </summary>
    public readonly struct Argument
    {
        /// <summary>
        ///     Argument instance representing a null value
        /// </summary>
        public static Argument Null = new Argument(EventArgType.Null);
        /// <summary>
        ///     Argument instance representing <see cref="TaleWorlds.CampaignSystem.Campaign.Current"/>.
        /// </summary>
        public static Argument CurrentCampaign = new Argument(EventArgType.CurrentCampaign);
        /// <summary>
        ///     Argument instance representing the singleton <see cref="TaleWorlds.ObjectSystem.MBObjectManager.Instance"/>.
        /// </summary>
        public static Argument MBObjectManager = new Argument(EventArgType.MBObjectManager);
        /// <summary>
        ///     The type of argument.
        /// </summary>
        public EventArgType EventType { get; }
        /// <summary>
        ///     An object reference in the <see cref="Common.CoopObjectManager"/>.
        /// </summary>
        public Guid? CoopObjectManagerId { get; }
        /// <summary>
        ///     A GUID. Not necessarily for any specfic use, transfered by value.
        /// </summary>
        public Guid? Guid { get; }
        /// <summary>
        ///     The contained <see cref="int"/> for <see cref="EventArgType.Int"/>, otherwise null. 
        /// </summary>
        public int? Int { get; }
        /// <summary>
        ///     The contained <see cref="float"/> for <see cref="EventArgType.Float"/>, otherwise null. 
        /// </summary>
        public float? Float { get; }
        /// <summary>
        ///     The contained <see cref="float"/> for <see cref="EventArgType.Bool"/>, otherwise null. 
        /// </summary>
        public bool? Bool { get; }

        /// <summary>
        ///     The contained <see cref="ObjectId"/> for <see cref="EventArgType.StoreObjectId"/>, otherwise null. 
        /// </summary>
        public ObjectId? StoreObjectId { get; }
        /// <summary>
        ///     The contained <see cref="CampaignBehaviorBase"/> for <see cref="EventArgType.CampaignBehavior"/>. 
        /// </summary>
        public CampaignBehaviorBase CampaignBehavior { get; }
        /// <summary>
        ///     The contained <see cref="PartyComponent"/> for <see cref="EventArgType.PartyComponent"/>. 
        /// </summary>
        public PartyComponent MobilePartyComponent { get; }
        /// <summary>
        ///     The contained byte buffer for <see cref="EventArgType.SmallObjectRaw"/>, otherwise null. 
        /// </summary>
        public byte[] Raw { get; }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.Int"/>.
        /// </summary>
        public Argument(int i) : this()
        {
            EventType = EventArgType.Int;
            Int = i;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.Float"/>.
        /// </summary>
        public Argument(float f) : this()
        {
            EventType = EventArgType.Float;
            Float = f;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.Bool"/>.
        /// </summary>
        public Argument(bool b) : this()
        {
            EventType = EventArgType.Bool;
            Bool = b;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.CoopObjectManagerId"/>.
        /// </summary>
        public Argument(Guid guid, bool isCoopObjectReference) : this()
        {
            if(isCoopObjectReference)
            {
                EventType = EventArgType.CoopObjectManagerId;
                CoopObjectManagerId = guid;
            }
            else
            {
                EventType = EventArgType.Guid;
                Guid = guid;
            }
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.StoreObjectId"/>.
        /// </summary>
        public Argument(ObjectId id) : this()
        {
            EventType = EventArgType.StoreObjectId;
            StoreObjectId = id;
        }

        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.CampaignBehavior"/>.
        /// </summary>
        public Argument(CampaignBehaviorBase campaignBehavior) : this()
        {
            EventType = EventArgType.CampaignBehavior;
            CampaignBehavior = campaignBehavior;
        }

        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.PartyComponent"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(PartyComponent component) : this()
        {
            EventType = EventArgType.PartyComponent;
            MobilePartyComponent = component;
        }

        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.SmallObjectRaw"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(byte[] raw) : this()
        {
            EventType = EventArgType.SmallObjectRaw;
            Raw = raw;
        }
        /// <summary>
        ///     Computes the hash of this arguments value.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override int GetHashCode()
        {
            var hash = (int) EventType;
            int? argHash = null;
            switch (EventType)
            {
                case EventArgType.Null:
                    break;
                case EventArgType.MBObjectManager:
                    break;
                case EventArgType.CoopObjectManagerId:
                    argHash = CoopObjectManagerId.Value.GetHashCode();
                    break;
                case EventArgType.Guid:
                    argHash = Guid.Value.GetHashCode();
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
                case EventArgType.SmallObjectRaw:
                    argHash = Raw.GetHashCode();
                    break;
                case EventArgType.Bool:
                    argHash = Bool.GetHashCode();
                    break;
                case EventArgType.CampaignBehavior:
                    argHash = CampaignBehavior.GetHashCode();
                    break;
                case EventArgType.PartyComponent:
                    argHash = MobilePartyComponent.GetHashCode();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (argHash.HasValue) hash = (hash * 397) ^ argHash.Value;

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
                case EventArgType.CoopObjectManagerId:
                    object obj = Common.CoopObjectManager.GetObject(CoopObjectManagerId.Value);
                    if (obj is MobileParty party)
                        return string.Format(
                            "\"{0, 4}:{1}\"",
                            party.Party.Index,
                            party.Party.Name);
                    return $"\"{obj}\"";
                case EventArgType.Guid:
                    return Guid.ToString();
                case EventArgType.Int:
                    return Int.ToString();
                case EventArgType.Float:
                    return Float.ToString();
                case EventArgType.Bool:
                    return Bool.ToString();
                case EventArgType.StoreObjectId:
                    return $"StoreObjectId: {StoreObjectId.ToString()}";
                case EventArgType.CurrentCampaign:
                    return "Campaign.Current";
                case EventArgType.SmallObjectRaw:
                    return "Raw byte array";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        #region Private
        private Argument(EventArgType eType) : this()
        {
            EventType = eType;
        }
        #endregion
    }
}