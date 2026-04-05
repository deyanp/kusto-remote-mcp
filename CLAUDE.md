# CLAUDE.md

## Project Overview

KustoRemoteMcp is an F# ASP.NET Core MCP (Model Context Protocol) server that proxies Azure Data Explorer (Kusto) queries with OAuth 2.0 user impersonation. Each user's own Azure Entra ID identity, permissions, and audit trail are preserved — no shared service principal.

## Tech Stack

- **Language**: F# 11, .NET 10.0
- **Web**: ASP.NET Core 10.0
- **MCP**: ModelContextProtocol + ModelContextProtocol.AspNetCore 1.1.0
- **Kusto**: Microsoft.Azure.Kusto.Data 14.0.3
- **Auth**: Azure.Identity, JWT Bearer validation (structural only — ADX does full crypto validation)
- **Secrets**: Azure Key Vault references in env vars (`!@Microsoft.KeyVault(SecretUri=...)`)

## Architecture

### Configuration Pattern

Config is loaded via **record types per concern** with `fromEnv()` factory functions (not module-level statics):

```
EnvVars.fs:  EntraIdConfig / AdxConfig / ServerConfig  (each with fromEnv())
     ↓
DependencyInjection.fs:  McpTools.create(adx) / OAuth.create(entra, adx, server)
     ↓
Api.Wiring.fs:  WebApi.OAuth.create(oauthHandlers) / McpTools.create(executeQuery)
     ↓
Program.fs:  Orchestrates config → DI → wiring → host
```

### Dependency Injection

Uses **partial application** (not a DI container). `DependencyInjection.fs` sub-modules expose `create` functions that take only the config groups they need and return partially-applied handlers as anonymous records.

### API Functions

All API functions (`Api.Functions.OAuth.*`, `Api.Functions.McpTools.*`) accept dependencies as explicit function parameters — making them directly testable without touching DI or env vars.

### Source File Compilation Order (F# is order-dependent)

```
Framework/Configuration.fs → Framework/Http.fs → Framework/Logging.fs →
Framework/AzureKeyVault.fs → Framework/Mcp.Hosting.fs →
Framework/AzureEntraIdOAuth.BearerTokenMiddleware.fs →
Framework/Hosting.HostBuilder.fs → Framework/AzureDataExplorer.QueryValidation.fs →
Api.Functions.OAuth.fs → Api.Functions.McpTools.fs →
EnvVars.fs → DependencyInjection.fs → Api.Wiring.fs → Program.fs
```

## Build & Run

```bash
dotnet build src/KustoRemoteMcp.fsproj
dotnet test tests/KustoRemoteMcp.Tests/KustoRemoteMcp.Tests.fsproj
./dotnet_run.sh              # local dev (no tunnel)
./dotnet_run.sh --mcp true   # local dev with cloudflared tunnel
```

## Testing

### Framework: TickSpec + xUnit (BDD)

Tests use **TickSpec** (Gherkin `.feature` files) with **xUnit** as the test runner.

### Step Definition Pattern

Steps are **module-level functions** (not class members) with immutable context threading:

```fsharp
[<TickSpec.StepScope(Feature = "Feature Name")>]
module Steps.MyFeatureSteps

type Context = { ... }                          // Immutable record per feature

[<BeforeScenario>]
let setup () = { ... }                          // Returns initial context

[<Given>]
let ``some precondition`` (ctx: Context) =      // Takes context as LAST param
    { ctx with Field = newValue }               // Returns modified context

[<When>]
let ``some action "(.*)"`` (param: string) (ctx: Context) =
    { ctx with Result = doSomething param }

[<Then>]
let ``expected outcome`` (ctx: Context) =
    Assert.Equal(expected, ctx.Result)           // Assertions; no return needed
```

### Key Conventions

- `[<StepScope(Feature="...")>]` on the **module** (not a class) — scopes steps to their feature
- Context flows through steps via return values — no mutable state
- `open global.Xunit` (not `open Xunit`) to avoid partial path resolution conflict with `TickSpec.Xunit`
- Feature files are **EmbeddedResources** — resource names are fully qualified: `KustoRemoteMcp.Tests.Features.FeatureName.feature`
- Explicit JSON comparisons use **doc strings** (`"""`) in feature files with structural (key-order-independent) comparison
- For values with embedded quotes (like HTTP headers), use doc strings instead of inline strings to avoid Gherkin escaping issues

### Features.fs Wiring

```fsharp
let source = AssemblyStepDefinitionsSource(Assembly.GetExecutingAssembly())
let scenarios name = source.ScenariosFromEmbeddedResource(prefix + name) |> MemberData.ofScenarios

[<Theory; MemberData("scenarios", "FeatureName.feature")>]
let FeatureName scenario = source.RunScenario scenario
```

### Test Infrastructure

- **TestServerBuilder.fs** — In-process ASP.NET TestServer using `WebHostBuilder` + test-wired OAuth/middleware (bypasses DI and EnvVars entirely)
- **Mocks.fs** — `MockDataReader` (IDataReader), `MockQueryProvider` (ICslQueryProvider), `JwtHelper` (creates structurally valid JWTs)
- **JsonAssert.fs** — Structural JSON comparison (key-order independent, clear path-based diff messages)
- **Client.fs** — HTTP client wrappers returning `(statusCode, body, headers, contentHeaders)` tuples

### Kusto Exception Construction

All Kusto exception types have a `(message: string, innerException: exn)` constructor:
```fsharp
SyntaxException("msg", null :> exn)
SemanticException("msg", null :> exn)
KustoServiceTimeoutException("msg", null :> exn)
KustoRequestThrottledException("msg", null :> exn)
KustoServicePartialQueryFailureLimitsExceededException("msg", null :> exn)
```

## Code Style

- **Functional only** — no OOP constructs, no classes (except for interface implementations like mocks)
- **Partial application** for dependency injection
- **Result type** for errors (`Result<unit, string>` for validation, `Result<string, string>` for token extraction)
- **Async workflows** with `Async.AwaitTask` / `Async.StartAsTask` for ASP.NET interop
- F# anonymous records (`{| ... |}`) for lightweight data transfer between DI and wiring layers
