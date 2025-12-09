using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Time.Commands;

    internal class TimeCommands
    {
        [CommandLineArgumentFunction("get_time_mode", "coop.debug")]
        public static string GetTimeMode(List<string> strings)
        {
            var tx = new GetterTransaction();

            return $"{tx.GetTimeControlMode()}";
        }

        [CommandLineArgumentFunction("net_status", "coop.debug")]
        public static string NetStatus(List<string> args)
        {
            var mode = global::GameInterface.ModInformation.IsServer ? "Serveur" : "Client";
            return $"Mode: {mode}, Port: 4200";
        }

        [CommandLineArgumentFunction("net_logs", "coop.debug")]
        public static string NetLogs(List<string> args)
        {
            try
            {
                var logs = new List<string>();
                var sys = Path.Combine("C:\\ProgramData\\Mount and Blade II Bannerlord\\logs", global::GameInterface.ModInformation.IsServer ? "Coop_server.log" : "Coop_client.log");
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                var alt = Path.Combine(asmDir, global::GameInterface.ModInformation.IsServer ? "Coop_server.log" : "Coop_client.log");
                var files = new[] { sys, alt };
                foreach (var f in files)
                {
                    if (File.Exists(f))
                    {
                        var lines = File.ReadAllLines(f);
                        logs.Add($"[{Path.GetFileName(f)}] \n" + string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 10))));
                    }
                }
                return logs.Count > 0 ? string.Join("\n\n", logs) : "Aucun log disponible";
            }
            catch { return "Lecture logs échouée"; }
        }
    }

class GetterTransaction
{
    public GetterTransaction()
    {
        MessageBroker.Instance.Subscribe<TimeControlModeResponse>(Handle);
    }

    ~GetterTransaction()
    {
        MessageBroker.Instance.Unsubscribe<TimeControlModeResponse>(Handle);
    }

    TaskCompletionSource<TimeControlEnum> tcs;
    public string GetTimeControlMode()
    {
        tcs = new TaskCompletionSource<TimeControlEnum>();
        var cts = new CancellationTokenSource(1000);

        MessageBroker.Instance.Publish(this, new GetTimeControlMode());

        try
        {
            tcs.Task.Wait(cts.Token);
            return $"{tcs.Task.Result}";
        }
        catch(OperationCanceledException)
        {
            return "Failed to get time mode";
        }
    }

    private void Handle(MessagePayload<TimeControlModeResponse> payload)
    {
        tcs.SetResult(payload.What.TimeMode);
    }
}
