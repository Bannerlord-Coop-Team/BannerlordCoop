using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Debug = TaleWorlds.Library.Debug;
using Logger = NLog.Logger;

namespace Coop.Mod.DebugUtil
{
    public class DebugManager : IDebugManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public void Assert(
            bool condition,
            string message,
            [CallerFilePath] string CallerFile = "",
            [CallerMemberName] string CallerMethod = "",
            [CallerLineNumber] int CallerLine = 0)
        {
            if (!condition)
            {
                Logger.Debug(
                    "Assert failure in {file}::{method}::{line}: {message}",
                    CallerFile,
                    CallerMethod,
                    CallerLine);
                if (Debugger.IsAttached)
                {
                    //Debugger.Break();
                }
            }
        }

        public void SilentAssert(
            bool condition,
            string message = "",
            bool getDump = false,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMethod = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (!condition)
            {
                Logger.Debug(
                    "Assert failure in {file}::{method}::{line}: {message}",
                    callerFile,
                    callerMethod,
                    callerLine);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        public void BeginTelemetryScope(TelemetryLevelMask levelMask, string scopeName)
        {
        }

        public void ShowMessageBox(string lpText, string lpCaption, uint uType)
        {
            Logger.Debug("{caption}: {message}", lpCaption, lpText);
        }

        public void DisplayDebugMessage(string message)
        {
            Logger.Debug("{message}", message);
        }

        public void EndTelemetryScope()
        {
        }

        public Vec3 GetDebugVector()
        {
            return MBDebug.DebugVector;
        }

        public void Print(
            string message,
            int logLevel = 0,
            Debug.DebugColor color = Debug.DebugColor.White,
            ulong debugFilter = 17592186044416)
        {
            LogEventInfo eventInfo = new LogEventInfo(LogLevel.Debug, Logger.Name, message);
            Logger.Log(typeof(DebugManager), eventInfo);
        }

        public void PrintError(string error, string stackTrace, ulong debugFilter = 17592186044416)
        {
            Logger.Error("{error}. {stacktrace}.", error, stackTrace);
        }

        public void PrintWarning(string warning, ulong debugFilter = 17592186044416)
        {
            Logger.Warn("{message}", warning);
        }

        public void RenderDebugFrame(MatrixFrame frame, float lineLength, float time = 0)
        {
        }

        public void RenderDebugLine(
            Vec3 position,
            Vec3 direction,
            uint color = uint.MaxValue,
            bool depthCheck = false,
            float time = 0)
        {
        }

        public void RenderDebugSphere(
            Vec3 position,
            float radius,
            uint color = uint.MaxValue,
            bool depthCheck = false,
            float time = 0)
        {
        }

        public void RenderDebugText(
            float screenX,
            float screenY,
            string text,
            uint color = uint.MaxValue,
            float time = 0)
        {
        }

        public void SetCrashReportCustomStack(string customStack)
        {
        }

        public void SetCrashReportCustomString(string customString)
        {
        }

        public void ShowWarning(string message)
        {
            Logger.Warn("{message}", message);
        }

        public void WatchVariable(string name, object value)
        {
        }

        public void WriteDebugLineOnScreen(string message)
        {
            Logger.Debug("{message}", message);
        }

        public void ShowError(string message)
        {
            Logger.Error("{message}", message);
        }

        public void BeginTelemetryScopeBaseLevel(TelemetryLevelMask levelMask, string scopeName)
        {
            throw new System.NotImplementedException();
        }

        public void EndTelemetryScopeBaseLevel()
        {
            throw new System.NotImplementedException();
        }

        public void RenderDebugText3D(Vec3 position, string text, uint color = uint.MaxValue, int screenPosOffsetX = 0, int screenPosOffsetY = 0, float time = 0)
        {
            throw new System.NotImplementedException();
        }
    }
}
