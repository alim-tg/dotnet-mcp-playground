using System.ComponentModel;
using System.Text.Json;
using McpPlayground;
using ModelContextProtocol.Server;

const string sampleKey = "sample-key";
string[] discoveryMethods = ["initialize", "notifications/initialized", "ping", "tools/list"];

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddMcpServer(o => o.ServerInstructions = "Sample MCP server for trying MCP Playground. Every tool is read-only and returns what it received.")
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<SampleTools>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var isMcp = context.Request.Path.StartsWithSegments("/mcp") && !context.Request.Path.StartsWithSegments("/mcp/ui");
    var authorization = context.Request.Headers.Authorization.ToString();
    if (!isMcp || authorization == $"Bearer {sampleKey}")
    {
        await next();
        return;
    }

    if (authorization.Length == 0 && HttpMethods.IsPost(context.Request.Method))
    {
        context.Request.EnableBuffering();
        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body);
            if (body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty("method", out var method)
                && discoveryMethods.Contains(method.GetString()))
            {
                context.Request.Body.Position = 0;
                await next();
                return;
            }
        }
        catch (JsonException)
        {
        }
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new { error = "Missing or unknown key. Listing the tools is open; calling a tool needs the key." });
});

app.MapMcp("/mcp");
app.MapMcpPlayground("/mcp/ui", app.Configuration.GetSection("McpPlayground"));
app.MapGet("/", () => Results.Redirect("/mcp/ui"));

app.Run();

[McpServerToolType]
public sealed class SampleTools
{
    [McpServerTool(Name = "echo", ReadOnly = true, Idempotent = true), Description("Returns the text unchanged.")]
    public static string Echo([Description("Any text, whitespace is kept.")] string text) => text;

    [McpServerTool(Name = "search_orders", ReadOnly = true, Idempotent = true), Description("Shows how each argument type is rendered and returns the arguments it received.")]
    public static object SearchOrders(
        [Description("Window start, ISO 8601.")] DateTimeOffset since,
        [Description("Window end, ISO 8601.")] DateTimeOffset? until = null,
        [Description("Order status.")] OrderStatus? status = null,
        [Description("Order ids to match.")] long[]? orderIds = null,
        [Description("Include the order items.")] bool includeItems = false,
        [Description("Free-form filter object.")] Dictionary<string, string>? filter = null,
        [Description("Rows to return, 1-50.")] int limit = 20) =>
        new { since, until, status = status?.ToString(), orderIds, includeItems, filter, limit };

    [McpServerTool(Name = "fail", ReadOnly = true), Description("Always fails, to show how tool errors look.")]
    public static string Fail() => throw new InvalidOperationException("This tool always fails.");
}

public enum OrderStatus
{
    Pending,
    Shipped,
    Cancelled
}
