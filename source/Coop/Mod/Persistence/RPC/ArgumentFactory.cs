using System;
using System.Collections.Generic;
using System.Linq;
using RailgunNet.Connection.Client;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using Sync.Store;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    public static class ArgumentFactory
    {
        public static object Resolve(this RailClientRoom room, IStore store, Argument arg)
        {
            switch (arg.EventType)
            {
                case EventArgType.Null:
                    return null;
                case EventArgType.EntityReference:
                    if (room.TryGet(arg.RailId.Value, out RailEntityClient entity))
                    {
                        return entity;
                    }

                    return null;
                case EventArgType.MBGUID:
                    return MBObjectManager.Instance.GetObject(arg.MbGUID.Value);
                case EventArgType.Int:
                    return arg.Int.Value;
                case EventArgType.StoreObjectId:
                    if (store == null)
                    {
                        throw new ArgumentException($"Cannot resolve ${arg}, no store provided.");
                    }

                    if (!arg.StoreObjectId.HasValue ||
                        !store.Data.ContainsKey(arg.StoreObjectId.Value))
                    {
                        throw new Exception($"Cannot resolve ${arg}.");
                    }

                    return store.Data[arg.StoreObjectId.Value];
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static object[] Resolve(this RailClientRoom room, IStore store, List<Argument> args)
        {
            return args.Select(arg => room.Resolve(store, arg)).ToArray();
        }

        public static Argument Create(IStore store, object obj)
        {
            switch (obj)
            {
                case null:
                    return Argument.Null;
                default:
                    return new Argument(store.Insert(obj));
            }
        }

        public static Argument Create(object obj)
        {
            switch (obj)
            {
                case null:
                    return Argument.Null;
                case MBGUID guid:
                    return new Argument(guid);
                case RailEntityBase entity:
                    return new Argument(entity);
                case EntityId entityId:
                    return new Argument(entityId);
                case MBObjectBase mbobj:
                    return new Argument(mbobj.Id);
                case int i:
                    return new Argument(i);
                default:
                    throw new Exception($"Unknown argument type: {obj}.");
            }
        }
    }
}
