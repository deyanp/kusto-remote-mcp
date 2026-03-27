/// Application entry point: Key Vault decryption, host builder composition, and startup.
module Program

open System.Threading
open Microsoft.Extensions.Hosting
open Framework.AzureKeyVault.Environment
open Framework.Hosting.HostBuilder
open Api.Wiring

module McpDI = MCP.DependencyInjection

// this has to be first, otherwise modules initialize with env vars which have not been decrypted yet!
Environment.overwriteEnvironmentVariablesFromKVRef () |> Async.RunSynchronously

let webApis =
    [ WebApi.health

      WebApi.OAuth.register
      WebApi.OAuth.authorize
      WebApi.OAuth.token
      WebApi.OAuth.wellKnownProtectedResource
      WebApi.OAuth.wellKnownAuthServer ]

let mcpTools = [ McpTools.executeKustoQuery ]

[<EntryPoint>]
let main argv =
    let builder =
        createDefaultBuilder argv BackgroundServiceExceptionBehavior.StopHost
        |> McpDI.configureMcpServices mcpTools
        |> configureWebHost webApis McpDI.OAuth.requireBearerToken (Framework.Mcp.Hosting.mapMcpEndpoints "")

    use tokenSource = new CancellationTokenSource()
    use host = builder.Build()

    printfn "MCP Server running at %s" EnvVars.baseUrl

    host.RunAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously

    0
