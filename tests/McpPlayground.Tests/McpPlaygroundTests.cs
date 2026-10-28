using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpPlayground.Tests;

public class McpPlaygroundTests
{
    [Fact]
    public async Task ServesThePageWithANonceMatchingTheCsp()
    {
        await using var app = await Start(e => e.MapMcpPlayground());
        var response = await app.GetTestClient().GetAsync("/mcp/ui");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var nonce = Regex.Match(csp, "script-src 'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        Assert.Contains($"<script nonce=\"{nonce}\">", html);
        Assert.Contains($"<style nonce=\"{nonce}\">", html);
        Assert.DoesNotContain("{{", html);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UsesAFreshNonceForEveryRequest()
    {
        await using var app = await Start(e => e.MapMcpPlayground());
        var client = app.GetTestClient();
        var first = (await client.GetAsync("/mcp/ui")).Headers.GetValues("Content-Security-Policy").Single();
        var second = (await client.GetAsync("/mcp/ui")).Headers.GetValues("Content-Security-Policy").Single();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task WritesTheDefaultConfig()
    {
        await using var app = await Start(e => e.MapMcpPlayground());
        var config = await Config(app, "/mcp/ui");

        Assert.Equal("MCP Playground", config.GetProperty("title").GetString());
        Assert.Equal("/mcp", config.GetProperty("mcpUrl").GetString());
        Assert.Equal("Authorization", config.GetProperty("authHeaderName").GetString());
        Assert.Equal("Bearer", config.GetProperty("authScheme").GetString());
        Assert.Equal("session", config.GetProperty("keyStorage").GetString());
        Assert.Equal("2025-06-18", config.GetProperty("protocolVersion").GetString());
        Assert.Equal(JsonValueKind.Null, config.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task ResolvesARelativeEndpointAgainstThePathBase()
    {
        await using var app = await Start(e => e.MapMcpPlayground(), a => a.UsePathBase("/api"));
        var config = await Config(app, "/api/mcp/ui");

        Assert.Equal("/api/mcp", config.GetProperty("mcpUrl").GetString());
    }

    [Fact]
    public async Task AllowsAnAbsoluteEndpointInConnectSrc()
    {
        await using var app = await Start(e => e.MapMcpPlayground("/playground", o => o.McpEndpoint = "https://mcp.example.com/mcp"));
        var response = await app.GetTestClient().GetAsync("/playground");
        var config = await Config(app, "/playground");

        Assert.Contains("connect-src 'self' https://mcp.example.com;", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("https://mcp.example.com/mcp", config.GetProperty("mcpUrl").GetString());
    }

    [Fact]
    public async Task EncodesTitleAndDescription()
    {
        await using var app = await Start(e => e.MapMcpPlayground(o =>
        {
            o.Title = "<b>Orders & {{NONCE}}</b>";
            o.Description = "</script><script>alert(1)</script>";
        }));
        var html = await app.GetTestClient().GetStringAsync("/mcp/ui");
        var config = await Config(app, "/mcp/ui");

        Assert.Contains("<title>&lt;b&gt;Orders &amp; {{NONCE}}&lt;/b&gt;</title>", html);
        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.Equal("</script><script>alert(1)</script>", config.GetProperty("description").GetString());
    }

    [Fact]
    public async Task BindsOptionsFromConfiguration()
    {
        var section = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpPlayground:Title"] = "orders-mcp",
            ["McpPlayground:Description"] = "Ask the team for a key",
            ["McpPlayground:McpEndpoint"] = "/api/mcp",
            ["McpPlayground:AuthHeaderName"] = "X-Api-Key",
            ["McpPlayground:AuthScheme"] = "",
            ["McpPlayground:KeyStorage"] = "Local"
        }).Build().GetSection("McpPlayground");

        await using var app = await Start(e => e.MapMcpPlayground("/ui", section, o => o.ProtocolVersion = "2025-03-26"));
        var config = await Config(app, "/ui");

        Assert.Equal("orders-mcp", config.GetProperty("title").GetString());
        Assert.Equal("Ask the team for a key", config.GetProperty("description").GetString());
        Assert.Equal("/api/mcp", config.GetProperty("mcpUrl").GetString());
        Assert.Equal("X-Api-Key", config.GetProperty("authHeaderName").GetString());
        Assert.Equal(JsonValueKind.Null, config.GetProperty("authScheme").ValueKind);
        Assert.Equal("local", config.GetProperty("keyStorage").GetString());
        Assert.Equal("2025-03-26", config.GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task OmitsTheKeyHeaderWhenAuthIsDisabled()
    {
        await using var app = await Start(e => e.MapMcpPlayground(o => o.AuthHeaderName = null));
        var config = await Config(app, "/mcp/ui");

        Assert.Equal(JsonValueKind.Null, config.GetProperty("authHeaderName").ValueKind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AllowsAnonymousOnlyWhenConfigured(bool allowAnonymous)
    {
        await using var app = await Start(e => e.MapMcpPlayground(o => o.AllowAnonymous = allowAnonymous));
        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(x => x.DisplayName == "MCP Playground");

        Assert.Equal(allowAnonymous, endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null);
    }

    [Fact]
    public void RejectsAnEmptyEndpoint()
    {
        var app = WebApplication.CreateBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapMcpPlayground(configure: o => o.McpEndpoint = " "));
    }

    private static async Task<WebApplication> Start(Action<IEndpointRouteBuilder> map, Action<WebApplication>? before = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        before?.Invoke(app);
        app.UseRouting();
        map(app);
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonElement> Config(WebApplication app, string path)
    {
        var html = await app.GetTestClient().GetStringAsync(path);
        var json = Regex.Match(html, "<script type=\"application/json\" id=\"mcp-playground-config\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
