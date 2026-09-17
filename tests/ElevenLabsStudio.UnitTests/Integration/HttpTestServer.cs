using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElevenLabsStudio.UnitTests.Integration;

/// <summary>
/// Lightweight in-process HTTP server backed by <see cref="HttpListener"/>.
/// Stands in for api.elevenlabs.io in integration tests so we exercise
/// <see cref="ElevenLabsStudio.Infrastructure.Http.ElevenLabsHttpClient"/>'s
/// real wire-level code path (auth header, JSON shape, error mapping,
/// Polly retry) without hitting the public internet.
/// </summary>
public sealed class HttpTestServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly ConcurrentQueue<(HttpListenerContext ctx, TaskCompletionSource completion)> _inflight
        = new();

    /// <summary>Per-test queue of canned (status, body, contentType) responses.</summary>
    public ConcurrentQueue<(int status, string body, string contentType)> Responses { get; } = new();

    /// <summary>Captured (method, path, headers) for each request the test
    /// sent, in arrival order. Inspect after the call to assert auth etc.</summary>
    public List<CapturedRequest> Captured { get; } = new();

    public string BaseUrl { get; }

    public HttpTestServer() : this(AllocateLoopback()) { }

    private HttpTestServer(string baseUrl)
    {
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        BaseUrl = baseUrl;
        _listener = new HttpListener { IgnoreWriteExceptions = true };
        _listener.Prefixes.Add(baseUrl);
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 183) // ERROR_ALREADY_EXISTS
        {
            // port locked by another test in the same run — let the
            // constructor throw so the test fails loudly.
            throw;
        }
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>
    /// Allocate a free TCP port on the loopback interface. TcpListener
    /// bound to port 0 lets the kernel pick a free port, which we
    /// then release — the (sub-second) gap before the HttpListener
    /// binds to it is acceptable for parallel-safe test execution.
    /// </summary>
    private static string AllocateLoopback()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            return $"http://localhost:{port}/";
        }
        finally
        {
            probe.Stop();
        }
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }

            var tcs = new TaskCompletionSource();
            _inflight.Enqueue((ctx, tcs));

            // Process off the listener thread so the loop can accept
            // the next request while the test is reading the response.
            _ = Task.Run(async () =>
            {
                try { await ProcessAsync(ctx); }
                finally { tcs.TrySetResult(); }
            });
        }
    }

    private async Task ProcessAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var captured = new CapturedRequest(
            req.HttpMethod,
            req.Url!.AbsolutePath,
            req.Url.Query,
            req.Headers.AllKeys.ToDictionary(k => k, k => req.Headers[k] ?? ""));
        lock (Captured) Captured.Add(captured);

        // Drain body so the test can inspect it.
        string body = "";
        if (req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            body = await reader.ReadToEndAsync();
        }
        captured.RequestBody = body;

        if (Responses.TryDequeue(out var resp))
        {
            ctx.Response.StatusCode = resp.status;
            ctx.Response.ContentType = resp.contentType;
            var bytes = Encoding.UTF8.GetBytes(resp.body);
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
        else
        {
            ctx.Response.StatusCode = 503;
            var bytes = Encoding.UTF8.GetBytes("test server: no canned response");
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
        ctx.Response.Close();
    }

    /// <summary>Enqueue a canned HTTP response.</summary>
    public HttpTestServer Enqueue(int status, string body, string contentType = "application/json")
    {
        Responses.Enqueue((status, body, contentType));
        return this;
    }

    /// <summary>Wait until all queued responses have been consumed.</summary>
    public async Task DrainAsync()
    {
        while (!Responses.IsEmpty) await Task.Delay(10);
        // Give the listener loop a moment to finish writing the
        // last response before the test inspects Captured.
        await Task.Delay(50);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* ignore */ }
        _listener.Close();
        try { await _loop; } catch { /* ignore */ }
        _cts.Dispose();
    }
}

public sealed record CapturedRequest(
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string> Headers)
{
    public string RequestBody { get; set; } = "";
    public string FullPath => string.IsNullOrEmpty(Query) ? Path : $"{Path}{Query}";
}