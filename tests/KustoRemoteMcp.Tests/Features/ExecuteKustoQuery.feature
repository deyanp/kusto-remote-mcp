Feature: Execute Kusto Query

  Scenario: Simple query returns JSON array with two rows
    Given the Kusto client returns columns and rows
      | State:string | Count:int |
      | TEXAS        | 42        |
      | OHIO         | 17        |
    When the MCP tool execute_kusto_query is called with "StormEvents | summarize Count=count() by State"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"State":"TEXAS","Count":42},{"State":"OHIO","Count":17}]
      """

  Scenario: Query returning empty result set produces empty JSON array
    Given the Kusto client returns columns and rows
      | Name:string |
    When the MCP tool execute_kusto_query is called with "T | where 1 == 0"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      []
      """

  Scenario: Query result with TimeSpan column serializes as culture-invariant string
    Given the Kusto client returns columns and rows
      | Name:string | Duration:timespan |
      | task1       | 01:30:00          |
    When the MCP tool execute_kusto_query is called with "T | project Name, Duration"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"Name":"task1","Duration":"01:30:00"}]
      """

  Scenario: Query result with DBNull skips the property in JSON
    Given the Kusto client returns columns and rows
      | Name:string | Score:int |
      | alice       | 100       |
      | bob         | <null>    |
    When the MCP tool execute_kusto_query is called with "T | project Name, Score"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"Name":"alice","Score":100},{"Name":"bob"}]
      """

  Scenario: Query result with single row and single column
    Given the Kusto client returns columns and rows
      | Total:long |
      | 99999      |
    When the MCP tool execute_kusto_query is called with "T | count"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"Total":99999}]
      """

  Scenario: Query result with boolean column
    Given the Kusto client returns columns and rows
      | Name:string | Active:bool |
      | svc1        | true        |
      | svc2        | false       |
    When the MCP tool execute_kusto_query is called with "T | project Name, Active"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"Name":"svc1","Active":true},{"Name":"svc2","Active":false}]
      """

  Scenario: Query result with double column
    Given the Kusto client returns columns and rows
      | Metric:string | Value:double |
      | latency       | 3.14         |
    When the MCP tool execute_kusto_query is called with "T | project Metric, Value"
    Then the tool result is not an error
    And the tool result text is the JSON
      """
      [{"Metric":"latency","Value":3.14}]
      """

  Scenario: Management command is rejected before execution
    When the MCP tool execute_kusto_query is called with ".drop table T"
    Then the tool result text is the string "Query rejected: Management command is not allowed"

  Scenario: Blocked operator is rejected before execution
    When the MCP tool execute_kusto_query is called with "T | set-or-append Target <| Source"
    Then the tool result text is the string "Query rejected: Operator 'set-or-append' is not allowed"

  Scenario: Syntax exception returns query syntax error
    Given the Kusto client throws a SyntaxException
    When the MCP tool execute_kusto_query is called with "T | take foo"
    Then the tool result text starts with "Query syntax error:"

  Scenario: Semantic exception returns query semantic error
    Given the Kusto client throws a SemanticException
    When the MCP tool execute_kusto_query is called with "T | where X > 0"
    Then the tool result text starts with "Query semantic error:"

  Scenario: Timeout exception returns timeout message
    Given the Kusto client throws a KustoServiceTimeoutException
    When the MCP tool execute_kusto_query is called with "T | take 10"
    Then the tool result text is the string "Query timed out. Try reducing the data range or simplifying the query."

  Scenario: Throttled exception returns throttle message
    Given the Kusto client throws a KustoRequestThrottledException
    When the MCP tool execute_kusto_query is called with "T | take 10"
    Then the tool result text is the string "Query was throttled. Wait a moment before retrying."

  Scenario: Resource limits exception returns limits message
    Given the Kusto client throws a KustoServicePartialQueryFailureLimitsExceededException
    When the MCP tool execute_kusto_query is called with "T | take 10"
    Then the tool result text is the string "Query exceeded resource limits. Try reducing the data range or simplifying the query."
