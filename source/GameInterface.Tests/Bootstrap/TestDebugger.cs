using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap
{
    internal class TestDebugger : IDebugManager
    {
        public void AbortGame()
        {
            throw new NotImplementedException();
        }

        public void Assert(bool condition, string message, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerMethod = "", [CallerLineNumber] int callerLine = 0)
        {
            
        }

        public void BeginTelemetryScopeBaseLevelInternal(TelemetryLevelMask levelMask, string scopeName)
        {
            throw new NotImplementedException();
        }

        public void BeginTelemetryScopeInternal(TelemetryLevelMask levelMask, string scopeName)
        {
            throw new NotImplementedException();
        }

        public void DisplayDebugMessage(string message)
        {
            throw new NotImplementedException();
        }

        public void DoDelayedexit(int returnCode)
        {
            throw new NotImplementedException();
        }

        public void EndTelemetryScopeBaseLevelInternal()
        {

        }

        public void EndTelemetryScopeInternal()
        {
            throw new NotImplementedException();
        }

        public Vec3 GetDebugVector()
        {
            throw new NotImplementedException();
        }

        public void Print(string message, int logLevel = 0, Debug.DebugColor color = Debug.DebugColor.White, ulong debugFilter = 17592186044416)
        {
            throw new NotImplementedException();
        }

        public void PrintError(string error, string stackTrace, ulong debugFilter = 17592186044416)
        {
            throw new NotImplementedException();
        }

        public void PrintWarning(string warning, ulong debugFilter = 17592186044416)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugFrame(MatrixFrame frame, float lineLength, float time = 0)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugLine(Vec3 position, Vec3 direction, uint color = uint.MaxValue, bool depthCheck = false, float time = 0)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugRectWithColor(float left, float bottom, float right, float top, uint color = uint.MaxValue)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugSphere(Vec3 position, float radius, uint color = uint.MaxValue, bool depthCheck = false, float time = 0)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugText(float screenX, float screenY, string text, uint color = uint.MaxValue, float time = 0)
        {
            throw new NotImplementedException();
        }

        public void RenderDebugText3D(Vec3 position, string text, uint color = uint.MaxValue, int screenPosOffsetX = 0, int screenPosOffsetY = 0, float time = 0)
        {
            throw new NotImplementedException();
        }

        public void ReportMemoryBookmark(string message)
        {
            throw new NotImplementedException();
        }

        public void SetCrashReportCustomStack(string customStack)
        {
            throw new NotImplementedException();
        }

        public void SetCrashReportCustomString(string customString)
        {
            throw new NotImplementedException();
        }

        public void SetTestModeEnabled(bool testModeEnabled)
        {
            throw new NotImplementedException();
        }

        public void ShowError(string message)
        {
            throw new NotImplementedException();
        }

        public void ShowMessageBox(string lpText, string lpCaption, uint uType)
        {
            throw new NotImplementedException();
        }

        public void ShowWarning(string message)
        {
            throw new NotImplementedException();
        }

        public void SilentAssert(bool condition, string message = "", bool getDump = false, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerMethod = "", [CallerLineNumber] int callerLine = 0)
        {
            throw new NotImplementedException();
        }

        public void WatchVariable(string name, object value)
        {
            throw new NotImplementedException();
        }

        public void WriteDebugLineOnScreen(string message)
        {
            throw new NotImplementedException();
        }
    }
}
