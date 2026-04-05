namespace KustoRemoteMcp.Tests.Features.BearerTokenMiddleware

open System.Net.Http
open TickSpec
open global.Xunit
open KustoRemoteMcp
open KustoRemoteMcp.Tests
open KustoRemoteMcp.Tests.Mocks
open KustoRemoteMcp.Tests.EnvVars

[<TickSpec.StepScope(Feature = "Bearer Token Middleware")>]
module Steps =

    type Context =
        { JwtToken: string
          StatusCode: int
          ResponseBody: string
          ResponseHeaders: System.Net.Http.Headers.HttpResponseHeaders option
          Client: HttpClient option }

    [<BeforeScenario>]
    let setup () =
        { JwtToken = ""
          StatusCode = 0
          ResponseBody = ""
          ResponseHeaders = None
          Client = None }

    let private getOrCreateClient (ctx: Context) =
        match ctx.Client with
        | Some c -> c, ctx
        | None ->
            let webApis =
                [ Api.Wiring.WebApi.health
                  Api.Wiring.WebApi.OAuth.wellKnownProtectedResource
                      (Api.Functions.OAuth.wellKnownOauthProtectedResource testServer.BaseUrl testScopeArray Mocks.log)
                  Api.Wiring.WebApi.OAuth.wellKnownAuthServer
                      (Api.Functions.OAuth.wellKnownAuthServer
                          testEntra.TenantId testEntra.ClientId testServer.BaseUrl testScopeArray Mocks.log) ]

            let client = TestServer.createWithoutMcp webApis TestServer.requireBearerToken
            client, { ctx with Client = Some client }

    [<Given>]
    let ``a JWT token with claims`` (table: Table) (ctx: Context) =
        let claims =
            table.Rows
            |> Array.toList
            |> List.map (fun row ->
                let claim = row.[0]
                let value = row.[1]

                match System.Int64.TryParse(value) with
                | true, n -> claim, box n
                | _ -> claim, box value)

        { ctx with
            JwtToken = JwtHelper.createToken claims }

    [<When>]
    let ``a GET request is sent to "(.*)" without authorization`` (path: string) (ctx: Context) =
        let client, ctx = getOrCreateClient ctx
        let status, body, headers, _ = HttpClient.get client path |> Async.RunSynchronously

        { ctx with
            StatusCode = status
            ResponseBody = body
            ResponseHeaders = Some headers }

    [<When>]
    let ``a GET request is sent to "(.*)" with authorization "(.*)"`` (path: string) (auth: string) (ctx: Context) =
        let client, ctx = getOrCreateClient ctx

        let status, body, headers, _ =
            HttpClient.getWithAuth client path auth |> Async.RunSynchronously

        { ctx with
            StatusCode = status
            ResponseBody = body
            ResponseHeaders = Some headers }

    [<When>]
    let ``a GET request is sent to "(.*)" with the JWT token`` (path: string) (ctx: Context) =
        let client, ctx = getOrCreateClient ctx
        let auth = sprintf "Bearer %s" ctx.JwtToken

        let status, body, headers, _ =
            HttpClient.getWithAuth client path auth |> Async.RunSynchronously

        { ctx with
            StatusCode = status
            ResponseBody = body
            ResponseHeaders = Some headers }

    [<Then>]
    let ``the response status code is (\d+)`` (expected: int) (ctx: Context) = Assert.Equal(expected, ctx.StatusCode)

    [<Then>]
    let ``the response status code is not (\d+)`` (unexpected: int) (ctx: Context) =
        Assert.NotEqual(unexpected, ctx.StatusCode)

    [<Then>]
    let ``the WWW-Authenticate header is`` (expected: string) (ctx: Context) =
        match ctx.ResponseHeaders with
        | Some h ->
            let values =
                h.WwwAuthenticate |> Seq.map (fun v -> v.ToString()) |> String.concat ", "

            Assert.Equal(expected.Trim(), values)
        | None -> Assert.Fail("No response headers")

module Feature =
    open TickSpec.Xunit

    let Scenarios =
        TickSpecWiring.source.ScenariosFromEmbeddedResource(
            TickSpecWiring.resourcePrefix + "BearerTokenMiddleware.feature"
        )
        |> MemberData.ofScenarios

    [<Theory; MemberData("Scenarios")>]
    let Test scenario =
        TickSpecWiring.source.RunScenario scenario
