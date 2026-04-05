/// JSON assertion helpers with structural comparison (key-order independent).
module KustoRemoteMcp.Tests.JsonAssert

open System
open System.Text.Json
open Xunit

/// Structural JSON comparison. Objects are compared key-by-key (order-independent).
/// Arrays are compared positionally. Produces clear diff messages with JSON path.
let assertJsonEquals (expected: string) (actual: string) =
    let rec compare (path: string) (exp: JsonElement) (act: JsonElement) =
        match exp.ValueKind, act.ValueKind with
        | JsonValueKind.Object, JsonValueKind.Object ->
            let expProps = [ for p in exp.EnumerateObject() -> p.Name, p.Value ] |> Map.ofList

            let actProps = [ for p in act.EnumerateObject() -> p.Name, p.Value ] |> Map.ofList

            for kvp in expProps do
                match actProps |> Map.tryFind kvp.Key with
                | None -> Assert.Fail(sprintf "At path %s: missing key '%s'. Actual JSON: %s" path kvp.Key actual)
                | Some actVal -> compare (sprintf "%s.%s" path kvp.Key) kvp.Value actVal

            for kvp in actProps do
                if not (expProps |> Map.containsKey kvp.Key) then
                    Assert.Fail(sprintf "At path %s: unexpected key '%s'. Actual JSON: %s" path kvp.Key actual)

        | JsonValueKind.Array, JsonValueKind.Array ->
            let expArr = [ for e in exp.EnumerateArray() -> e ]
            let actArr = [ for e in act.EnumerateArray() -> e ]

            Assert.True(
                expArr.Length = actArr.Length,
                sprintf
                    "At path %s: expected array length %d but got %d. Actual JSON: %s"
                    path
                    expArr.Length
                    actArr.Length
                    actual
            )

            List.iteri2 (fun i e a -> compare (sprintf "%s[%d]" path i) e a) expArr actArr

        | _ ->
            let expStr = exp.GetRawText()
            let actStr = act.GetRawText()

            let msg =
                sprintf "At path %s: expected %s but got %s. Actual JSON: %s" path expStr actStr actual

            Assert.True((expStr = actStr), msg)

    let expDoc = JsonDocument.Parse(expected.Trim())
    let actDoc = JsonDocument.Parse(actual.Trim())
    compare "$" expDoc.RootElement actDoc.RootElement

/// Assert response JSON properties from a Property/Value table.
/// Supports <any> to skip value check and JPath-like array indexing (e.g. "grant_types[0]").
let assertJsonProperties (rows: string[][]) (json: string) =
    let doc = JsonDocument.Parse(json)

    for row in rows do
        let property = row.[0]
        let expectedValue = row.[1]

        if expectedValue <> "<any>" then
            // Simple path resolution supporting "prop[N]" notation
            let rec resolve (element: JsonElement) (parts: string list) =
                match parts with
                | [] -> element
                | part :: rest ->
                    // Check for array indexing like "grant_types[0]"
                    let bracketIdx = part.IndexOf('[')

                    if bracketIdx >= 0 then
                        let propName = part.Substring(0, bracketIdx)
                        let idxStr = part.Substring(bracketIdx + 1, part.Length - bracketIdx - 2)
                        let idx = Int32.Parse(idxStr)

                        let arr =
                            if String.IsNullOrEmpty(propName) then
                                element
                            else
                                element.GetProperty(propName)

                        resolve (arr.[idx]) rest
                    else
                        resolve (element.GetProperty(part)) rest

            let parts = property.Split('.') |> Array.toList
            let element = resolve doc.RootElement parts

            let actualValue =
                match element.ValueKind with
                | JsonValueKind.String -> element.GetString()
                | JsonValueKind.Number -> element.GetRawText()
                | JsonValueKind.True -> "true"
                | JsonValueKind.False -> "false"
                | JsonValueKind.Null -> "<null>"
                | _ -> element.GetRawText()

            let msg =
                sprintf
                    "Property '%s': expected '%s' but got '%s'. Full JSON: %s"
                    property
                    expectedValue
                    actualValue
                    json

            Assert.True((actualValue = expectedValue), msg)
