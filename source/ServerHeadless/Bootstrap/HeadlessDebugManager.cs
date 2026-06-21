using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap
{
    /// <summary>
    /// The <see cref="IDebugManager"/> for the headless host.
    ///
    /// In the shipping game every error, failed assert and warning the engine and campaign raise is
    /// routed through <see cref="Debug"/> to a native debug manager that pops an on-screen error
    /// report. Headless there is no native manager — <see cref="Debug.DebugManager"/> is null — so
    /// those calls are silently swallowed and the operator never learns WHY a save misbehaves or the
    /// server stops. (The native error-report SCENE is neutralised separately, in
    /// <see cref="Patches.EnginePatches"/>, because it bypasses this manager and calls straight into
    /// the dead native engine.)
    ///
    /// This manager surfaces the error-level signals (errors, message boxes, failed asserts) on the
    /// console — the headless server's log — and no-ops the rendering / telemetry / per-frame
    /// chatter. Every member is non-throwing so a single diagnostic call can never bring the server
    /// down.
    /// </summary>
    internal sealed class HeadlessDebugManager : IDebugManager
    {
        private const string Prefix = "[Headless Engine]";

        public void ShowError(string message)
            => Console.Error.WriteLine($"{Prefix} ERROR: {message}");

        public void PrintError(string error, string stackTrace, ulong debugFilter = 17592186044416uL)
        {
            Console.Error.WriteLine($"{Prefix} ERROR: {error}");
            if (!string.IsNullOrEmpty(stackTrace))
            {
                Console.Error.WriteLine(stackTrace);
            }
        }

        public void ShowMessageBox(string lpText, string lpCaption, uint uType)
            => Console.Error.WriteLine($"{Prefix} {lpCaption}: {lpText}");

        public void Assert(bool condition, string message, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerMethod = "", [CallerLineNumber] int callerLine = 0)
        {
            if (!condition)
            {
                Console.Error.WriteLine($"{Prefix} ASSERT FAILED: {message} (at {callerMethod} in {callerFile}:{callerLine})");
            }
        }

        public void SilentAssert(bool condition, string message = "", bool getDump = false, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerMethod = "", [CallerLineNumber] int callerLine = 0)
        {
            if (!condition)
            {
                Console.Error.WriteLine($"{Prefix} ASSERT FAILED: {message} (at {callerMethod} in {callerFile}:{callerLine})");
            }
        }

        public void ShowWarning(string message)
            => Console.Out.WriteLine($"{Prefix} WARNING: {message}");

        public void PrintWarning(string warning, ulong debugFilter = 17592186044416uL)
            => Console.Out.WriteLine($"{Prefix} WARNING: {warning}");

        // The engine asks to abort / exit when it would normally tear the process down on a fatal
        // error. Surface it but let the headless game loop decide when to stop, rather than killing
        // the process from inside a diagnostic callback.
        public void AbortGame()
            => Console.Error.WriteLine($"{Prefix} AbortGame requested (ignored headless).");

        public void DoDelayedexit(int returnCode)
            => Console.Error.WriteLine($"{Prefix} DoDelayedexit({returnCode}) requested (ignored headless).");

        // Chatter and graphics-only members: no-ops headless. Print in particular is extremely
        // high-volume general game logging, so leaving it silent keeps the console readable; the
        // signals worth seeing are captured by the error/assert/warning members above.
        public void Print(string message, int logLevel = 0, Debug.DebugColor color = Debug.DebugColor.White, ulong debugFilter = 17592186044416uL) { }
        public void DisplayDebugMessage(string message) { }
        public void WriteDebugLineOnScreen(string message) { }
        public void WatchVariable(string name, object value) { }
        public void ReportMemoryBookmark(string message) { }
        public void SetCrashReportCustomString(string customString) { }
        public void SetCrashReportCustomStack(string customStack) { }
        public void SetTestModeEnabled(bool testModeEnabled) { }
        public void SetDebugVector(Vec3 value) { }
        public Vec3 GetDebugVector() => Vec3.Zero;
        public void RenderDebugLine(Vec3 position, Vec3 direction, uint color = uint.MaxValue, bool depthCheck = false, float time = 0f) { }
        public void RenderDebugSphere(Vec3 position, float radius, uint color = uint.MaxValue, bool depthCheck = false, float time = 0f) { }
        public void RenderDebugText3D(Vec3 position, string text, uint color = uint.MaxValue, int screenPosOffsetX = 0, int screenPosOffsetY = 0, float time = 0f) { }
        public void RenderDebugFrame(MatrixFrame frame, float lineLength, float time = 0f) { }
        public void RenderDebugText(float screenX, float screenY, string text, uint color = uint.MaxValue, float time = 0f) { }
        public void RenderDebugRectWithColor(float left, float bottom, float right, float top, uint color = uint.MaxValue) { }
        public void BeginTelemetryScopeBaseLevelInternal(TelemetryLevelMask levelMask, string scopeName) { }
        public void BeginTelemetryScopeInternal(TelemetryLevelMask levelMask, string scopeName) { }
        public void EndTelemetryScopeBaseLevelInternal() { }
        public void EndTelemetryScopeInternal() { }
    }
}
