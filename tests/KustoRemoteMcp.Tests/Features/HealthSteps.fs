namespace KustoRemoteMcp.Tests.Features.Health

open System.Net.Http
open TickSpec
open global.Xunit
open KustoRemoteMcp
open KustoRemoteMcp.Tests

[<TickSpec.StepScope(Feature = "Health Endpoint")>]
module Steps =

    type Context =
        { StatusCode: int
          ResponseBody: string
          Client: HttpClient option }

    [<BeforeScenario>]
    let setup () =
        { StatusCode = 0
          ResponseBody = ""
          Client = None }

    let private getOrCreateClient (ctx: Context) =
        match ctx.Client with
        | Some c -> c, ctx
        | None ->
            let client =
                TestServer.createWithoutMcp [ Api.Wiring.WebApi.health ] TestServer.noMiddleware

            client, { ctx with Client = Some client }

    [<When>]
    let ``a GET request is sent to "(.*)"`` (path: string) (ctx: Context) =
        let client, ctx = getOrCreateClient ctx
        let status, body, _, _ = HttpClient.get client path |> Async.RunSynchronously

        { ctx with
            StatusCode = status
            ResponseBody = body }

    [<Then>]
    let ``the response status code is (\d+)`` (expected: int) (ctx: Context) = Assert.Equal(expected, ctx.StatusCode)

    [<Then>]
    let ``the response body is`` (expectedBody: string) (ctx: Context) =
        Assert.Equal(expectedBody.Trim(), ctx.ResponseBody.Trim())

module Feature =
    open TickSpec.Xunit

    let Scenarios =
        TickSpecWiring.source.ScenariosFromEmbeddedResource(TickSpecWiring.resourcePrefix + "Health.feature")
        |> MemberData.ofScenarios

    [<Theory; MemberData("Scenarios")>]
    let Test scenario =
        TickSpecWiring.source.RunScenario scenario
