namespace KustoRemoteMcp.Tests.Features.OAuthEndpoints

open System.Collections.Generic
open System.Net.Http
open TickSpec
open global.Xunit
open KustoRemoteMcp
open KustoRemoteMcp.Tests
open KustoRemoteMcp.Tests.EnvVars
open KustoRemoteMcp.Tests.Mocks

[<TickSpec.StepScope(Feature = "OAuth Proxy Endpoints")>]
module Steps =

    type Context =
        { MockResponse: int * string
          MockContext: IDictionary<string, obj>
          StatusCode: int
          ResponseBody: string
          ResponseHeaders: System.Net.Http.Headers.HttpResponseHeaders option
          Client: HttpClient option }

    [<BeforeScenario>]
    let setup () =
        { MockResponse = (200, """{"mock":"default"}""")
          MockContext = Dictionary<string, obj>()
          StatusCode = 0
          ResponseBody = ""
          ResponseHeaders = None
          Client = None }

    let private getOrCreateClient (ctx: Context) =
        match ctx.Client with
        | Some c -> c, ctx
        | None ->
            let mockCtx = ctx.MockContext

            let mockCallOverHttp (formData: IDictionary<string, string>) =
                async {
                    mockCtx.["CapturedFormData"] <- box formData
                    return ctx.MockResponse
                }

            let webApis =
                [ Api.Wiring.WebApi.OAuth.register
                      (Api.Functions.OAuth.register testEntra.ClientId log)
                  Api.Wiring.WebApi.OAuth.authorize
                      (Api.Functions.OAuth.authorize testEntra.TenantId testEntra.ClientId testScopeString log)
                  Api.Wiring.WebApi.OAuth.token
                      (Api.Functions.OAuth.token testEntra.ClientId testEntra.ClientSecret testScopeString mockCallOverHttp log)
                  Api.Wiring.WebApi.OAuth.wellKnownProtectedResource
                      (Api.Functions.OAuth.wellKnownOauthProtectedResource testServer.BaseUrl testScopeArray log)
                  Api.Wiring.WebApi.OAuth.wellKnownAuthServer
                      (Api.Functions.OAuth.wellKnownAuthServer testEntra.TenantId testEntra.ClientId testServer.BaseUrl testScopeArray log) ]

            let client =
                TestServer.createWithoutMcp webApis TestServer.noMiddleware

            client, { ctx with Client = Some client }

    let private doRequest
        (requestFn: HttpClient -> Async<int * string * System.Net.Http.Headers.HttpResponseHeaders * _>)
        (ctx: Context)
        =
        let client, ctx = getOrCreateClient ctx
        let status, body, headers, _ = requestFn client |> Async.RunSynchronously

        { ctx with
            StatusCode = status
            ResponseBody = body
            ResponseHeaders = Some headers }

    let private getCapturedFormData (ctx: Context) =
        match ctx.MockContext.TryGetValue("CapturedFormData") with
        | true, v -> v :?> IDictionary<string, string>
        | _ -> failwith "No form data was captured"

    [<Given>]
    let ``the Entra ID token endpoint will respond with status (\d+) and body``
        (status: int)
        (body: string)
        (ctx: Context)
        =
        let ctx =
            { ctx with
                MockResponse = (status, body.Trim()) }

        let _, ctx = getOrCreateClient ctx
        ctx

    [<Given>]
    let ``the Entra ID token endpoint will capture the forwarded form data`` (ctx: Context) =
        let ctx =
            { ctx with
                MockResponse = (200, """{"access_token":"captured","token_type":"Bearer","expires_in":3600}""") }

        let _, ctx = getOrCreateClient ctx
        ctx

    [<When>]
    let ``a GET request is sent to "(.*)"`` (path: string) (ctx: Context) =
        doRequest (fun client -> HttpClient.get client path) ctx

    [<When>]
    let ``a POST request is sent to "(.*)" with JSON body`` (path: string) (body: string) (ctx: Context) =
        doRequest (fun client -> HttpClient.postJson client path (body.Trim())) ctx

    [<When>]
    let ``a POST form request is sent to "(.*)" with fields`` (path: string) (table: Table) (ctx: Context) =
        let fields = table.Rows |> Array.toList |> List.map (fun row -> row.[0], row.[1])
        doRequest (fun client -> HttpClient.postForm client path fields) ctx

    [<Then>]
    let ``the response status code is (\d+)`` (expected: int) (ctx: Context) = Assert.Equal(expected, ctx.StatusCode)

    [<Then>]
    let ``the response JSON is`` (expectedJson: string) (ctx: Context) =
        JsonAssert.assertJsonEquals (expectedJson.Trim()) (ctx.ResponseBody.Trim())

    [<Then>]
    let ``the response JSON has properties`` (table: Table) (ctx: Context) =
        JsonAssert.assertJsonProperties table.Rows ctx.ResponseBody

    [<Then>]
    let ``the Location header contains "(.*)"`` (substring: string) (ctx: Context) =
        let location =
            match ctx.ResponseHeaders with
            | Some h ->
                match h.Location with
                | null -> ""
                | uri -> uri.ToString()
            | None -> ""

        Assert.True(
            location.Contains(substring),
            sprintf "Location header '%s' does not contain '%s'" location substring
        )

    [<Then>]
    let ``the response body is`` (expectedBody: string) (ctx: Context) =
        Assert.Equal(expectedBody.Trim(), ctx.ResponseBody.Trim())

    [<Then>]
    let ``the forwarded form data has client_id "(.*)"`` (expected: string) (ctx: Context) =
        let formData = getCapturedFormData ctx
        Assert.Equal(expected, formData.["client_id"])

    [<Then>]
    let ``the forwarded form data has client_secret "(.*)"`` (expected: string) (ctx: Context) =
        let formData = getCapturedFormData ctx
        Assert.Equal(expected, formData.["client_secret"])

    [<Then>]
    let ``the forwarded form data does not have key "(.*)"`` (key: string) (ctx: Context) =
        let formData = getCapturedFormData ctx

        Assert.False(formData.ContainsKey(key), sprintf "Form data should not contain key '%s'" key)

module Feature =
    open TickSpec.Xunit

    let Scenarios =
        TickSpecWiring.source.ScenariosFromEmbeddedResource(TickSpecWiring.resourcePrefix + "OAuthEndpoints.feature")
        |> MemberData.ofScenarios

    [<Theory; MemberData("Scenarios")>]
    let Test scenario =
        TickSpecWiring.source.RunScenario scenario
