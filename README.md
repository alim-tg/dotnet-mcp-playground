# McpPlayground

A Swagger-like page for MCP servers hosted in ASP.NET Core. Open it in a browser, paste a key, and you get every tool the server exposes with a form generated from its input schema, a Call button and the highlighted result.

- One embedded HTML page, no CDN, no npm, no dependencies beyond ASP.NET Core.
- Independent of the MCP SDK: it talks plain MCP (JSON-RPC over Streamable HTTP, JSON or SSE responses), so it works with any SDK version or a hand-written server, and adding it never pulls `ModelContextProtocol` or newer `Microsoft.Extensions.*` packages into your app.
- Every call goes through your real MCP endpoint, so your authentication, rate limits and audit apply exactly as they do for Claude Code or any other client. The page itself holds nothing secret.
- Strict CSP with a per-request nonce, `no-store`, `nosniff`, no referrer.
- Targets .NET 8, works on .NET 8, 9 and 10.

## Install

```
dotnet add package McpPlayground
```

## Use

```csharp
using McpPlayground;

app.MapMcp("/mcp");
app.MapMcpPlayground();
```

The page is served at `/mcp/ui` and calls `/mcp`.

### Configure in code

```csharp
app.MapMcpPlayground("/mcp/ui", o =>
{
    o.Title = "orders-mcp";
    o.Description = "Read-only orders. Ask #team for a key.";
    o.McpEndpoint = "/mcp";
    o.KeyStorage = McpPlaygroundKeyStorage.Local;
});
```

### Configure from appsettings

```csharp
app.MapMcpPlayground("/mcp/ui", app.Configuration.GetSection("McpPlayground"));
```

```json
{
  "McpPlayground": {
    "Title": "orders-mcp",
    "Description": "Read-only orders. Ask #team for a key.",
    "AuthHeaderName": "X-Api-Key",
    "AuthScheme": ""
  }
}
```

A configure delegate passed after the section is applied on top of it.

### Options

| Option | Default | Meaning |
|---|---|---|
| `Title` | `MCP Playground` | Page title and header. |
| `Description` | none | Line under the title, e.g. where to get a key. |
| `McpEndpoint` | `/mcp` | MCP endpoint the page calls. A path starting with `/` is resolved against the request's `PathBase`, so it works behind a path prefix. An absolute URL is allowed and added to the CSP `connect-src` (the server must allow CORS for it). |
| `AuthHeaderName` | `Authorization` | Header that carries the key. Empty or `null` hides the key field and connects right away. |
| `AuthScheme` | `Bearer` | Prefix before the key. Empty sends the key alone, e.g. for `X-Api-Key`. |
| `KeyStorage` | `Session` | Where the browser keeps the key: `None`, `Session` (until the tab closes) or `Local` (until cleared). |
| `ProtocolVersion` | `2025-06-18` | MCP protocol version sent in `initialize`. |
| `AllowAnonymous` | `true` | Adds `AllowAnonymous` to the page endpoint so a global fallback policy does not hide it. The MCP endpoint keeps its own auth. Set `false` to protect the page too. |

`MapMcpPlayground` returns the endpoint convention builder, so you can chain anything else, for example `.RequireHost("internal.example.com")` or `.RequireAuthorization("staff")`.

## What the page does

- Connects with `initialize`, lists all tools (following `nextCursor`), and keeps `Mcp-Session-Id` for stateful servers.
- Builds a form per tool: enums and booleans as selects, numbers with min/max, `date` / `date-time` strings, arrays of scalars one value per line, objects and anything else as JSON. Required fields and limits come from the schema. Switch to raw JSON at any time.
- Shows text, image, audio, resource and structured content, the tool error flag, JSON-RPC errors, timing, HTTP status and size, with the raw response one click away.
- Remembers the last arguments per tool for the session and deep-links tools as `#tool=<name>`.
- Keyboard: `/` filters tools, `Ctrl`/`⌘` + `Enter` calls the tool.
- Light and dark themes follow the OS.

## Sample

`samples/Sample.McpServer` is a small MCP server with the playground mapped. Run it and open http://localhost:5099/mcp/ui, key `sample-key`:

```
dotnet run --project samples/Sample.McpServer --urls http://localhost:5099
```

## Releasing

Push a tag `v<semver>` (for example `v1.1.0` or `v1.2.0-beta.1`). The `release` workflow builds, tests and packs that version, publishes it to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) and creates a GitHub release with the packages. One-time setup: a trusted publishing policy on nuget.org for this repository and `release.yml`, a `nuget` environment in the repository settings, and a repository variable `NUGET_USER` with the nuget.org account name.

```
git tag v1.1.0
git push origin v1.1.0
```

## License

MIT, see [LICENSE](LICENSE).
