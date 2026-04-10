/// In-process test server builder using IHostBuilder + UseTestServer(), aligned with the platform pattern.
module KustoRemoteMcp.Tests.TestServer

open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Hosting
open Framework.Hosting.HostBuilder
open Framework.Mcp.Hosting
open KustoRemoteMcp.Tests.EnvVars

let requireBearerToken (app: IApplicationBuilder) =
    Framework.AzureEntraIdOAuth.BearerTokenMiddleware.requireBearerToken
        testEntra.TenantId
        "MCP Server"
        (sprintf "%s/.well-known/oauth-protected-resource" testServer.BaseUrl)
        [ "/mcp" ]
        app

let noMiddleware (_: IApplicationBuilder) = ()

let createWithoutMcp (webApis: WebApiDef list) (middleware: IApplicationBuilder -> unit) =
    let hostBuilder =
        createDefaultBuilder Array.empty BackgroundServiceExceptionBehavior.Ignore
        |> configureWebHost webApis middleware (fun _ -> ())
        |> _.ConfigureWebHostDefaults(fun b -> b.UseTestServer() |> ignore)

    let cancellationTokenSource = new CancellationTokenSource()
    let host = hostBuilder.Build()
    host.RunAsync cancellationTokenSource.Token |> Async.AwaitTask |> Async.Start
    host.GetTestClient()

let createWithMcp
    (webApis: WebApiDef list)
    (mcpTools: McpServerToolDef list)
    (middleware: IApplicationBuilder -> unit)
    =
    let hostBuilder =
        createDefaultBuilder Array.empty BackgroundServiceExceptionBehavior.Ignore
        |> configureMcpServices mcpTools
        |> configureWebHost webApis middleware (mapMcpEndpoints "")
        |> _.ConfigureWebHostDefaults(fun b -> b.UseTestServer() |> ignore)

    let cancellationTokenSource = new CancellationTokenSource()
    let host = hostBuilder.Build()
    host.RunAsync cancellationTokenSource.Token |> Async.AwaitTask |> Async.Start
    host.GetTestClient()
