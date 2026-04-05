/// Self-contained mock implementations for IDataReader, ICslQueryProvider, and JWT tokens.
module KustoRemoteMcp.Tests.Mocks

open System
open System.Data
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Kusto.Data.Common
open Microsoft.IdentityModel.Tokens

let log: Framework.Logging.Log =
    { Info = fun _ _ _ -> ()
      Warning = fun _ _ _ -> ()
      Exception = fun _ _ _ _ -> () }


// ---------------------------------------------------------------------------
// MockDataReader
// ---------------------------------------------------------------------------

type MockDataReader(columns: string[], rows: obj[][]) =
    let mutable currentRow = -1

    interface IDataReader with
        member _.Read() =
            currentRow <- currentRow + 1
            currentRow < rows.Length

        member _.FieldCount = columns.Length
        member _.GetName(i) = columns.[i]
        member _.GetValue(i) = rows.[currentRow].[i]

        member _.IsDBNull(i) =
            rows.[currentRow].[i] = box DBNull.Value

        member _.Dispose() = ()
        member _.Close() = ()
        member _.Depth = 0
        member _.GetSchemaTable() = null
        member _.IsClosed = false
        member _.NextResult() = false
        member _.RecordsAffected = 0

        member _.GetBoolean _ = raise (NotImplementedException())
        member _.GetByte _ = raise (NotImplementedException())
        member _.GetBytes(_, _, _, _, _) = raise (NotImplementedException())
        member _.GetChar _ = raise (NotImplementedException())
        member _.GetChars(_, _, _, _, _) = raise (NotImplementedException())
        member _.GetData _ = raise (NotImplementedException())
        member _.GetDataTypeName _ = raise (NotImplementedException())
        member _.GetDateTime _ = raise (NotImplementedException())
        member _.GetDecimal _ = raise (NotImplementedException())
        member _.GetDouble _ = raise (NotImplementedException())
        member _.GetFieldType _ = raise (NotImplementedException())
        member _.GetFloat _ = raise (NotImplementedException())
        member _.GetGuid _ = raise (NotImplementedException())
        member _.GetInt16 _ = raise (NotImplementedException())
        member _.GetInt32 _ = raise (NotImplementedException())
        member _.GetInt64 _ = raise (NotImplementedException())
        member _.GetOrdinal _ = raise (NotImplementedException())
        member _.GetString _ = raise (NotImplementedException())
        member _.GetValues _ = raise (NotImplementedException())

        member _.Item
            with get (_: int): obj = raise (NotImplementedException())

        member _.Item
            with get (_: string): obj = raise (NotImplementedException())

/// Parses a TickSpec table header like "State:string" into column name and typed value parser.
module TableParser =
    let parseColumnHeader (header: string) =
        match header.Split(':') with
        | [| name; typ |] -> name.Trim(), typ.Trim().ToLowerInvariant()
        | _ -> header.Trim(), "string"

    let parseValue (typ: string) (value: string) : obj =
        if value = "<null>" then
            box DBNull.Value
        else
            match typ with
            | "string" -> box value
            | "int" -> box (Int32.Parse(value))
            | "long" -> box (Int64.Parse(value))
            | "bool" -> box (Boolean.Parse(value))
            | "double" -> box (Double.Parse(value))
            | "timespan" -> box (TimeSpan.Parse(value))
            | "datetime" -> box (DateTime.Parse(value))
            | "guid" -> box (Guid.Parse(value))
            | _ -> box value

    let fromTable (headers: string[]) (rows: string[][]) =
        let parsed = headers |> Array.map parseColumnHeader
        let columnNames = parsed |> Array.map fst
        let columnTypes = parsed |> Array.map snd

        let dataRows =
            rows
            |> Array.map (fun row -> row |> Array.mapi (fun i cell -> parseValue columnTypes.[i] cell))

        new MockDataReader(columnNames, dataRows)

// ---------------------------------------------------------------------------
// MockQueryProvider
// ---------------------------------------------------------------------------

type MockQueryProvider(behavior: unit -> IDataReader) =
    interface ICslQueryProvider with
        member _.ExecuteQuery(_databaseName, _query, _properties) = behavior ()

        member _.ExecuteQueryAsync(_databaseName, _query, _properties, _cancellationToken) =
            try
                Task.FromResult(behavior ())
            with ex ->
                Task.FromException<IDataReader>(ex)

        member _.ExecuteQueryV2Async(_databaseName, _query, _properties, _cancellationToken) =
            raise (NotImplementedException())

        member _.DefaultDatabaseName
            with get () = ""
            and set _ = ()

        member _.ExecuteQuery(_query, _properties) = behavior ()
        member _.ExecuteQuery(_query) = behavior ()

    interface IDisposable with
        member _.Dispose() = ()

module MockQueryProvider =
    let returning (reader: IDataReader) = new MockQueryProvider(fun () -> reader)

    let throwing (ex: exn) = new MockQueryProvider(fun () -> raise ex)

// ---------------------------------------------------------------------------
// JwtHelper
// ---------------------------------------------------------------------------

module JwtHelper =
    let createToken (claims: (string * obj) list) : string =
        let header = """{"alg":"none","typ":"JWT"}"""

        let claimsJson =
            let obj = new System.Text.Json.Nodes.JsonObject()

            for key, value in claims do
                match value with
                | :? int64 as v -> obj.[key] <- System.Text.Json.Nodes.JsonValue.Create(v)
                | :? int as v -> obj.[key] <- System.Text.Json.Nodes.JsonValue.Create(v)
                | :? string as v -> obj.[key] <- System.Text.Json.Nodes.JsonValue.Create(v)
                | _ -> obj.[key] <- System.Text.Json.Nodes.JsonValue.Create(value.ToString())

            obj.ToJsonString()

        let encode (s: string) =
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(s))

        sprintf "%s.%s.nosig" (encode header) (encode claimsJson)
