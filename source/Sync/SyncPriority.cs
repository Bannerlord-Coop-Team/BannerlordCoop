using HarmonyLib;

namespace Sync
{
    public class SyncPriority
    {
        public const int Last = Priority.Last - 2;
        public const int First = Priority.First + 1;
    }
}
