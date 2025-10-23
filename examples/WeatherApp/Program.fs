open System
open System.IO.Compression
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Oxpecker
open WeatherApp.templates
open WeatherApp.Models
open WeatherApp.templates.shared

let htmlView' f (ctx: HttpContext) =
    f ctx
    |> layout.html ctx
    |> ctx.WriteHtmlView

let getWeatherData (ctx: HttpContext) =
    task {
        // Simulate asynchronous loading to demonstrate long rendering
        do! Task.Delay(500)

        let startDate = DateOnly.FromDateTime(DateTime.Now)
        let summaries = [ "Freezing"; "Bracing"; "Chilly"; "Cool"; "Mild"; "Warm"; "Balmy"; "Hot"; "Sweltering"; "Scorching" ]
        let forecasts =
            [|
                for index in 1..5 do
                    {
                        Date = startDate.AddDays(index)
                        TemperatureC = Random.Shared.Next(-20, 55)
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    }
            |]
        return! ctx.WriteHtmlView(weather.data forecasts)
    } :> Task

let endpoints = [
    GET [
        route "/" <| htmlView' home.html
        route "/counter" <| htmlView' counter.html
        route "/weather" <| htmlView' weather.html
        route "/weatherData" <| getWeatherData
        route "/error" <| htmlView' error.html
    ]
]

let configureApp (appBuilder: WebApplication) =
    if appBuilder.Environment.IsDevelopment() then
        appBuilder.UseDeveloperExceptionPage() |> ignore
    else
        appBuilder.UseExceptionHandler("/error", true) |> ignore
    appBuilder
        .UseResponseCompression()
        .UseStaticFiles()
        .UseAntiforgery()
        .UseRouting()
        .UseOxpecker(endpoints) |> ignore

let configureServices (services: IServiceCollection) =
    services
        .AddRouting()
        .AddLogging(fun builder -> builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning) |> ignore)
        .AddAntiforgery()
        .AddResponseCompression(fun options ->
            // Enable compression for all MIME types (default only compresses specific types)
            options.EnableForHttps <- true
            // Add Brotli compression provider (more efficient than Gzip)
            options.Providers.Add<BrotliCompressionProvider>()
            options.Providers.Add<GzipCompressionProvider>())
        // Configure Brotli for maximum compression (trades CPU for smaller size)
        .Configure<BrotliCompressionProviderOptions>(fun (options: BrotliCompressionProviderOptions) ->
            options.Level <- CompressionLevel.Optimal)
        // Configure Gzip as fallback
        .Configure<GzipCompressionProviderOptions>(fun (options: GzipCompressionProviderOptions) ->
            options.Level <- CompressionLevel.Optimal)
        .AddOxpecker()
    |> ignore


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    let app = builder.Build()
    configureApp app
    app.Run()
    0
