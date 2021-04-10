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
    /// <summary>
    ///     Trace data for a method call or a field change.
    /// </summary>
    public struct CallTrace
    {
        /// <summary>
        ///     The id of the field, if a field was changed. Otherwise null.
        /// </summary>
        public FieldId? Value { get; set; }
        /// <summary>
        ///     The id of the call, if a call was made. Otherwise null.
        /// </summary>
        public InvokableId? Call { get; set; }
        /// <summary>
        ///     The instance the call was made on / the field was changed on. Null for static.
        /// </summary>
        [CanBeNull] public object Instance { get; set; }
        /// <summary>
        ///     The arguments to the method call. Null for field changes.
        /// </summary>
        [CanBeNull] public object[] Arguments { get; set; }
        /// <summary>
        ///     The rooms tick the call was made on.
        /// </summary>
        public Tick Tick { get; set; }
    }
    /// <summary>
    ///     Rolling buffer for call trace events. Only active in debug mode.
    /// </summary>
    public class CallStatistics : DropoutStack<CallTrace>
    {
        public CallStatistics(int capacity) : base(capacity)
        {
        }
        /// <summary>
        ///     Creates a new trace for a method call.
        /// </summary>
        /// <param name="call"></param>
        /// <param name="tick"></param>
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
        /// <summary>
        ///     Create a new trace for a field change.
        /// </summary>
        /// <param name="change"></param>
        /// <param name="tick"></param>
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