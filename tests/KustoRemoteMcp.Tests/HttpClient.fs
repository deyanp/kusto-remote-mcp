/// HTTP client wrappers for test requests.
module KustoRemoteMcp.Tests.HttpClient

open System.Net.Http
open System.Text

let get (client: HttpClient) (path: string) =
    async {
        let! response = client.GetAsync(path) |> Async.AwaitTask
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return int response.StatusCode, body, response.Headers, response.Content.Headers
    }

let getWithAuth (client: HttpClient) (path: string) (authHeader: string) =
    async {
        let request = new HttpRequestMessage(HttpMethod.Get, path)

        if not (System.String.IsNullOrEmpty(authHeader)) then
            request.Headers.TryAddWithoutValidation("Authorization", authHeader) |> ignore

        let! response = client.SendAsync(request) |> Async.AwaitTask
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return int response.StatusCode, body, response.Headers, response.Content.Headers
    }

let postJson (client: HttpClient) (path: string) (body: string) =
    async {
        let content = new StringContent(body, Encoding.UTF8, "application/json")
        let! response = client.PostAsync(path, content) |> Async.AwaitTask
        let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return int response.StatusCode, responseBody, response.Headers, response.Content.Headers
    }

let postForm (client: HttpClient) (path: string) (fields: (string * string) list) =
    async {
        let content =
            new FormUrlEncodedContent(fields |> List.map System.Collections.Generic.KeyValuePair)

        let! response = client.PostAsync(path, content) |> Async.AwaitTask
        let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return int response.StatusCode, responseBody, response.Headers, response.Content.Headers
    }
