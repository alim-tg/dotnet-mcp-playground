using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace McpPlayground;

public static class McpPlaygroundEndpointRouteBuilderExtensions
{
    public const string DefaultPattern = "/mcp/ui";

    public static IEndpointConventionBuilder MapMcpPlayground(
        this IEndpointRouteBuilder endpoints,
        string pattern = DefaultPattern,
        Action<McpPlaygroundOptions>? configure = null)
    {
        var options = new McpPlaygroundOptions();
        configure?.Invoke(options);
        return Map(endpoints, pattern, options);
    }

    public static IEndpointConventionBuilder MapMcpPlayground(
        this IEndpointRouteBuilder endpoints,
        Action<McpPlaygroundOptions> configure) =>
        endpoints.MapMcpPlayground(DefaultPattern, configure);

    public static IEndpointConventionBuilder MapMcpPlayground(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        IConfiguration configuration,
        Action<McpPlaygroundOptions>? configure = null)
    {
        var options = new McpPlaygroundOptions();
        configuration.Bind(options);
        configure?.Invoke(options);
        return Map(endpoints, pattern, options);
    }

    private static IEndpointConventionBuilder Map(IEndpointRouteBuilder endpoints, string pattern, McpPlaygroundOptions options)
    {
        var page = new McpPlaygroundPage(options);
        var builder = endpoints.MapGet(pattern, page.Render)
            .WithDisplayName("MCP Playground")
            .ExcludeFromDescription();

        if (options.AllowAnonymous)
        {
            builder.AllowAnonymous();
        }

        return builder;
    }
}
