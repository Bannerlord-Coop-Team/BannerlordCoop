namespace Sync
{
    public enum EPatchBehaviour
    {
        AlwaysCallOriginal, // The patch always calls the original. Return value of the dispatcher is ignored.
        NeverCallOriginal, // The patch never calls the original. Return value of the dispatcher is ignored.
        CallOriginalBaseOnDispatcherReturn // The patch calls the original if the dispatcher function returns true. The dispatcher function needs to return a boolean.
    }
}
