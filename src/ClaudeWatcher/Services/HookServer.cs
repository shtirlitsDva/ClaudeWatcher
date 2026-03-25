using System.IO;
using ClaudeWatcher.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClaudeWatcher.Services;

public class HookServer : IDisposable
{
    private WebApplication? _app;
    private readonly SessionManager _sessionManager;
    public int Port { get; private set; } = 22322;

    public HookServer(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task StartAsync()
    {
        for (int port = 22322; port <= 22330; port++)
        {
            try
            {
                var builder = WebApplication.CreateSlimBuilder();
                builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
                builder.Logging.ClearProviders();
                _app = builder.Build();

                MapEndpoints(_app);
                await _app.StartAsync();
                Port = port;
                return;
            }
            catch (IOException)
            {
                // Port in use, try next
            }
        }
    }

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", port = Port }));

        app.MapPost("/api/session/start", async (HttpContext ctx) =>
        {
            try
            {
                var payload = await ctx.Request.ReadFromJsonAsync<SessionStartPayload>();
                if (payload != null) _sessionManager.RegisterSession(payload);
            }
            catch { }
            return Results.Ok();
        });

        app.MapPost("/api/session/update", async (HttpContext ctx) =>
        {
            try
            {
                var payload = await ctx.Request.ReadFromJsonAsync<SessionUpdatePayload>();
                if (payload != null) _sessionManager.UpdateSession(payload);
            }
            catch { }
            return Results.Ok();
        });

        app.MapPost("/api/session/notification", async (HttpContext ctx) =>
        {
            try
            {
                var payload = await ctx.Request.ReadFromJsonAsync<SessionNotificationPayload>();
                if (payload != null) _sessionManager.NotifySession(payload);
            }
            catch { }
            return Results.Ok();
        });

        app.MapPost("/api/session/end", async (HttpContext ctx) =>
        {
            try
            {
                var payload = await ctx.Request.ReadFromJsonAsync<SessionEndPayload>();
                if (payload != null) _sessionManager.RemoveSession(payload.session_id);
            }
            catch { }
            return Results.Ok();
        });

        app.MapPost("/api/session/subagent", async (HttpContext ctx) =>
        {
            try
            {
                var payload = await ctx.Request.ReadFromJsonAsync<SubagentPayload>();
                if (payload != null) _sessionManager.HandleSubagent(payload);
            }
            catch { }
            return Results.Ok();
        });
    }

    public void Dispose()
    {
        if (_app != null)
        {
            // Must run on thread pool — calling GetResult()/Wait() on the WPF
            // dispatcher thread deadlocks because StopAsync continuations need
            // the dispatcher, which is blocked. Task.Run has no sync context,
            // so awaits resume on the thread pool and can't deadlock.
            Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await _app.StopAsync(cts.Token); } catch { }
                try { await _app.DisposeAsync(); } catch { }
            }).Wait(TimeSpan.FromSeconds(3));
        }
    }
}
