namespace Common;

// Executes the linked dispatcher without loading Bannerlord or native DLLs.
public static class GameThread
{
    public static void Run(Action action, bool blocking) => action();
}
