using System;
using System.Collections.Generic;
using System.Linq;
using RailgunNet.Connection.Client;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    public static class ArgumentFactory
    {
        public static object Resolve(this RailClientRoom room, Argument arg)
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static object[] Resolve(this RailClientRoom room, List<Argument> args)
        {
            return args.Select(arg => room.Resolve(arg)).ToArray();
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
