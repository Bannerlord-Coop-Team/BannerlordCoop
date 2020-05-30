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
            if (obj == null)
            {
                return Argument.Null;
            }

            if (obj is MBGUID guid)
            {
                return new Argument(guid);
            }

            if (obj is RailEntityBase entity)
            {
                return new Argument(entity);
            }

            if (obj is EntityId entityId)
            {
                return new Argument(entityId);
            }

            if (obj is MBObjectBase mbobj)
            {
                return new Argument(mbobj.Id);
            }

            throw new Exception($"Unknown argument type: {obj}.");
        }
    }
}
