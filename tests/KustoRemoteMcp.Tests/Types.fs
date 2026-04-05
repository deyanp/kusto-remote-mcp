/// Shared test types: noop logger and test configuration records.
module KustoRemoteMcp.Tests.Types


let log: Framework.Logging.Log =
    { Info = fun _ _ _ -> ()
      Warning = fun _ _ _ -> ()
      Exception = fun _ _ _ _ -> () }

