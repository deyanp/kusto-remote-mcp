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
    let entraIdConfig = EntraIdConfig.fromEnv ()
    let adxConfig = AdxConfig.fromEnv ()
    let serverConfig = ServerConfig.fromEnv ()

    // Wire endpoints and middleware
    let webApis =
        [ Api.Wiring.WebApi.health
          Api.Wiring.WebApi.OAuth.register (OAuthDI.register entraIdConfig)
          Api.Wiring.WebApi.OAuth.authorize (OAuthDI.authorize entraIdConfig adxConfig)
          Api.Wiring.WebApi.OAuth.token (OAuthDI.token entraIdConfig adxConfig)
          Api.Wiring.WebApi.OAuth.wellKnownProtectedResource (OAuthDI.wellKnownOauthProtectedResource adxConfig serverConfig)
          Api.Wiring.WebApi.OAuth.wellKnownAuthServer (OAuthDI.wellKnownAuthServer entraIdConfig adxConfig serverConfig) ]

    let mcpTools = [ Api.Wiring.McpTools.executeKustoQuery adxConfig ]

    let requireBearerToken =
        DependencyInjection.Middleware.createBearerTokenAuth entraIdConfig serverConfig

    // Build and run host
    let builder =
        createDefaultBuilder argv BackgroundServiceExceptionBehavior.StopHost
        |> Framework.Mcp.Hosting.configureMcpServices mcpTools
        |> configureWebHost webApis requireBearerToken (Framework.Mcp.Hosting.mapMcpEndpoints "")

    use tokenSource = new CancellationTokenSource()
    use host = builder.Build()

    printfn "MCP Server running at %s" serverConfig.BaseUrl

    host.RunAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously

    0
