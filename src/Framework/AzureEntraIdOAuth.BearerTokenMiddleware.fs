/// ASP.NET middleware that enforces Bearer token presence and structural JWT validation.
module Framework.AzureEntraIdOAuth.BearerTokenMiddleware

open System
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.IdentityModel.Tokens

/// Validates basic JWT structure and claims without cryptographic signature verification.
/// ADX performs full token validation; this is an early-rejection layer to avoid
/// wasting resources on obviously invalid tokens.
let private validateBearerToken (expectedTenantId: string) (token: string) : Result<unit, string> =
    let parts = token.Split('.')

    if parts.Length <> 3 then
        Error "Malformed token"
    else
        try
            let payloadBytes = Base64UrlEncoder.DecodeBytes(parts.[1])
            let payload = Encoding.UTF8.GetString(payloadBytes)
            use doc = JsonDocument.Parse(payload)
            let root = doc.RootElement

            match root.TryGetProperty("exp") with
            | false, _ -> Error "Token missing expiration"
            | true, exp ->
                let expTime = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64())
                let clockSkew = TimeSpan.FromMinutes(5.0)

                if DateTimeOffset.UtcNow > expTime.Add(clockSkew) then
                    Error "Token expired"
                else
                    match root.TryGetProperty("iss") with
                    | false, _ -> Error "Token missing issuer"
                    | true, iss ->
                        let issuer = iss.GetString()

                        if not (issuer.Contains(expectedTenantId)) then
                            Error "Token issuer does not match expected tenant"
                        else
                            Ok()
        with _ ->
            Error "Malformed token"

/// Bearer token middleware with structural validation.
/// Enforces auth for paths matching the given list: entries ending with '/' are treated as
/// prefixes (any path starting with the prefix requires auth); other entries match exactly
/// or as a path prefix (e.g. "/mcp" matches "/mcp" and "/mcp/anything").
/// All other paths pass through without a token check.
/// Returns 401 with WWW-Authenticate + resource_metadata for requests without a valid Bearer token.
/// Validates JWT structure (expiration, tenant) as an early-rejection layer;
/// full cryptographic validation is handled by the downstream resource (e.g. ADX).
let requireBearerToken
    (tenantId: string)
    (realm: string)
    (resourceMetadataUrl: string)
    (enforcePaths: string list)
    (app: IApplicationBuilder)
    =
    app.Use(fun (ctx: HttpContext) (next: Func<Task>) ->
        let path = ctx.Request.Path.Value

        let requiresAuth =
            enforcePaths
            |> List.exists (fun pattern ->
                if pattern.EndsWith("/") then
                    path.StartsWith(pattern)
                else
                    path = pattern || path.StartsWith(pattern + "/"))

        if not requiresAuth then
            next.Invoke()
        else
            let authHeader = ctx.Request.Headers.["Authorization"].ToString()

            if
                String.IsNullOrEmpty(authHeader)
                || not (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            then
                ctx.Response.StatusCode <- 401

                ctx.Response.Headers.["WWW-Authenticate"] <-
                    StringValues(sprintf "Bearer realm=\"%s\", resource_metadata=\"%s\"" realm resourceMetadataUrl)

                Task.CompletedTask
            else
                let token = authHeader.Substring(7)

                match validateBearerToken tenantId token with
                | Ok() -> next.Invoke()
                | Error _ ->
                    ctx.Response.StatusCode <- 401
                    Task.CompletedTask)
    |> ignore
