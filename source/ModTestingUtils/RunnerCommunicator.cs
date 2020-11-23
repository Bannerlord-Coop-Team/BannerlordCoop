using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModTestingUtils
{
    class RunnerCommunicator
    {
        public event Action<string> OnMessageReceived;
        public event Action<string> OnMessageSent;

        public bool Connected = false;

        public RunnerCommunicator()
        {
            Task<bool> connectTask = Task<bool>.Run(async () =>
            {
                // Cancle connect after 5 seconds
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await ws.ConnectAsync(new Uri("ws://localhost:8080"), cts.Token);
                }
                catch (WebSocketException) {
                    return false;
                }
                return true;
            });

            connectTask.Wait();

            if(connectTask.Result)
            {
                Connected = true;
                ReceiveLoopTask = Task.Run(() => ReceiveLoop(ReceiveLoopCTS.Token));
            }
        }

        public bool SendData(string message)
        {
            if(message == string.Empty) { return false; }

            byte[] asciiData = Encoding.ASCII.GetBytes(message);
            ArraySegment<byte> array = new ArraySegment<byte>(asciiData);
            Task.Run(async () => await ws.SendAsync(array, WebSocketMessageType.Text, true, new CancellationToken())).Wait();
            OnMessageSent?.Invoke(message);
            return true;
        }

        #region Private
        private readonly static ClientWebSocket ws = new ClientWebSocket();
        private Task ReceiveLoopTask;
        private CancellationTokenSource ReceiveLoopCTS = new CancellationTokenSource();
        private async void ReceiveLoop(CancellationToken cancellationToken)
        {
            // TODO add disconnect handling
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
                    OnMessageReceived?.Invoke(message);
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
