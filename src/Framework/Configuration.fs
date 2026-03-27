/// Environment variable access with null-safety wrappers.
module Framework.Configuration

open System

[<RequireQualifiedAccess>]
module Environment =
    let tryGetEnvironmentVariable (name: string) =
        let value = Environment.GetEnvironmentVariable(name)

        if String.IsNullOrWhiteSpace(value) then
            None
        else
            Some value

    let getEnvironmentVariable (name: string) =
        tryGetEnvironmentVariable name
        |> Option.defaultWith (fun () -> failwith $"Missing environment variable %s{name}!")
