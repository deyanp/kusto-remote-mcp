/// Shared TickSpec configuration: assembly step definitions source and embedded resource prefix.
module KustoRemoteMcp.Tests.TickSpecWiring

open TickSpec.Xunit

let source =
    AssemblyStepDefinitionsSource(System.Reflection.Assembly.GetExecutingAssembly())

let resourcePrefix = "KustoRemoteMcp.Tests.Features."
