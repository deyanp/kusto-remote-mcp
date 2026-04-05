Feature: KQL Query Validation

  Scenario: Simple table query passes validation
    Given the KQL query "StormEvents | take 10"
    When the query is validated
    Then validation succeeds

  Scenario: Query with multiple allowed operators passes
    Given the KQL query "StormEvents | where State == 'TEXAS' | summarize count() by State | sort by Count desc"
    When the query is validated
    Then validation succeeds

  Scenario: Query with extend and project passes
    Given the KQL query "StormEvents | extend Duration = EndTime - StartTime | project State, Duration"
    When the query is validated
    Then validation succeeds

  Scenario: Query with join operator passes
    Given the KQL query "T1 | join kind=inner T2 on Key"
    When the query is validated
    Then validation succeeds

  Scenario: Query with mv-expand operator passes
    Given the KQL query "T | mv-expand Column"
    When the query is validated
    Then validation succeeds

  Scenario: Query with make-series operator passes
    Given the KQL query "T | make-series count() on Timestamp step 1h"
    When the query is validated
    Then validation succeeds

  Scenario: Query with union operator passes
    Given the KQL query "union T1, T2 | where Value > 0"
    When the query is validated
    Then validation succeeds

  Scenario: Query with allowed evaluate plugin bag_unpack passes
    Given the KQL query "T | evaluate bag_unpack(DynamicCol)"
    When the query is validated
    Then validation succeeds

  Scenario: Query with allowed evaluate plugin autocluster passes
    Given the KQL query "T | evaluate autocluster()"
    When the query is validated
    Then validation succeeds

  Scenario: Allowed .show tables command passes
    Given the KQL query ".show tables"
    When the query is validated
    Then validation succeeds

  Scenario: Allowed .show table with name passes
    Given the KQL query ".show table StormEvents"
    When the query is validated
    Then validation succeeds

  Scenario: Allowed .show functions command passes
    Given the KQL query ".show functions"
    When the query is validated
    Then validation succeeds

  Scenario: Allowed .show materialized-views command passes
    Given the KQL query ".show materialized-views"
    When the query is validated
    Then validation succeeds

  Scenario: Management command .drop is blocked
    Given the KQL query ".drop table StormEvents"
    When the query is validated
    Then validation fails with "Management command is not allowed"

  Scenario: Management command .create is blocked
    Given the KQL query ".create table NewTable (Col1:string)"
    When the query is validated
    Then validation fails with "Management command is not allowed"

  Scenario: Management command .alter is blocked
    Given the KQL query ".alter table StormEvents (NewCol:int)"
    When the query is validated
    Then validation fails with "Management command is not allowed"

  Scenario: Management command .set is blocked
    Given the KQL query ".set SomeTable <| T | take 10"
    When the query is validated
    Then validation fails with "Management command is not allowed"

  Scenario: externaldata expression is blocked
    Given the KQL query "externaldata(Col1:string) ['https://evil.com/data.csv']"
    When the query is validated
    Then validation fails with "externaldata is not allowed"

  Scenario: external_table function is blocked
    Given the KQL query "external_table('RemoteTable') | take 10"
    When the query is validated
    Then validation fails with "external_table() is not allowed"

  Scenario: Cross-cluster query is blocked
    Given the KQL query "cluster('other.kusto.windows.net').database('db').T | take 10"
    When the query is validated
    Then validation fails with "cross-cluster queries are not allowed"

  Scenario: Cross-database query is blocked
    Given the KQL query "database('otherdb').T | take 10"
    When the query is validated
    Then validation fails with "cross-database queries are not allowed"

  Scenario: Dangerous evaluate plugin python is blocked
    Given the KQL query "T | evaluate python(typeof(*, result:string), script)"
    When the query is validated
    Then validation fails with "Evaluate plugin 'python' is not allowed"

  Scenario: Dangerous evaluate plugin sql_request is blocked
    Given the KQL query "T | evaluate sql_request('connstr', 'SELECT 1')"
    When the query is validated
    Then validation fails with "Evaluate plugin 'sql_request' is not allowed"

  Scenario: Dangerous evaluate plugin http_request is blocked
    Given the KQL query "T | evaluate http_request('https://evil.com')"
    When the query is validated
    Then validation fails with "Evaluate plugin 'http_request' is not allowed"

  Scenario: Multi-statement with one blocked command fails
    Given the KQL query "StormEvents | take 10; .drop table StormEvents"
    When the query is validated
    Then validation fails with "Management command is not allowed"
