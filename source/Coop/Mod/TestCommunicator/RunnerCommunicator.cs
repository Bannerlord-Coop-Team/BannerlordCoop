using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Mod.BIT
{
    public enum E_BIT_Requests
    {
        IsHost,
        HostState,
        ConnectedGames,
    }
    class RunnerCommunicator
    {

        public static readonly Dictionary<Enum, string> BITCommands = new Dictionary<Enum, string>() 
        {
            { E_BIT_Requests.IsHost, "Is Host"},
            { E_BIT_Requests.HostState, "Host State"},
            { E_BIT_Requests.ConnectedGames, "Connected Games"},
        };

        public event Action<string> OnDataReceived;
        public event Action<string> OnDataSent;

        public RunnerCommunicator()
        {
            Task.Run(async () =>
            {
                // Cancle connect after 5 seconds
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                await ws.ConnectAsync(new Uri("ws://localhost:8080"), cts.Token);
            }).Wait();

            ReceiveLoopTask = Task.Run(() => ReceiveLoop(ReceiveLoopCTS.Token));
        }

        public bool SendData(string message)
        {
            if(message == string.Empty) { return false; }

            byte[] asciiData = Encoding.ASCII.GetBytes(message);
            ArraySegment<byte> array = new ArraySegment<byte>(asciiData);
            Task.Run(async () => await ws.SendAsync(array, WebSocketMessageType.Text, true, new CancellationToken())).Wait();
            OnDataSent?.Invoke(message);
            return true;
        }

        #region Private
        private readonly static ClientWebSocket ws = new ClientWebSocket();
        private Task ReceiveLoopTask;
        private CancellationTokenSource ReceiveLoopCTS = new CancellationTokenSource();
        private async void ReceiveLoop(CancellationToken cancellationToken)
        {
            string message = string.Empty;
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] byteArray = new byte[100];
                ArraySegment<byte> array = new ArraySegment<byte>(byteArray);
                WebSocketReceiveResult result = await ws.ReceiveAsync(array, cancellationToken);

                message += Encoding.ASCII.GetString(array.Array);

                if (result.EndOfMessage)
                {
                    message = message.Trim(new char[] { '\0' });
                    OnDataReceived?.Invoke(message);
                    message = string.Empty;
                }
            }
        }

        ~RunnerCommunicator()
        {
            ReceiveLoopCTS.Cancel();
        }
        #endregion
    }
    
}
