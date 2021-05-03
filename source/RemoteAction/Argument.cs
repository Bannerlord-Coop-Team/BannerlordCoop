﻿using System;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace RemoteAction
{
    /// <summary>
    ///     Wrapper for an argument used in a remote action.
    ///     ATTENTION: The used state transfer library, Railgun, is intended to reliably distribute
    ///     very small amount of data that is to be applied at a synchronized point in time on all clients.
    ///     Maximum payload data in a single event is tiny <see cref="RailgunNet.RailConfig.MAXSIZE_EVENT" />.
    ///     Larger objects need be transferred using a <see cref="Sync.Store.RemoteStore" /> and then referenced
    ///     in a <see cref="EventArgType.StoreObjectId" />. The <see cref="ArgumentFactory.Create" />
    ///     can take care of this.
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
        CurrentCampaign,
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
        ///     The contained <see cref="MBGUID"/> for <see cref="EventArgType.MBObject"/>, otherwise null. 
        /// </summary>
        public MBGUID? MbGUID { get; }
        /// <summary>
        ///     The contained <see cref="int"/> for <see cref="EventArgType.Int"/>, otherwise null. 
        /// </summary>
        public int? Int { get; }
        /// <summary>
        ///     The contained <see cref="float"/> for <see cref="EventArgType.Float"/>, otherwise null. 
        /// </summary>
        public float? Float { get; }
        /// <summary>
        ///     The contained <see cref="ObjectId"/> for <see cref="EventArgType.StoreObjectId"/>, otherwise null. 
        /// </summary>
        public ObjectId? StoreObjectId { get; }
        /// <summary>
        ///     The contained byte buffer for <see cref="EventArgType.SmallObjectRaw"/>, otherwise null. 
        /// </summary>
        public byte[] Raw { get; }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.Int"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(int i) : this()
        {
            EventType = EventArgType.Int;
            Int = i;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.Float"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(float f) : this()
        {
            EventType = EventArgType.Float;
            Float = f;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.MBObject"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(MBGUID guid) : this()
        {
            EventType = EventArgType.MBObject;
            MbGUID = guid;
        }
        /// <summary>
        ///     Constructs a new argument for <see cref="EventArgType.StoreObjectId"/>.
        /// </summary>
        /// <param name="i"></param>
        public Argument(ObjectId id) : this()
        {
            EventType = EventArgType.StoreObjectId;
            StoreObjectId = id;
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
                case EventArgType.SmallObjectRaw:
                    argHash = Raw.GetHashCode();
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
                case EventArgType.MBObject:
                    object obj = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject(MbGUID.Value);
                    if (obj is MobileParty party)
                        return string.Format(
                            "\"{0, 4}:{1}\"",
                            party.Party.Index,
                            party.Party.Name);
                    return $"\"{obj}\"";
                case EventArgType.Int:
                    return Int.ToString();
                case EventArgType.Float:
                    return Float.ToString();
                case EventArgType.StoreObjectId:
                    return $"{StoreObjectId.ToString()}";
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