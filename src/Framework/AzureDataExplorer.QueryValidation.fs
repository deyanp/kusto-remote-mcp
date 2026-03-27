/// Whitelist-based KQL query validation to block dangerous operators, plugins, and management commands.
module Framework.AzureDataExplorer.QueryValidation

open System.Text.RegularExpressions

/// Tabular operators allowed after '|' in KQL queries.
let private allowedTabularOperators =
    set
        [ "as"
          "consume"
          "count"
          "datatable"
          "distinct"
          "evaluate"
          "extend"
          "facet"
          "filter"
          "find"
          "fork"
          "getschema"
          "invoke"
          "join"
          "limit"
          "lookup"
          "make-series"
          "mv-apply"
          "mv-expand"
          "order"
          "parse"
          "parse-where"
          "partition"
          "print"
          "project"
          "project-away"
          "project-keep"
          "project-rename"
          "project-reorder"
          "range"
          "reduce"
          "render"
          "sample"
          "sample-distinct"
          "scan"
          "search"
          "serialize"
          "sort"
          "summarize"
          "take"
          "top"
          "top-hitters"
          "top-nested"
          "union"
          "where" ]

/// Evaluate plugins that are safe to execute.
/// Excludes: python, r, sql_request, http_request, http_request_post,
/// cosmosdb_sql_request, mysql_request, postgresql_request
let private allowedEvaluatePlugins =
    set
        [ "autocluster"
          "bag_unpack"
          "basket"
          "dcount_intersect"
          "diffpatterns"
          "diffpatterns_text"
          "infer_storage_schema"
          "ipv4_lookup"
          "ipv6_lookup"
          "narrow"
          "pivot"
          "preview"
          "rolling_percentile"
          "rows_near"
          "schema_merge"
          "sequence_detect"
          "session_count"
          "sliding_window_counts" ]

let private pipeOperatorPattern =
    Regex(@"\|\s*([a-z][-a-z]*)", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

let private evaluatePluginPattern =
    Regex(@"\bevaluate\s+([a-z0-9_]+)", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

/// Read-only .show commands allowed for schema discovery.
/// Pattern matches the command prefix after trimming; the rest of the statement is ignored.
let private allowedShowCommands =
    [| ".show tables"
       ".show table"
       ".show functions"
       ".show function"
       ".show materialized-views"
       ".show materialized-view" |]

let private blockedSourcePatterns =
    [ Regex(@"\bexternaldata\b", RegexOptions.IgnoreCase ||| RegexOptions.Compiled), "externaldata is not allowed"
      Regex(@"\bexternal_table\s*\(", RegexOptions.IgnoreCase ||| RegexOptions.Compiled),
      "external_table() is not allowed"
      Regex(@"\bcluster\s*\(", RegexOptions.IgnoreCase ||| RegexOptions.Compiled),
      "cross-cluster queries are not allowed"
      Regex(@"\bdatabase\s*\(", RegexOptions.IgnoreCase ||| RegexOptions.Compiled),
      "cross-database queries are not allowed" ]

/// Validates a KQL query against a whitelist of allowed operators and plugins.
/// Scalar functions (including anomaly detection) are allowed — they execute
/// within whitelisted operators like extend/summarize/project.
let validate (query: string) : Result<unit, string> =
    let statements =
        query.Split(';')
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s.Length > 0)

    let isAllowedShowCommand (statement: string) =
        let lower = statement.ToLowerInvariant()
        allowedShowCommands |> Array.exists (fun cmd -> lower.StartsWith(cmd))

    // Block management commands (starting with '.') except whitelisted .show commands
    let blockedCmd =
        statements
        |> Array.tryFind (fun s -> s.StartsWith(".") && not (isAllowedShowCommand s))

    match blockedCmd with
    | Some _ -> Error "Management command is not allowed"
    | None ->
        // Block dangerous source expressions
        let blockedMatch =
            blockedSourcePatterns |> List.tryFind (fun (regex, _) -> regex.IsMatch(query))

        match blockedMatch with
        | Some(_, msg) -> Error msg
        | None ->
            // Verify all tabular operators after '|' are whitelisted
            let invalidOperator =
                pipeOperatorPattern.Matches(query)
                |> Seq.cast<Match>
                |> Seq.tryFind (fun m ->
                    let op = m.Groups.[1].Value.ToLowerInvariant()
                    not (allowedTabularOperators.Contains(op)))

            match invalidOperator with
            | Some m -> Error(sprintf "Operator '%s' is not allowed" m.Groups.[1].Value)
            | None ->
                // Verify all evaluate plugins are whitelisted
                let invalidPlugin =
                    evaluatePluginPattern.Matches(query)
                    |> Seq.cast<Match>
                    |> Seq.tryFind (fun m ->
                        let plugin = m.Groups.[1].Value.ToLowerInvariant()
                        not (allowedEvaluatePlugins.Contains(plugin)))

                match invalidPlugin with
                | Some m -> Error(sprintf "Evaluate plugin '%s' is not allowed" m.Groups.[1].Value)
                | None -> Ok()
