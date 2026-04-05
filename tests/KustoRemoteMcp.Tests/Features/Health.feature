Feature: Health Endpoint

  Scenario: Health endpoint returns OK
    When a GET request is sent to "/health"
    Then the response status code is 200
    And the response body is
      """
      OK
      """
