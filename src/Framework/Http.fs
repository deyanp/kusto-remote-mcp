/// HTTP utilities: authorization header extraction and form-encoded POST requests.
module Framework.Http

open System.Collections.Generic
open System.Net.Http
open Microsoft.AspNetCore.Http

/// Returns the full Authorization header value from the current HTTP request.
let getAuthorizationHeader (accessor: IHttpContextAccessor) () =
    accessor.HttpContext.Request.Headers["Authorization"].ToString()

let callOverHttp (httpClient: HttpClient) (url: string) (formData: IDictionary<string, string>) : Async<int * string> =
    async {
        let content = new FormUrlEncodedContent(formData)
        let! response = httpClient.PostAsync(url, content) |> Async.AwaitTask
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return int response.StatusCode, body
    }
