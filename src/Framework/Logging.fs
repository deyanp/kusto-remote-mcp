/// Structured logging with event IDs, wrapping ILogger into a composable Log record.
module Framework.Logging

open Microsoft.Extensions.Logging

type Log =
    { Info: (int * string) -> string -> obj[] -> unit
      Warning: (int * string) -> string -> obj[] -> unit
      Exception: (int * string) -> string -> exn -> obj[] -> unit }

let create (logger: ILogger) : Log =
    { Info = fun (id, name) message args -> logger.Log(LogLevel.Information, EventId(id, name), message, args)
      Warning = fun (id, name) message args -> logger.Log(LogLevel.Warning, EventId(id, name), message, args)
      Exception = fun (id, name) message ex args -> logger.Log(LogLevel.Error, EventId(id, name), ex, message, args) }

let createDefault (categoryName: string) : Log =
    let logger =
        LoggerFactory.Create(fun b -> b.AddConsole() |> ignore).CreateLogger(categoryName)

    create logger
