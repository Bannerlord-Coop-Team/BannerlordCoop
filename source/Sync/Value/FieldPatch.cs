using Sync.Behaviour;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sync.Value
{
    // TODO add documentation
    public readonly struct FieldPatch
    {
        static int _nextId = 0;

        public static ConcurrentDictionary<int, FieldPatch> FieldPatches { get; } =
            new ConcurrentDictionary<int, FieldPatch>();

        public delegate EFieldChangeAction ChangeAllowedDelegate();

        public readonly int Id;
        public readonly ChangeAllowedDelegate ChangeAllowed;
        public readonly FieldInfo Field;

        public FieldPatch(FieldInfo field, ChangeAllowedDelegate changeAllowedDelegate)
        {
            ChangeAllowed = changeAllowedDelegate;
            Field = field;

            Id = Interlocked.Increment(ref _nextId);
            FieldPatches.TryAdd(Id, this);
        }
    }
}
