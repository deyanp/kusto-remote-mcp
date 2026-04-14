namespace KustoRemoteMcp.Tests.Features.ExecuteKustoQuery

open System
open System.Data
open System.Threading.Tasks
open TickSpec
open global.Xunit
open Kusto.Data.Common
open Kusto.Data.Exceptions
open ModelContextProtocol.Client
open ModelContextProtocol.Protocol
open KustoRemoteMcp
open KustoRemoteMcp.Tests
open KustoRemoteMcp.Tests.AppEnvInit
open KustoRemoteMcp.Tests.Mocks

[<TickSpec.StepScope(Feature = "Execute Kusto Query")>]
module Steps =

    type Context =
        { AdxBehavior: (unit -> IDataReader) option
          McpClient: McpClient option
          ToolResult: CallToolResult option }

    [<BeforeScenario>]
    let setup () =
        { AdxBehavior = None
          McpClient = None
          ToolResult = None }

    let private validTestJwt =
        JwtHelper.createToken
            [ "exp", box 9999999999L
              "iss", box (sprintf "https://login.microsoftonline.com/%s/v2.0" appEnv.TenantId) ]

    let private getOrCreateMcpClient (ctx: Context) =
        match ctx.McpClient with
        | Some c -> c, ctx
        | None ->
            let behavior =
                ctx.AdxBehavior
                |> Option.defaultValue (fun () -> new MockDataReader([||], [||]) :> IDataReader)

            let mockQueryProvider = new MockQueryProvider(behavior) :> ICslQueryProvider

            // The tool function returns the query result as a string.
            // Auth header extraction is skipped in tests — the bearer token middleware already
            // validated the token, and the mock ADX client doesn't need a real token.
            let executeKustoQuery (query: string) : Task<string> =
                let getAuthorizationHeader () = sprintf "Bearer %s" validTestJwt
                let createAdxClient (_: string) = mockQueryProvider

                Api.Functions.McpTools.executeKustoQuery Mocks.log getAuthorizationHeader createAdxClient query
                |> Async.StartAsTask

            let mcpToolDef: Framework.Mcp.Hosting.McpServerToolDef =
                { Name = "execute_kusto_query"
                  Description = "Execute KQL query"
                  ReadOnly = true
                  Destructive = false
                  ExecuteOperation = Func<string, Task<string>>(executeKustoQuery) }

            let httpClient =
                TestServer.createWithMcp [] [ mcpToolDef ] TestServer.requireBearerToken

            httpClient.DefaultRequestHeaders.Authorization <-
                System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validTestJwt)

            let transportOptions =
                HttpClientTransportOptions(Endpoint = Uri("http://localhost/mcp"))

            let transport = new HttpClientTransport(transportOptions, httpClient)

            let mcpClient =
                McpClient.CreateAsync(transport) |> Async.AwaitTask |> Async.RunSynchronously

            mcpClient, { ctx with McpClient = Some mcpClient }

    [<Given>]
    let ``the Kusto client returns columns and rows`` (table: Table) (ctx: Context) =
        let reader = TableParser.fromTable table.Header table.Rows :> IDataReader

        { ctx with
            AdxBehavior = Some(fun () -> reader) }

    [<Given>]
    let ``the Kusto client throws a SyntaxException`` (ctx: Context) =
        { ctx with
            AdxBehavior = Some(fun () -> raise (SyntaxException("test syntax error", null :> exn))) }

    [<Given>]
    let ``the Kusto client throws a SemanticException`` (ctx: Context) =
        { ctx with
            AdxBehavior = Some(fun () -> raise (SemanticException("test semantic error", null :> exn))) }

    [<Given>]
    let ``the Kusto client throws a KustoServiceTimeoutException`` (ctx: Context) =
        { ctx with
            AdxBehavior = Some(fun () -> raise (KustoServiceTimeoutException("timeout", null :> exn))) }

    [<Given>]
    let ``the Kusto client throws a KustoRequestThrottledException`` (ctx: Context) =
        { ctx with
            AdxBehavior = Some(fun () -> raise (KustoRequestThrottledException("throttled", null :> exn))) }

    [<Given>]
    let ``the Kusto client throws a KustoServicePartialQueryFailureLimitsExceededException`` (ctx: Context) =
        { ctx with
            AdxBehavior =
                Some(fun () -> raise (KustoServicePartialQueryFailureLimitsExceededException("limits", null :> exn))) }

    [<When>]
    let ``the MCP tool execute_kusto_query is called with "(.*)"`` (query: string) (ctx: Context) =
        let mcpClient, ctx = getOrCreateMcpClient ctx

        let args =
            dict [ "delegateArg0", box query ]
            |> System.Collections.ObjectModel.ReadOnlyDictionary

        let result =
            mcpClient.CallToolAsync("execute_kusto_query", args)
            |> fun vt -> vt.AsTask()
            |> Async.AwaitTask
            |> Async.RunSynchronously

        { ctx with ToolResult = Some result }

    let private getResultText (ctx: Context) =
        let content = ctx.ToolResult.Value.Content |> Seq.head

        match content with
        | :? TextContentBlock as t -> t.Text
        | _ -> failwith "Expected TextContentBlock"

    [<Then>]
    let ``the tool result is not an error`` (ctx: Context) =
        let isError = ctx.ToolResult.Value.IsError.GetValueOrDefault(false)
        Assert.False(isError, sprintf "Expected no error but got: %s" (getResultText ctx))

    [<Then>]
    let ``the tool result text is the JSON`` (expectedJson: string) (ctx: Context) =
        JsonAssert.assertJsonEquals (expectedJson.Trim()) ((getResultText ctx).Trim())

    [<Then>]
    let ``the tool result text is the string "(.*)"`` (expected: string) (ctx: Context) =
        Assert.Equal(expected, getResultText ctx)

    [<Then>]
    let ``the tool result text starts with "(.*)"`` (prefix: string) (ctx: Context) =
        Assert.StartsWith(prefix, getResultText ctx)

module Feature =
    open TickSpec.Xunit
    open KustoRemoteMcp.Tests

    let Scenarios =
        TickSpecWiring.source.ScenariosFromEmbeddedResource(TickSpecWiring.resourcePrefix + "ExecuteKustoQuery.feature")
        |> MemberData.ofScenarios

    [<Theory; MemberData("Scenarios")>]
    let Test scenario =
        TickSpecWiring.source.RunScenario scenario
