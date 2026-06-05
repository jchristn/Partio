namespace Test.Shared
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    /// <summary>
    /// Minimal Ollama-compatible test server used to simulate concurrent upstream calls.
    /// </summary>
    public sealed class SlowOllamaCompatibleServer : IDisposable, IAsyncDisposable
    {
        private readonly TcpListener _Listener;
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Task _AcceptLoopTask;
        private readonly ConcurrentDictionary<string, int> _RawPathRequestCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public string BaseUrl { get; }

        public int EmbeddingDelayMs { get; set; }

        public int ChatDelayMs { get; set; }

        public string EmbeddingModel { get; set; } = "all-minilm";

        public string CompletionModel { get; set; } = "gemma3:4b";

        public int EmbeddingRequestCount => Volatile.Read(ref _EmbeddingRequestCount);

        public int ChatRequestCount => Volatile.Read(ref _ChatRequestCount);

        public int TagsRequestCount => Volatile.Read(ref _TagsRequestCount);

        public int GetRawPathRequestCount(string rawPath)
        {
            return _RawPathRequestCounts.TryGetValue(rawPath, out int count) ? count : 0;
        }

        public string? LastEmbeddingKeepAlive { get; private set; }

        public string? LastCompletionKeepAlive { get; private set; }

        private int _EmbeddingRequestCount = 0;

        private int _ChatRequestCount = 0;

        private int _TagsRequestCount = 0;

        public SlowOllamaCompatibleServer(int embeddingDelayMs = 0, int chatDelayMs = 0)
        {
            EmbeddingDelayMs = embeddingDelayMs;
            ChatDelayMs = chatDelayMs;

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

        public async Task WaitForEmbeddingRequestCountAsync(int minCount, int timeoutMs = 5000)
        {
            await WaitForCountAsync(() => EmbeddingRequestCount, minCount, timeoutMs).ConfigureAwait(false);
        }

        public async Task WaitForChatRequestCountAsync(int minCount, int timeoutMs = 5000)
        {
            await WaitForCountAsync(() => ChatRequestCount, minCount, timeoutMs).ConfigureAwait(false);
        }

        public async Task WaitForRawPathRequestCountAsync(string rawPath, int minCount, int timeoutMs = 5000)
        {
            await WaitForCountAsync(() => GetRawPathRequestCount(rawPath), minCount, timeoutMs).ConfigureAwait(false);
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
                    _RawPathRequestCounts.AddOrUpdate(request.RawPath, 1, (_, count) => count + 1);

                    if ((request.Method == "GET" || request.Method == "HEAD") && request.Path == "/api/tags")
                    {
                        Interlocked.Increment(ref _TagsRequestCount);
                        await WriteJsonResponseAsync(stream, 200, new
                        {
                            models = new[]
                            {
                                new
                                {
                                    name = EmbeddingModel,
                                    model = EmbeddingModel,
                                    modified_at = DateTime.UtcNow.ToString("O"),
                                    size = 1
                                },
                                new
                                {
                                    name = CompletionModel,
                                    model = CompletionModel,
                                    modified_at = DateTime.UtcNow.ToString("O"),
                                    size = 1
                                }
                            }
                        }).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/api/show")
                    {
                        await WriteJsonResponseAsync(stream, 404, new { error = "capability probe not implemented" }).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/api/embed")
                    {
                        Interlocked.Increment(ref _EmbeddingRequestCount);
                        LastEmbeddingKeepAlive = ReadStringProperty(request.Body, "keep_alive");
                        await DelayIfNeededAsync(EmbeddingDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, BuildEmbeddingResponse(request.Body)).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/api/chat")
                    {
                        Interlocked.Increment(ref _ChatRequestCount);
                        LastCompletionKeepAlive = ReadStringProperty(request.Body, "keep_alive");
                        await DelayIfNeededAsync(ChatDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, new
                        {
                            model = CompletionModel,
                            created_at = DateTime.UtcNow.ToString("O"),
                            message = new
                            {
                                role = "assistant",
                                content = "Stub Ollama response."
                            },
                            done = true,
                            done_reason = "stop",
                            total_duration = 1,
                            load_duration = 1,
                            prompt_eval_count = 1,
                            prompt_eval_duration = 1,
                            eval_count = 1,
                            eval_duration = 1
                        }).ConfigureAwait(false);
                        return;
                    }

                    if (request.Method == "POST" && request.Path == "/api/generate")
                    {
                        Interlocked.Increment(ref _ChatRequestCount);
                        LastCompletionKeepAlive = ReadStringProperty(request.Body, "keep_alive");
                        await DelayIfNeededAsync(ChatDelayMs, token).ConfigureAwait(false);
                        await WriteJsonResponseAsync(stream, 200, new
                        {
                            model = CompletionModel,
                            created_at = DateTime.UtcNow.ToString("O"),
                            response = "",
                            done = true,
                            done_reason = "load",
                            total_duration = 1,
                            load_duration = 1,
                            prompt_eval_count = 0,
                            prompt_eval_duration = 0,
                            eval_count = 0,
                            eval_duration = 0
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

            return new RawHttpRequest(method, path, rawPath, body);
        }

        private object BuildEmbeddingResponse(string requestBody)
        {
            int inputCount = CountEmbeddingInputs(requestBody);
            List<float[]> embeddings = new List<float[]>();
            for (int i = 0; i < inputCount; i++)
            {
                embeddings.Add(new[] { 0.125f, 0.25f, 0.5f });
            }

            return new
            {
                model = EmbeddingModel,
                embeddings,
                total_duration = 1,
                load_duration = 1,
                prompt_eval_count = inputCount
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

        private static string? ReadStringProperty(string requestBody, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(requestBody))
                return null;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(requestBody);
                if (!doc.RootElement.TryGetProperty(propertyName, out JsonElement property))
                    return null;

                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
            }
            catch
            {
                return null;
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
                "HTTP/1.1 " + statusCode + " " + statusText + "\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n" +
                "Connection: close\r\n\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static int GetAvailablePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed record RawHttpRequest(string Method, string Path, string RawPath, string Body);
    }
}
