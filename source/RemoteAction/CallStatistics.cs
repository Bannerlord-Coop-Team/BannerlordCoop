using System.Diagnostics;
using System.Linq;
using Common;
using JetBrains.Annotations;
using RailgunNet.System.Types;
using Sync;
using Sync.Call;
using Sync.Value;

namespace RemoteAction
{
    public struct CallTrace
    {
        public FieldId? Value { get; set; }
        public InvokableId? Call { get; set; }
        [CanBeNull] public object Instance { get; set; }
        [CanBeNull] public object[] Arguments { get; set; }
        public Tick Tick { get; set; }
    }

    public class CallStatistics : DropoutStack<CallTrace>
    {
        public CallStatistics(int capacity) : base(capacity)
        {
        }

        [Conditional("DEBUG")]
        public void Push(MethodCall call, Tick tick)
        {
            Push(new CallTrace
            {
                Call = call.Id,
                Instance = call.Instance,
                Arguments = call.Arguments.Select(a => (object) a).ToArray(),
                Tick = tick
            });
        }

        [Conditional("DEBUG")]
        public void Push(FieldChange change, Tick tick)
        {
            Push(new CallTrace
            {
                Value = change.Id,
                Instance = change.Instance,
                Arguments = change.Arguments.Select(a => (object) a).ToArray(),
                Tick = tick
            });
        }
    }
}