/// Application entry point: Key Vault decryption, host builder composition, and startup.
module KustoRemoteMcp.Program

open System.Threading
open Microsoft.Extensions.Hosting
open Framework.AzureKeyVault.Environment
open Framework.Hosting.HostBuilder
open EnvVars

module OAuthDI = DependencyInjection.OAuth

[<EntryPoint>]
let main argv =
    // Decrypt Key Vault references before reading any env vars
    Environment.overwriteEnvironmentVariablesFromKVRef () |> Async.RunSynchronously

    // Load config
    let entra = EntraIdConfig.fromEnv ()
    let adx = AdxConfig.fromEnv ()
    let server = ServerConfig.fromEnv ()

    // Wire endpoints and middleware
    let webApis =
        [ Api.Wiring.WebApi.health
          Api.Wiring.WebApi.OAuth.register (OAuthDI.register entra)
          Api.Wiring.WebApi.OAuth.authorize (OAuthDI.authorize entra adx)
          Api.Wiring.WebApi.OAuth.token (OAuthDI.token entra adx)
          Api.Wiring.WebApi.OAuth.wellKnownProtectedResource (OAuthDI.wellKnownOauthProtectedResource adx server)
          Api.Wiring.WebApi.OAuth.wellKnownAuthServer (OAuthDI.wellKnownAuthServer entra adx server) ]

    let mcpTools = Api.Wiring.McpTools.create adx

    let requireBearerToken =
        DependencyInjection.Middleware.createBearerTokenAuth entra server

    // Build and run host
    let builder =
        createDefaultBuilder argv BackgroundServiceExceptionBehavior.StopHost
        |> Framework.Mcp.Hosting.configureMcpServices mcpTools
        |> configureWebHost webApis requireBearerToken (Framework.Mcp.Hosting.mapMcpEndpoints "")

    use tokenSource = new CancellationTokenSource()
    use host = builder.Build()

    printfn "MCP Server running at %s" server.BaseUrl

    host.RunAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously

    0
