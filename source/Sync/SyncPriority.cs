using HarmonyLib;

namespace Sync
{
    public class SyncPriority
    {
        public const int SyncCallPost = SyncValuePost - 1;
        public const int SyncValuePost = Priority.Last - 2;
        public const int SyncValuePre = Priority.First + 1;
        public const int SyncCallPre = SyncValuePre + 1;
        public const int SyncCallPreUserPatch = SyncCallPre + 1;
    }
}
