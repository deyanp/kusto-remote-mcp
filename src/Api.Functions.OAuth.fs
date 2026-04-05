/// OAuth proxy endpoints (RFC 7591, 8414, 9728) that delegate authentication to Azure Entra ID.
module KustoRemoteMcp.Api.Functions.OAuth

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Framework.Logging

/// Handles RFC 7591 Dynamic Client Registration by returning a static client registration response.
/// The registration is proxied locally — no actual client is created in Entra ID.
let register (clientId: string) (log: Log) (ctx: HttpContext) =
    async {
        let! body =
            ctx.Request.ReadFromJsonAsync<System.Text.Json.JsonElement>().AsTask()
            |> Async.AwaitTask

        log.Info (7001, "OAuthRegister") "DCR request received" [||]

        let redirectUris =
            match body.TryGetProperty("redirect_uris") with
            | true, uris -> uris.EnumerateArray() |> Seq.map (fun u -> u.GetString()) |> Array.ofSeq
            | _ -> [||]

        let tokenEndpointAuthMethod =
            match body.TryGetProperty("token_endpoint_auth_method") with
            | true, m -> m.GetString()
            | _ -> "client_secret_post"

        // Generate a dummy client_secret — our token proxy injects the real one
        let dummySecret = Guid.NewGuid().ToString("N")

        let response =
            {| client_id = clientId
               client_secret = dummySecret
               client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
               client_secret_expires_at = 0
               grant_types = [| "authorization_code"; "refresh_token" |]
               response_types = [| "code" |]
               token_endpoint_auth_method = tokenEndpointAuthMethod
               redirect_uris = redirectUris |}

        ctx.Response.StatusCode <- 201
        do! ctx.Response.WriteAsJsonAsync(response) |> Async.AwaitTask
    }

/// Proxies the OAuth authorize request to Azure Entra ID.
/// scopes: space-separated scope string (e.g. "https://cluster.kusto.windows.net/.default openid profile offline_access")
let authorize (tenantId: string) (clientId: string) (scopes: string) (log: Log) (ctx: HttpContext) =
    async {
        let query = ctx.Request.Query

        let redirectUri = query.["redirect_uri"].ToString()
        let responseType = query.["response_type"].ToString()
        let state = query.["state"].ToString()
        let codeChallenge = query.["code_challenge"].ToString()
        let codeChallengeMethod = query.["code_challenge_method"].ToString()
        let prompt = query.["prompt"].ToString()

        let entraAuthorizeUrl =
            sprintf "https://login.microsoftonline.com/%s/oauth2/v2.0/authorize" tenantId

        let buildQueryParam (key: string) (value: string) =
            sprintf "%s=%s" (Uri.EscapeDataString(key)) (Uri.EscapeDataString(value))

        let queryParts =
            [ "client_id", clientId
              "response_type",
              (if String.IsNullOrEmpty(responseType) then
                   "code"
               else
                   responseType)
              "redirect_uri", redirectUri
              "scope", scopes
              "state", state
              "response_mode", "query" ]
            @ (if String.IsNullOrEmpty(codeChallenge) then
                   []
               else
                   [ "code_challenge", codeChallenge
                     "code_challenge_method",
                     (if String.IsNullOrEmpty(codeChallengeMethod) then
                          "S256"
                      else
                          codeChallengeMethod) ])
            @ (if String.IsNullOrEmpty(prompt) then
                   []
               else
                   [ "prompt", prompt ])
            |> List.map (fun (k, v) -> buildQueryParam k v)
            |> String.concat "&"

        let targetUrl = sprintf "%s?%s" entraAuthorizeUrl queryParts

        log.Info (7002, "OAuthAuthorize") "Authorize proxy: redirecting to Entra ID" [||]

        ctx.Response.StatusCode <- 302
        ctx.Response.Headers.["Location"] <- StringValues(targetUrl)
    }

/// Proxies the OAuth token request to Azure Entra ID.
/// callOverHttp: injected function that POSTs the form data to the pre-configured Entra token endpoint
/// defaultScopes: space-separated scope string used when the client doesn't provide a scope
let token
    (clientId: string)
    (clientSecret: string)
    (defaultScopes: string)
    (callOverHttp: IDictionary<string, string> -> Async<int * string>)
    (log: Log)
    (ctx: HttpContext)
    =
    async {
        let! form = ctx.Request.ReadFormAsync() |> Async.AwaitTask

        let formData = Dictionary<string, string>() :> IDictionary<string, string>

        for kvp in form do
            // Strip MCP-specific and proxy-replaced params before forwarding to Entra ID:
            // client_id/client_secret are replaced with real values; resource is an MCP concept
            // that conflicts with Entra ID v2.0 scopes (AADSTS9010010).
            if kvp.Key <> "client_secret" && kvp.Key <> "client_id" && kvp.Key <> "resource" then
                formData.[kvp.Key] <- kvp.Value.ToString()

        formData.["client_id"] <- clientId
        formData.["client_secret"] <- clientSecret

        if not (formData.ContainsKey("scope")) || String.IsNullOrEmpty(formData.["scope"]) then
            formData.["scope"] <- defaultScopes

        let grantType =
            if formData.ContainsKey("grant_type") then
                formData.["grant_type"]
            else
                "unknown"

        log.Info (7003, "OAuthToken") "Token proxy: forwarding with grant_type={grantType}" [| grantType |]

        let! statusCode, responseBody = callOverHttp formData

        log.Info (7003, "OAuthToken") "Token proxy: Entra ID responded with status {statusCode}" [| statusCode |]

        ctx.Response.StatusCode <- statusCode
        ctx.Response.ContentType <- "application/json"
        do! ctx.Response.WriteAsync(responseBody) |> Async.AwaitTask
    }

/// Returns RFC 9728 OAuth protected resource metadata.
let wellKnownOauthProtectedResource (baseUrl: string) (scopes: string[]) (log: Log) (ctx: HttpContext) =
    async {
        let response =
            {| resource = sprintf "%s/mcp" baseUrl
               authorization_servers = [| baseUrl |]
               scopes = scopes |}

        ctx.Response.StatusCode <- 200
        do! ctx.Response.WriteAsJsonAsync(response) |> Async.AwaitTask

        log.Info (7004, "OAuthProtectedResource") "Protected resource metadata returned" [||]
    }

/// Returns RFC 8414 OAuth authorization server metadata.
let wellKnownAuthServer
    (tenantId: string)
    (clientId: string)
    (baseUrl: string)
    (scopesSupported: string[])
    (log: Log)
    (ctx: HttpContext)
    =
    async {
        let issuer = sprintf "https://login.microsoftonline.com/%s/v2.0" tenantId

        let authEndpoint = sprintf "%s/oauth/authorize" baseUrl
        let tokenEndpoint = sprintf "%s/oauth/token" baseUrl

        let response =
            {| issuer = issuer
               authorization_endpoint = authEndpoint
               token_endpoint = tokenEndpoint
               registration_endpoint = sprintf "%s/oauth/register" baseUrl
               client_id = clientId
               scopes_supported = scopesSupported
               response_types_supported = [| "code" |]
               response_modes_supported = [| "query"; "fragment"; "form_post" |]
               grant_types_supported = [| "authorization_code"; "refresh_token" |]
               code_challenge_methods_supported = [| "S256" |]
               token_endpoint_auth_methods_supported = [| "client_secret_post"; "client_secret_basic"; "none" |] |}

        ctx.Response.StatusCode <- 200
        do! ctx.Response.WriteAsJsonAsync(response) |> Async.AwaitTask

        log.Info (7005, "OAuthAuthServer") "Authorization server metadata returned" [||]
    }
