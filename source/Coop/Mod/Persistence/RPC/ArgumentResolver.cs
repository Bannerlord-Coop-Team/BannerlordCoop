using System;
using System.Collections.Generic;
using System.Linq;
using RailgunNet.Connection.Client;
using RailgunNet.Logic;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    public static class ArgumentResolver
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
    }
}
