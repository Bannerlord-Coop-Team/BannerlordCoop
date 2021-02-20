using System.Collections.Generic;
using System.Linq;
using Sync.Call;
using Sync.Value;

namespace Sync.Behaviour
{
    public static class Util
    {
        public static Dictionary<InvokableId, List<CallBehaviourBuilder>> SortByMethod(
            List<ActionBehaviourBuilder> allBehaviours)
        {
            var applicableBehaviours =
                new Dictionary<InvokableId, List<CallBehaviourBuilder>>();

            var patchedMethods = allBehaviours
                .SelectMany(b => b.CallBehaviours.Keys).Distinct();
            foreach (var patchedMethod in patchedMethods)
            {
                var callBehaviours = allBehaviours
                    .Where(a => a.CallBehaviours.ContainsKey(patchedMethod))
                    .Select(a => a.CallBehaviours[patchedMethod]);
                applicableBehaviours[patchedMethod] = callBehaviours.ToList();
            }

            return applicableBehaviours;
        }

        public static Dictionary<FieldId, List<FieldActionBehaviourBuilder>> SortByField(
            List<ActionBehaviourBuilder> allBehaviours)
        {
            var applicableBehaviours =
                new Dictionary<FieldId, List<FieldActionBehaviourBuilder>>();

            var patchedFields = allBehaviours
                .SelectMany(b => b.FieldChangeAction.Keys).Distinct();
            foreach (var patchedField in patchedFields)
            {
                var fieldBehaviours = allBehaviours
                    .Where(a => a.FieldChangeAction.ContainsKey(patchedField))
                    .Select(a => a.FieldChangeAction[patchedField]);
                applicableBehaviours[patchedField] = fieldBehaviours.ToList();
            }

            return applicableBehaviours;
        }
    }
}