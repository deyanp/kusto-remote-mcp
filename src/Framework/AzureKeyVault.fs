/// Resolves Azure Key Vault references in environment variables at startup.
module Framework.AzureKeyVault.Environment

open System
open System.Collections
open System.Text.RegularExpressions
open Azure.Identity
open Azure.Security.KeyVault.Secrets

type Async with
    static member map f a = async { let! r = a in return f r }

module Environment =
    let private kvRegex =
        Regex(
            @"(?:@Microsoft.KeyVault\(SecretUri=)(?<keyVaultUri>https:\/\/[^\/]+)\/secrets\/(?<secretKey>.+)\/(?<secretVersion>[a-f0-9]+)?\)",
            RegexOptions.Compiled
        )


    let private resolveValue (value: string) =
        async {
            if not (value.StartsWith("!@Microsoft.KeyVault")) then
                return value
            else
                let m = kvRegex.Match value

                if not m.Success then
                    return
                        failwithf
                            "Could not parse key vault reference %s with regex %s"
                            value
                            (kvRegex.ToString())
                else
                    let client = SecretClient(Uri(m.Groups.["keyVaultUri"].Value), DefaultAzureCredential())
                    let! response = client.GetSecretAsync(m.Groups.["secretKey"].Value, m.Groups.["secretVersion"].Value) |> Async.AwaitTask
                    return response.Value.Value
        }

    let loadEnvironmentVariables () : Async<Map<string, string>> =
            Environment.GetEnvironmentVariables()
            |> Seq.cast<DictionaryEntry>
            |> Seq.map (fun kvp -> kvp.Key :?> string, kvp.Value :?> string)
            |> Seq.map (fun (key, value) -> async {
                let! resolved = resolveValue value
                return key, resolved
            })
            |> Async.Parallel
            |> Async.map Map.ofArray
