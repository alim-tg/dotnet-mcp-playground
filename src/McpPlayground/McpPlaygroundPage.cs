using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace McpPlayground;

internal sealed partial class McpPlaygroundPage
{
    private const string ResourceName = "McpPlayground.playground.html";

    private static readonly Lazy<string> Template = new(LoadTemplate);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly McpPlaygroundOptions _options;
    private readonly string? _endpointOrigin;

    public McpPlaygroundPage(McpPlaygroundOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.McpEndpoint))
        {
            throw new ArgumentException("McpEndpoint is required.", nameof(options));
        }

        _options = options;
        _endpointOrigin = Uri.TryCreate(options.McpEndpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }

    public Task Render(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var title = string.IsNullOrWhiteSpace(_options.Title) ? "MCP Playground" : _options.Title;
        var config = JsonSerializer.Serialize(new PageConfig(
            title,
            Blank(_options.Description),
            ResolveEndpoint(context.Request),
            Blank(_options.AuthHeaderName),
            Blank(_options.AuthScheme),
            _options.KeyStorage,
            _options.ProtocolVersion), JsonOptions);

        var html = Placeholder().Replace(Template.Value, match => match.Groups[1].Value switch
        {
            "NONCE" => nonce,
            "TITLE" => WebUtility.HtmlEncode(title),
            _ => config
        });

        var response = context.Response;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; img-src data:; media-src data:; " +
            $"connect-src 'self'{(_endpointOrigin == null ? "" : " " + _endpointOrigin)}; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
        return response.WriteAsync(html, context.RequestAborted);
    }

    private string ResolveEndpoint(HttpRequest request) =>
        _endpointOrigin == null && _options.McpEndpoint.StartsWith('/')
            ? request.PathBase.Add(new PathString(_options.McpEndpoint)).ToString()
            : _options.McpEndpoint;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string LoadTemplate()
    {
        using var stream = typeof(McpPlaygroundPage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{(NONCE|TITLE|CONFIG)\}\}")]
    private static partial Regex Placeholder();

    private sealed record PageConfig(
        string Title,
        string? Description,
        string McpUrl,
        string? AuthHeaderName,
        string? AuthScheme,
        McpPlaygroundKeyStorage KeyStorage,
        string ProtocolVersion);
}
