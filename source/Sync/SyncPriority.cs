using HarmonyLib;

namespace Sync
{
    public static class SyncPriority
    {
        public const int FieldWatcherPost = Priority.Last - 100;
        public const int MethodPatchGenerated = Priority.First;
        public const int FieldWatcherPre = Priority.First + 100;
    }
}