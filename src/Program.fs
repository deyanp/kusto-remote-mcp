/// Application entry point: env var loading, AppEnv construction, host builder composition, and startup.
module KustoRemoteMcp.Program

open System.Threading
open Microsoft.Extensions.Hosting
open Framework.AzureKeyVault.Environment
open Framework.Hosting.HostBuilder

module DI = DependencyInjection

[<EntryPoint>]
let main argv =
    let envVars = Environment.loadEnvironmentVariables () |> Async.RunSynchronously
    let appEnv = AppEnv.AppEnv.create envVars

    // Wire endpoints and middleware
    let webApis =
        [ Api.Wiring.WebApi.health
          Api.Wiring.WebApi.OAuth.register (DI.OAuth.register appEnv)
          Api.Wiring.WebApi.OAuth.authorize (DI.OAuth.authorize appEnv)
          Api.Wiring.WebApi.OAuth.token (DI.OAuth.token appEnv)
          Api.Wiring.WebApi.OAuth.wellKnownProtectedResource (DI.OAuth.wellKnownOauthProtectedResource appEnv)
          Api.Wiring.WebApi.OAuth.wellKnownAuthServer (DI.OAuth.wellKnownAuthServer appEnv) ]

    let mcpTools = [ Api.Wiring.McpTools.executeKustoQuery (DI.McpTools.executeKustoQuery appEnv) ]

    let requireBearerToken = DI.Middleware.createBearerTokenAuth appEnv

    // Build and run host
    let builder =
        createDefaultBuilder argv BackgroundServiceExceptionBehavior.StopHost
        |> Framework.Mcp.Hosting.configureMcpServices mcpTools
        |> configureWebHost webApis requireBearerToken (Framework.Mcp.Hosting.mapMcpEndpoints "")

    use tokenSource = new CancellationTokenSource()
    use host = builder.Build()

    printfn "MCP Server running at %s" appEnv.BaseUrl

    host.RunAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously

    0
