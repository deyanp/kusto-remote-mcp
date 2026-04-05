Feature: Bearer Token Middleware

  Scenario: Health endpoint does not require authentication
    When a GET request is sent to "/health" without authorization
    Then the response status code is 200

  Scenario: OAuth discovery endpoint does not require authentication
    When a GET request is sent to "/.well-known/oauth-authorization-server" without authorization
    Then the response status code is 200

  Scenario: OAuth protected resource endpoint does not require authentication
    When a GET request is sent to "/.well-known/oauth-protected-resource" without authorization
    Then the response status code is 200

  Scenario: Request to /mcp without Authorization header returns 401
    When a GET request is sent to "/mcp" without authorization
    Then the response status code is 401
    And the WWW-Authenticate header is
      """
      Bearer realm="MCP Server", resource_metadata="https://test-mcp.example.com/.well-known/oauth-protected-resource"
      """

  Scenario: Request to /mcp subpath without Authorization header returns 401
    When a GET request is sent to "/mcp/sse" without authorization
    Then the response status code is 401

  Scenario: Request with non-Bearer authorization scheme returns 401
    When a GET request is sent to "/mcp" with authorization "Basic dXNlcjpwYXNz"
    Then the response status code is 401

  Scenario: Request with malformed JWT returns 401
    When a GET request is sent to "/mcp" with authorization "Bearer not-a-jwt"
    Then the response status code is 401

  Scenario: Request with expired JWT returns 401
    Given a JWT token with claims
      | Claim | Value                                                                                   |
      | exp   | 1000000000                                                                              |
      | iss   | https://login.microsoftonline.com/test-tenant-00000000-0000-0000-0000-000000000000/v2.0 |
    When a GET request is sent to "/mcp" with the JWT token
    Then the response status code is 401

  Scenario: Request with JWT missing exp claim returns 401
    Given a JWT token with claims
      | Claim | Value                                                                                   |
      | iss   | https://login.microsoftonline.com/test-tenant-00000000-0000-0000-0000-000000000000/v2.0 |
    When a GET request is sent to "/mcp" with the JWT token
    Then the response status code is 401

  Scenario: Request with JWT from wrong tenant returns 401
    Given a JWT token with claims
      | Claim | Value                                                  |
      | exp   | 9999999999                                             |
      | iss   | https://login.microsoftonline.com/wrong-tenant-id/v2.0 |
    When a GET request is sent to "/mcp" with the JWT token
    Then the response status code is 401

  Scenario: Request with JWT missing iss claim returns 401
    Given a JWT token with claims
      | Claim | Value      |
      | exp   | 9999999999 |
    When a GET request is sent to "/mcp" with the JWT token
    Then the response status code is 401

  Scenario: Request with valid JWT to /mcp passes through middleware
    Given a JWT token with claims
      | Claim | Value                                                                                   |
      | exp   | 9999999999                                                                              |
      | iss   | https://login.microsoftonline.com/test-tenant-00000000-0000-0000-0000-000000000000/v2.0 |
    When a GET request is sent to "/mcp" with the JWT token
    Then the response status code is not 401
