/// MCP tool implementations: KQL query execution against ADX with per-user token passthrough.
module KustoRemoteMcp.Api.Functions.McpTools

open System
open System.Data
open System.Text.Json
open System.Text.Json.Nodes
open Kusto.Data.Common
open Kusto.Data.Exceptions
open Framework.Logging

let private extractBearerToken (header: string) : Result<string, string> =
    if header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) then
        Ok(header.Substring(7))
    else
        Error "Missing or malformed Authorization header"

let private readResultsAsJson (reader: IDataReader) : JsonObject list =
    [ while reader.Read() do
          let obj = JsonObject()

          for i in 0 .. reader.FieldCount - 1 do
              if not (reader.IsDBNull(i)) then
                  let name = reader.GetName(i)
                  let value = reader.GetValue(i)

                  obj[name] <-
                      match value with
                      | :? TimeSpan as t -> JsonValue.Create(t.ToString("c"))
                      | v -> JsonValue.Create(v)

          obj ]

/// Executes a KQL query against ADX with the user's bearer token.
/// Dependencies are injected as function parameters for testability.
let executeKustoQuery
    (log: Log)
    (getAuthorizationHeader: unit -> string)
    (createAdxClient: string -> ICslQueryProvider)
    (query: string)
    : Async<string> =
    async {
        match Framework.AzureDataExplorer.QueryValidation.validate query with
        | Error msg -> return sprintf "Query rejected: %s" msg
        | Ok() ->
            match extractBearerToken (getAuthorizationHeader ()) with
            | Error msg ->
                log.Warning (7011, "McpExecuteKustoQuery") "Bearer token extraction failed" [||]
                return sprintf "Authentication error: %s" msg
            | Ok userToken ->
                try
                    use client = createAdxClient userToken

                    let props = ClientRequestProperties()
                    props.ClientRequestId <- Guid.NewGuid().ToString()
                    props.SetOption(ClientRequestProperties.OptionServerTimeout, TimeSpan.FromSeconds 30L)

                    use! reader = client.ExecuteQueryAsync(null, query, props) |> Async.AwaitTask

                    let results = readResultsAsJson reader
                    return JsonSerializer.Serialize(results)
                with ex ->
                    let kustoEx =
                        match ex with
                        | :? AggregateException as agg when agg.InnerExceptions.Count = 1 -> agg.InnerExceptions.[0]
                        | _ -> ex

                    log.Exception (7010, "McpExecuteKustoQuery") "Error executing Kusto query" ex [||]

                    match kustoEx with
                    | :? SyntaxException -> return sprintf "Query syntax error: %s" kustoEx.Message
                    | :? SemanticException -> return sprintf "Query semantic error: %s" kustoEx.Message
                    | :? KustoServiceTimeoutException
                    | :? KustoClientTimeoutException ->
                        return "Query timed out. Try reducing the data range or simplifying the query."
                    | :? KustoRequestThrottledException -> return "Query was throttled. Wait a moment before retrying."
                    | :? KustoServicePartialQueryFailureLimitsExceededException ->
                        return "Query exceeded resource limits. Try reducing the data range or simplifying the query."
                    | _ -> return "Error executing query"
    }
