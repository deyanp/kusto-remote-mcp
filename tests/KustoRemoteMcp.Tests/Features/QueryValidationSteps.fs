namespace KustoRemoteMcp.Tests.Features.QueryValidation

open TickSpec
open global.Xunit

[<TickSpec.StepScope(Feature = "KQL Query Validation")>]
module Steps =

    type Context =
        { Query: string
          Result: Result<unit, string> }

    [<BeforeScenario>]
    let setup () = { Query = ""; Result = Ok() }

    [<Given>]
    let ``the KQL query "(.*)"`` (q: string) (ctx: Context) = { ctx with Query = q }

    [<When>]
    let ``the query is validated`` (ctx: Context) =
        { ctx with
            Result = Framework.AzureDataExplorer.QueryValidation.validate ctx.Query }

    [<Then>]
    let ``validation succeeds`` (ctx: Context) = Assert.Equal(Ok(), ctx.Result)

    [<Then>]
    let ``validation fails with "(.*)"`` (expectedMsg: string) (ctx: Context) =
        match ctx.Result with
        | Error msg -> Assert.Equal(expectedMsg, msg)
        | Ok() -> Assert.Fail(sprintf "Expected validation to fail with '%s' but it succeeded" expectedMsg)

module Feature =
    open TickSpec.Xunit
    open KustoRemoteMcp.Tests

    let Scenarios =
        TickSpecWiring.source.ScenariosFromEmbeddedResource(TickSpecWiring.resourcePrefix + "QueryValidation.feature")
        |> MemberData.ofScenarios

    [<Theory; MemberData("Scenarios")>]
    let Test scenario =
        TickSpecWiring.source.RunScenario scenario
