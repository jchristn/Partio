namespace Test.Shared
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    /// <summary>
    /// Minimal OpenAI-compatible test server used to simulate slow upstream providers.
    /// </summary>
    public sealed class SlowOpenAiCompatibleServer : IDisposable, IAsyncDisposable
    {
        private readonly TcpListener _Listener;
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Task _AcceptLoopTask;

        public string BaseUrl { get; }

        public int EmbeddingDelayMs { get; set; }

        public int CompletionDelayMs { get; set; }

        public int ModelsDelayMs { get; set; }

        public string EmbeddingModel { get; set; } = "text-embedding-3-small";

        public string CompletionModel { get; set; } = "gpt-4.1-mini";

        public int EmbeddingRequestCount => Volatile.Read(ref _EmbeddingRequestCount);

        public int CompletionRequestCount => Volatile.Read(ref _CompletionRequestCount);

        private int _EmbeddingRequestCount = 0;

        private int _CompletionRequestCount = 0;

        public SlowOpenAiCompatibleServer(int embeddingDelayMs = 0, int completionDelayMs = 0, int modelsDelayMs = 0)
        {
            EmbeddingDelayMs = embeddingDelayMs;
            CompletionDelayMs = completionDelayMs;
            ModelsDelayMs = modelsDelayMs;

            int port = GetAvailablePort();
            BaseUrl = "http://127.0.0.1:" + port;
            _Listener = new TcpListener(IPAddress.Loopback, port);
            _Listener.Start();
            _AcceptLoopTask = Task.Run(() => AcceptLoopAsync(_Cancellation.Token));
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            _Cancellation.Cancel();

            try
            {
                _Listener.Stop();
            }
            catch
            {
            }

            try
            {
                await _AcceptLoopTask.ConfigureAwait(false);
            }
            catch
            {
            }

            _Cancellation.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested) break;
                    client?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    RawHttpRequest? request = await ReadRequestAsync(stream, token).ConfigureAwait(false);
                    if (request == null) return;

                    if (request.Method == "GET" && request.Path == "/v1/models")
                    {
                        await DelayIfNeededAsync(ModelsDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, new
                        {
                            @object = "list",
                            data = new[]
                            {
                                new { id = CompletionModel, @object = "model" },
                                new { id = EmbeddingModel, @object = "model" }
                            }
                        }).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/v1/embeddings")
                    {
                        Interlocked.Increment(ref _EmbeddingRequestCount);
                        await DelayIfNeededAsync(EmbeddingDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, BuildEmbeddingResponse(request.Body)).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/v1/chat/completions")
                    {
                        Interlocked.Increment(ref _CompletionRequestCount);
                        await DelayIfNeededAsync(CompletionDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, new
                        {
                            id = "chatcmpl_stub",
                            @object = "chat.completion",
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            model = CompletionModel,
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    message = new { role = "assistant", content = "Stub completion response." },
                                    finish_reason = "stop"
                                }
                            },
                            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 }
                        }).ConfigureAwait(false);
                        return;
                    }

                    await WriteJsonResponseAsync(stream, 404, new { error = "NotFound", path = request.Path }).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    try
                    {
                        await WriteJsonResponseAsync(stream, 500, new { error = "InternalError" }).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task<RawHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            string? requestLine = await reader.ReadLineAsync().WaitAsync(token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine))
                return null;

            string[] requestLineParts = requestLine.Split(' ');
            string method = requestLineParts.Length > 0 ? requestLineParts[0].Trim().ToUpperInvariant() : "GET";
            string rawPath = requestLineParts.Length > 1 ? requestLineParts[1].Trim() : "/";
            string path = rawPath.Split('?', 2)[0];

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                string? line = await reader.ReadLineAsync().WaitAsync(token).ConfigureAwait(false);
                if (line == null || line.Length < 1)
                    break;

                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    string key = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();
                    headers[key] = value;
                }
            }

            int contentLength = 0;
            if (headers.TryGetValue("Content-Length", out string? contentLengthValue))
                int.TryParse(contentLengthValue, out contentLength);

            string body = string.Empty;
            if (contentLength > 0)
            {
                char[] buffer = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                {
                    int bytesRead = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), token).ConfigureAwait(false);
                    if (bytesRead <= 0) break;
                    read += bytesRead;
                }

                body = new string(buffer, 0, read);
            }

            return new RawHttpRequest(method, path, body);
        }

        public async Task WaitForEmbeddingRequestCountAsync(int minCount, int timeoutMs = 5000)
        {
            await WaitForCountAsync(() => EmbeddingRequestCount, minCount, timeoutMs).ConfigureAwait(false);
        }

        public async Task WaitForCompletionRequestCountAsync(int minCount, int timeoutMs = 5000)
        {
            await WaitForCountAsync(() => CompletionRequestCount, minCount, timeoutMs).ConfigureAwait(false);
        }

        private static object BuildEmbeddingResponse(string requestBody)
        {
            int inputCount = CountEmbeddingInputs(requestBody);
            List<object> data = new List<object>();
            for (int i = 0; i < inputCount; i++)
            {
                data.Add(new
                {
                    @object = "embedding",
                    index = i,
                    embedding = new[] { 0.125f, 0.25f, 0.5f }
                });
            }

            return new
            {
                @object = "list",
                data,
                model = "text-embedding-3-small",
                usage = new { prompt_tokens = inputCount, total_tokens = inputCount }
            };
        }

        private static int CountEmbeddingInputs(string requestBody)
        {
            if (string.IsNullOrWhiteSpace(requestBody))
                return 1;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(requestBody);
                if (!doc.RootElement.TryGetProperty("input", out JsonElement input))
                    return 1;

                if (input.ValueKind == JsonValueKind.Array)
                    return Math.Max(1, input.GetArrayLength());

                return 1;
            }
            catch
            {
                return 1;
            }
        }

        private static async Task WaitForCountAsync(Func<int> getCount, int minCount, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (getCount() >= minCount)
                    return;

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for upstream request count " + minCount + ".");
        }

        private static async Task DelayIfNeededAsync(int delayMs, CancellationToken token)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, token).ConfigureAwait(false);
        }

        private static async Task WriteJsonResponseAsync(NetworkStream stream, int statusCode, object payload)
        {
            string statusText = statusCode switch
            {
                200 => "OK",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "OK"
            };

            string body = JsonSerializer.Serialize(payload);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string headers =
                "HTTP/1.1 " + statusCode + " " + statusText + "\r\n"
                + "Content-Type: application/json\r\n"
                + "Content-Length: " + bodyBytes.Length + "\r\n"
                + "Connection: close\r\n"
                + "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static int GetAvailablePort()
        {
            using TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private sealed class RawHttpRequest
        {
            public string Method { get; }

            public string Path { get; }

            public string Body { get; }

            public RawHttpRequest(string method, string path, string body)
            {
                Method = method;
                Path = path;
                Body = body;
            }
        }
    }
}
