/// MCP server infrastructure: tool registration, service configuration, and endpoint mapping.
namespace Framework.Mcp

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open ModelContextProtocol.Server

module Hosting =

    /// Definition of an MCP server tool including its metadata and execution delegate.
    type McpServerToolDef =
        { Name: string
          Description: string
          ReadOnly: bool
          Destructive: bool
          ExecuteOperation: Delegate }

    /// Registers MCP server services and tools in the DI container.
    let configureMcpServices (mcpTools: McpServerToolDef list) (builder: IHostBuilder) : IHostBuilder =
        builder.ConfigureServices(fun _ services ->
            services.AddHttpContextAccessor() |> ignore

            let tools =
                mcpTools
                |> List.map (fun toolDef ->
                    let options =
                        McpServerToolCreateOptions(
                            Name = toolDef.Name,
                            Description = toolDef.Description,
                            ReadOnly = Nullable toolDef.ReadOnly,
                            Destructive = Nullable toolDef.Destructive
                        )

                    McpServerTool.Create(toolDef.ExecuteOperation, options))
                |> Array.ofList

            services.AddMcpServer().WithHttpTransport().WithTools(tools) |> ignore)

    /// Endpoint mapping function to pass as configureAdditionalEndpoints to HostBuilder.configureWebHost.
    let mapMcpEndpoints (basePath: string) (endpoints: IEndpointRouteBuilder) =
        endpoints.MapGroup($"%s{basePath}/mcp").MapMcp() |> ignore
