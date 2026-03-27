/// Resolves Azure Key Vault references in environment variables at startup.
module Framework.AzureKeyVault.Environment

open System
open System.Collections
open System.Text.RegularExpressions
open Azure.Identity
open Azure.Security.KeyVault.Secrets

module Environment =
    let private kvRegex =
        Regex(
            @"(?:@Microsoft.KeyVault\(SecretUri=)(?<keyVaultUri>https:\/\/[^\/]+)\/secrets\/(?<secretKey>.+)\/(?<secretVersion>[a-f0-9]+)?\)",
            RegexOptions.Compiled
        )

    let private tryGetValueFromKV (keyVaultRefOrSecretValue: string) =
        async {
            if
                not (String.IsNullOrEmpty(keyVaultRefOrSecretValue))
                && keyVaultRefOrSecretValue.Contains("Microsoft.KeyVault")
            then
                let m = kvRegex.Match(keyVaultRefOrSecretValue)

                if m.Success then
                    let keyVaultUri = m.Groups.["keyVaultUri"].Value
                    let secretKey = m.Groups.["secretKey"].Value
                    let secretVersion = m.Groups.["secretVersion"].Value

                    let client = SecretClient(Uri(keyVaultUri), DefaultAzureCredential())

                    let! response = client.GetSecretAsync(secretKey, secretVersion) |> Async.AwaitTask

                    return response.Value.Value
                else
                    return
                        failwithf
                            "Could not parse key vault reference %s with regex %s"
                            keyVaultRefOrSecretValue
                            (kvRegex.ToString())
            else
                return keyVaultRefOrSecretValue
        }

    let overwriteEnvironmentVariablesFromKVRef () : Async<unit> =
        async {
            let entries =
                Environment.GetEnvironmentVariables()
                |> Seq.cast<DictionaryEntry>
                |> Seq.filter (fun kvp -> (kvp.Value :?> string).StartsWith("!@Microsoft.KeyVault"))
                |> Seq.toArray

            for kvp in entries do
                let! decryptedValue = tryGetValueFromKV (kvp.Value :?> string)
                Environment.SetEnvironmentVariable(kvp.Key :?> string, decryptedValue)
        }
