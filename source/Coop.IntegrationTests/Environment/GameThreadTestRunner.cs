using Common;

namespace Coop.IntegrationTests.Environment;

internal static class GameThreadTestRunner
{
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                GameThread.Instance.MarkGameThread();
                action();
            }
            catch (Exception e)
            {
                captured = e;
            }
            finally
            {
                GameThread.Instance.UnmarkGameThread();
            }
        });

        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
