/// Generic host builder setup and composable web host configuration with middleware and endpoint hooks.
module Framework.Hosting.HostBuilder

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http

let createDefaultBuilder (argv: string[]) (bgSvcExnBehavior: BackgroundServiceExceptionBehavior) =
    HostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .ConfigureHostConfiguration(fun config ->
            config.AddEnvironmentVariables(prefix = "DOTNET_") |> ignore

            if not (isNull argv) then
                config.AddCommandLine(argv) |> ignore)
        .ConfigureAppConfiguration(fun hostingContext config ->
            config.AddEnvironmentVariables() |> ignore

            if not (isNull argv) then
                config.AddCommandLine(argv) |> ignore)
        .ConfigureLogging(fun hostingContext logging ->
            logging.AddConsole() |> ignore
            logging.AddDebug() |> ignore
            logging.AddEventSourceLogger() |> ignore

            logging.Configure(fun options ->
                options.ActivityTrackingOptions <-
                    ActivityTrackingOptions.SpanId
                    ||| ActivityTrackingOptions.TraceId
                    ||| ActivityTrackingOptions.ParentId)
            |> ignore)
        .UseDefaultServiceProvider(fun context options ->
            let isDevelopment = context.HostingEnvironment.IsDevelopment()
            options.ValidateScopes <- isDevelopment
            options.ValidateOnBuild <- isDevelopment)
        .ConfigureServices(fun services ->
            services.Configure<HostOptions>(fun (hostOptions: HostOptions) ->
                hostOptions.BackgroundServiceExceptionBehavior <- bgSvcExnBehavior)
            |> ignore)

type WebApiDef =
    { Name: string
      Method: HttpMethod
      Path: string
      ExecuteOperation: HttpContext -> Task }

let configureWebHost
    (webApiDefs: WebApiDef list)
    (configureMiddleware: IApplicationBuilder -> unit)
    (configureAdditionalEndpoints: IEndpointRouteBuilder -> unit)
    (builder: IHostBuilder)
    : IHostBuilder =
    builder.ConfigureWebHostDefaults(fun webBuilder ->
        webBuilder.Configure(fun context app ->
            app.UseRouting() |> ignore

            configureMiddleware app

            app.UseEndpoints(fun endpoints ->
                webApiDefs
                |> List.iter (fun webApiDef ->
                    endpoints
                        .MapMethods(webApiDef.Path, [ webApiDef.Method.ToString() ], webApiDef.ExecuteOperation)
                        .AllowAnonymous()
                    |> ignore)

                configureAdditionalEndpoints endpoints)
            |> ignore)

        |> ignore)
