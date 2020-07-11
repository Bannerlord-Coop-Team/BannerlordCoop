using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RailgunNet.Logic;
using Sync.Store;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Factory to create the transfer wrapper for an argument in a RPC call.
    /// </summary>
    public static class ArgumentFactory
    {
        /// <summary>
        ///     Resolves the transferred RPC argument to be used in the local function call.
        /// </summary>
        /// <param name="store">Clients remote store instance.</param>
        /// <param name="arg">Argument to be resolved.</param>
        /// <returns>The unwrapped argument.</returns>
        /// <exception cref="ArgumentException">
        ///     If the argument references an object in the store,
        ///     but the reference cannot be resolved.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">If the argument type is unknown.</exception>
        public static object Resolve(IStore store, Argument arg)
        {
            switch (arg.EventType)
            {
                case EventArgType.Null:
                    return null;
                case EventArgType.MBObject:
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
                        throw new ArgumentException($"Cannot resolve ${arg}.");
                    }

                    object resolvedObject = store.Data[arg.StoreObjectId.Value];
                    store.Remove(arg.StoreObjectId.Value);
                    return resolvedObject;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        ///     Resolves a list of transferred RPC arguments to be used in the local function call.
        /// </summary>
        /// <param name="room">Rail room for the local client.</param>
        /// <param name="store">Clients remote store instance.</param>
        /// <param name="args">Argument to be resolved.</param>
        /// <returns>A list of the unwrapped arguments.</returns>
        public static object[] Resolve(IStore store, List<Argument> args)
        {
            return args.Select(arg => Resolve(store, arg)).ToArray();
        }

        /// <summary>
        ///     Creates a RPC transfer wrapper for a function call argument.
        /// </summary>
        /// <param name="store">Clients remote store instance.</param>
        /// <param name="obj">The object to be wrapped.</param>
        /// <param name="bTransferByValue">
        ///     If true, the object will always be transferred by
        ///     value. If the argument is too large to fit in an <see cref="RailEvent" />,
        ///     the argument will be shared to all receivers using the <paramref name="store" />.
        ///     If false, the argument may be, depending on the type, transferred by reference.
        /// </param>
        /// <returns>The wrapped argument.</returns>
        public static Argument Create(
            [NotNull] IStore store,
            [CanBeNull] object obj,
            bool bTransferByValue)
        {
            switch (obj)
            {
                case null:
                    return Argument.Null;
                case MBGUID guid:
                    return new Argument(guid);
                case int i:
                    return new Argument(i);
                case MBObjectBase mbobj:
                    if (bTransferByValue)
                    {
                        goto default;
                    }

                    return new Argument(mbobj.Id);
                default:
                    return new Argument(store.Insert(obj));
            }
        }
    }
}
