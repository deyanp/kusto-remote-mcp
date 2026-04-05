Feature: OAuth Proxy Endpoints

  Scenario: Well-known authorization server metadata returns correct JSON
    When a GET request is sent to "/.well-known/oauth-authorization-server"
    Then the response status code is 200
    And the response JSON is
      """
      {
        "issuer": "https://login.microsoftonline.com/test-tenant-00000000-0000-0000-0000-000000000000/v2.0",
        "authorization_endpoint": "https://test-mcp.example.com/oauth/authorize",
        "token_endpoint": "https://test-mcp.example.com/oauth/token",
        "registration_endpoint": "https://test-mcp.example.com/oauth/register",
        "client_id": "test-client-id",
        "scopes_supported": ["https://testcluster.kusto.windows.net/.default", "openid", "profile", "offline_access"],
        "response_types_supported": ["code"],
        "response_modes_supported": ["query", "fragment", "form_post"],
        "grant_types_supported": ["authorization_code", "refresh_token"],
        "code_challenge_methods_supported": ["S256"],
        "token_endpoint_auth_methods_supported": ["client_secret_post", "client_secret_basic", "none"]
      }
      """

  Scenario: Well-known OAuth protected resource metadata returns correct JSON
    When a GET request is sent to "/.well-known/oauth-protected-resource"
    Then the response status code is 200
    And the response JSON is
      """
      {
        "resource": "https://test-mcp.example.com/mcp",
        "authorization_servers": ["https://test-mcp.example.com"],
        "scopes": ["https://testcluster.kusto.windows.net/.default", "openid", "profile", "offline_access"]
      }
      """

  Scenario: Dynamic client registration returns 201 with correct structure
    When a POST request is sent to "/oauth/register" with JSON body
      """
      {"redirect_uris": ["http://localhost:3000/callback"], "token_endpoint_auth_method": "none"}
      """
    Then the response status code is 201
    And the response JSON has properties
      | Property                   | Value                          |
      | client_id                  | test-client-id                 |
      | client_secret              | <any>                          |
      | client_id_issued_at        | <any>                          |
      | client_secret_expires_at   | 0                              |
      | grant_types[0]             | authorization_code             |
      | grant_types[1]             | refresh_token                  |
      | response_types[0]          | code                           |
      | token_endpoint_auth_method | none                           |
      | redirect_uris[0]           | http://localhost:3000/callback  |

  Scenario: Dynamic client registration uses default auth method when not specified
    When a POST request is sent to "/oauth/register" with JSON body
      """
      {"redirect_uris": ["http://localhost:3000/callback"]}
      """
    Then the response status code is 201
    And the response JSON has properties
      | Property                   | Value              |
      | token_endpoint_auth_method | client_secret_post |

  Scenario: Authorize endpoint redirects to Entra ID with correct parameters
    When a GET request is sent to "/oauth/authorize?redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcallback&state=abc123&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256"
    Then the response status code is 302
    And the Location header contains "https://login.microsoftonline.com/test-tenant-00000000-0000-0000-0000-000000000000/oauth2/v2.0/authorize"
    And the Location header contains "client_id=test-client-id"
    And the Location header contains "state=abc123"
    And the Location header contains "response_mode=query"
    And the Location header contains "response_type=code"
    And the Location header contains "code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"

  Scenario: Token endpoint proxies Entra ID response
    Given the Entra ID token endpoint will respond with status 200 and body
      """
      {"access_token":"at_123","token_type":"Bearer","expires_in":3600}
      """
    When a POST form request is sent to "/oauth/token" with fields
      | Field      | Value              |
      | grant_type | authorization_code |
      | code       | auth_code_abc      |
    Then the response status code is 200
    And the response body is
      """
      {"access_token":"at_123","token_type":"Bearer","expires_in":3600}
      """

  Scenario: Token endpoint replaces client credentials before forwarding
    Given the Entra ID token endpoint will capture the forwarded form data
    When a POST form request is sent to "/oauth/token" with fields
      | Field         | Value              |
      | grant_type    | authorization_code |
      | code          | auth_code_abc      |
      | client_id     | evil-client-id     |
      | client_secret | evil-secret        |
    Then the forwarded form data has client_id "test-client-id"
    And the forwarded form data has client_secret "test-client-secret"
    And the forwarded form data does not have key "resource"

  Scenario: Token endpoint forwards Entra ID error responses
    Given the Entra ID token endpoint will respond with status 400 and body
      """
      {"error":"invalid_grant","error_description":"Code expired"}
      """
    When a POST form request is sent to "/oauth/token" with fields
      | Field      | Value              |
      | grant_type | authorization_code |
      | code       | expired-code       |
    Then the response status code is 400
    And the response body is
      """
      {"error":"invalid_grant","error_description":"Code expired"}
      """
