using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Common.LiveTesting;

public sealed class NamedPipeLiveTestServer : IDisposable
{
    private readonly string pipeName;
    private readonly Func<LiveTestProcessInfo> processProvider;
    private readonly Func<LiveTestRequest, LiveTestResponse> requestHandler;
    private readonly object lifetimeLock = new object();
    private readonly ManualResetEventSlim listenerWaiting = new ManualResetEventSlim(false);

    private Thread listenerThread;
    private NamedPipeServerStream activePipe;
    private bool started;
    private bool disposed;

    public NamedPipeLiveTestServer(
        string pipeName,
        Func<LiveTestProcessInfo> processProvider,
        Func<LiveTestRequest, LiveTestResponse> requestHandler)
    {
        if (string.IsNullOrWhiteSpace(pipeName)) throw new ArgumentException("A pipe name is required.", nameof(pipeName));
        if (processProvider == null) throw new ArgumentNullException(nameof(processProvider));
        if (requestHandler == null) throw new ArgumentNullException(nameof(requestHandler));

        this.pipeName = pipeName;
        this.processProvider = processProvider;
        this.requestHandler = requestHandler;
    }

    public void Start()
    {
        lock (lifetimeLock)
        {
            ThrowIfDisposed();
            if (started) throw new InvalidOperationException("The live-test pipe server has already started.");

            started = true;
            listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "BannerlordCoop live-test pipe",
            };
            listenerThread.Start();
        }
    }

    public void Dispose()
    {
        Thread threadToJoin;
        lock (lifetimeLock)
        {
            if (disposed) return;

            disposed = true;
            activePipe?.Dispose();
            activePipe = null;
            threadToJoin = listenerThread;
        }

        if (threadToJoin != null && threadToJoin != Thread.CurrentThread)
        {
            threadToJoin.Join(1000);
        }
    }

    internal bool WaitUntilListening(TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (!listenerWaiting.Wait(timeout)) return false;

        TimeSpan remaining = timeout - stopwatch.Elapsed;
        if (remaining <= TimeSpan.Zero) return false;

        return SpinWait.SpinUntil(() =>
        {
            lock (lifetimeLock)
            {
                return listenerWaiting.IsSet &&
                    listenerThread != null &&
                    (listenerThread.ThreadState & ThreadState.WaitSleepJoin) != 0;
            }
        }, remaining);
    }

    private void Listen()
    {
        while (!IsDisposed())
        {
            try
            {
                using (var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous))
                {
                    SetActivePipe(pipe);
                    while (!IsDisposed())
                    {
                        bool connectionAccepted = false;
                        try
                        {
                            listenerWaiting.Set();
                            try
                            {
                                pipe.WaitForConnection();
                                connectionAccepted = true;
                            }
                            finally
                            {
                                listenerWaiting.Reset();
                            }

                            ServeConnection(pipe);
                        }
                        finally
                        {
                            if (connectionAccepted && !IsDisposed())
                            {
                                pipe.Disconnect();
                            }
                        }
                    }
                }
            }
            catch (SocketException) when (IsDisposed())
            {
            }
            catch (Exception exception) when (
                exception is SocketException ||
                exception is IOException ||
                exception is ObjectDisposedException ||
                exception is InvalidOperationException)
            {
                if (!IsDisposed()) Thread.Sleep(25);
            }
            finally
            {
                listenerWaiting.Reset();
                ClearActivePipe();
            }
        }
    }

    private void ServeConnection(NamedPipeServerStream pipe)
    {
        var encoding = new UTF8Encoding(false, true);
        using (var reader = new StreamReader(pipe, encoding, false, 4096, true))
        using (var writer = new StreamWriter(pipe, encoding, 4096, true) { AutoFlush = true })
        {
            while (!IsDisposed())
            {
                string requestJson;
                bool characterLimitExceeded;
                try
                {
                    requestJson = ReadBoundedLine(reader, out characterLimitExceeded);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is DecoderFallbackException)
                {
                    return;
                }

                if (requestJson == null) return;

                LiveTestResponse response = characterLimitExceeded
                    ? LiveTestResponse.Failure(
                        "unknown",
                        processProvider(),
                        new LiveTestError(
                            "request_too_large",
                            $"Live-test requests cannot exceed {LiveTestProtocol.MaximumMessageBytes} UTF-8 bytes.",
                            false))
                    : CreateResponse(requestJson);
                string responseJson;
                try
                {
                    responseJson = LiveTestProtocol.SerializeResponse(response);
                }
                catch (Exception exception)
                {
                    responseJson = LiveTestProtocol.SerializeResponse(LiveTestResponse.Failure(
                        response.Id,
                        processProvider(),
                        new LiveTestError("response_too_large", exception.Message, true)));
                }

                try
                {
                    writer.WriteLine(responseJson);
                }
                catch (IOException)
                {
                    return;
                }
            }
        }
    }

    private static string ReadBoundedLine(StreamReader reader, out bool characterLimitExceeded)
    {
        var line = new StringBuilder();
        characterLimitExceeded = false;

        while (true)
        {
            int value = reader.Read();
            if (value == -1)
            {
                return line.Length == 0 && !characterLimitExceeded ? null : line.ToString();
            }

            if (value == '\n')
            {
                if (line.Length > 0 && line[line.Length - 1] == '\r')
                {
                    line.Length--;
                }

                return line.ToString();
            }

            if (line.Length < LiveTestProtocol.MaximumMessageBytes)
            {
                line.Append((char)value);
            }
            else
            {
                characterLimitExceeded = true;
            }
        }
    }

    private LiveTestResponse CreateResponse(string requestJson)
    {
        if (!LiveTestProtocol.TryDeserializeRequest(requestJson, out var request, out var validationError))
        {
            return LiveTestResponse.Failure(
                request?.Id ?? "unknown",
                processProvider(),
                validationError);
        }

        try
        {
            LiveTestResponse response = requestHandler(request);
            if (response == null)
            {
                return LiveTestResponse.Failure(
                    request.Id,
                    processProvider(),
                    new LiveTestError("empty_response", "The request handler returned no response.", true));
            }

            response.Version = LiveTestProtocol.Version;
            response.Id = request.Id;
            response.Process = response.Process ?? processProvider();
            return response;
        }
        catch (Exception exception)
        {
            return LiveTestResponse.Failure(
                request.Id,
                processProvider(),
                new LiveTestError("handler_exception", exception.ToString(), true));
        }
    }

    private void SetActivePipe(NamedPipeServerStream pipe)
    {
        lock (lifetimeLock)
        {
            if (disposed)
            {
                pipe.Dispose();
                return;
            }

            activePipe = pipe;
        }
    }

    private void ClearActivePipe()
    {
        lock (lifetimeLock)
        {
            activePipe = null;
        }
    }

    private bool IsDisposed()
    {
        lock (lifetimeLock)
        {
            return disposed;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(NamedPipeLiveTestServer));
    }
}
