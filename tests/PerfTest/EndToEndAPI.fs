namespace PerfTest

open System
open System.Net.Http
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Oxpecker
open Oxpecker.ViewEngine

// Test data models
module EndToEndTypes =
    type Contact = {
        Id: int
        Name: string
        Email: string
        Phone: string
        CreatedAt: DateTime
    }

    type WeatherForecast = {
        Date: DateOnly
        TemperatureC: int
        TemperatureF: int
        Summary: string
    }

    type ApiError = {
        Code: int
        Message: string
        Details: string option
    }

    // Test data generation
    let generateContacts count = [|
        for i in 1..count do
            {
                Id = i
                Name = $"Contact {i}"
                Email = $"contact{i}@example.com"
                Phone = $"555-{1000 + i}"
                CreatedAt = DateTime.UtcNow.AddDays(-float i)
            }
    |]

    let generateWeatherForecasts count =
        let summaries = [|
            "Freezing"
            "Bracing"
            "Chilly"
            "Cool"
            "Mild"
            "Warm"
            "Balmy"
            "Hot"
            "Sweltering"
            "Scorching"
        |]
        [|
            for i in 0 .. count - 1 do
                let tempC = Random.Shared.Next(-20, 55)
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(float i))
                    TemperatureC = tempC
                    TemperatureF = 32 + (int(float tempC / 0.5556))
                    Summary = summaries.[Random.Shared.Next(summaries.Length)]
                }
        |]

module EndToEndHandlers =
    open EndToEndTypes

    // Test endpoints handlers
    let handleContactsJson (ctx: HttpContext) =
        let contacts = generateContacts 10
        ctx.WriteJson contacts

    let handleWeatherJson (ctx: HttpContext) =
        let forecasts = generateWeatherForecasts 5
        ctx.WriteJson forecasts

    let handleSimpleHtml (ctx: HttpContext) =
        let view =
            html() {
                head() { title() { "Performance Test" } }
                body() {
                    h1() { "Welcome" }
                    p() { "This is a simple HTML response for performance testing." }
                    ul() {
                        for i in 1..10 do
                            li() { $"List item {i}" }
                    }
                }
            }
        ctx.WriteHtmlView view

    let handleComplexHtml (ctx: HttpContext) =
        let contacts = generateContacts 50
        let view =
            html() {
                head() {
                    title() { "Contacts List" }
                    style() {
                        """
                    body { font-family: Arial, sans-serif; margin: 20px; }
                    table { border-collapse: collapse; width: 100%; }
                    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                    th { background-color: #f2f2f2; }
                """
                    }
                }
                body() {
                    h1() { "Contacts Directory" }
                    p() { $"Total contacts: {contacts.Length}" }
                    table() {
                        thead() {
                            tr() {
                                th() { "ID" }
                                th() { "Name" }
                                th() { "Email" }
                                th() { "Phone" }
                                th() { "Created" }
                            }
                        }
                        tbody() {
                            for contact in contacts do
                                tr() {
                                    td() { string contact.Id }
                                    td() { contact.Name }
                                    td() { contact.Email }
                                    td() { contact.Phone }
                                    td() { contact.CreatedAt.ToString("yyyy-MM-dd") }
                                }
                        }
                    }
                }
            }
        ctx.WriteHtmlView view

    let handleRouteParams (id: int) (ctx: HttpContext) =
        let contact = {
            Id = id
            Name = $"Contact {id}"
            Email = $"contact{id}@example.com"
            Phone = $"555-{1000 + id}"
            CreatedAt = DateTime.UtcNow
        }
        ctx.WriteJson contact

    let handleErrorResponse (ctx: HttpContext) =
        ctx.SetStatusCode 404
        let error = {
            Code = 404
            Message = "Resource not found"
            Details = Some "The requested contact does not exist in the system"
        }
        ctx.WriteJson error

    let handleFormPost (ctx: HttpContext) =
        task {
            let form = ctx.BindForm<Contact>()
            return! ctx.WriteJson {| success = true; contact = form |}
        }
        :> Task

    // Define routing for test application
    let endpoints = [
        GET [
            route "/api/contacts" handleContactsJson
            route "/api/weather" handleWeatherJson
            route "/html/simple" handleSimpleHtml
            route "/html/complex" handleComplexHtml
            routef "/api/contacts/{%i}" handleRouteParams
            route "/api/error" handleErrorResponse
        ]
        POST [ route "/api/contacts" handleFormPost ]
    ]

    let createTestServer () =
        let builder =
            WebHostBuilder()
                .ConfigureServices(fun services -> services.AddRouting().AddOxpecker() |> ignore)
                .Configure(fun app -> app.UseRouting().UseOxpecker(endpoints) |> ignore)
        new TestServer(builder)

/// End-to-end API benchmarks measuring complete request/response cycles
/// including routing, middleware, handler execution, and response serialization.
/// These complement micro-benchmarks by testing real-world application scenarios.
[<MemoryDiagnoser>]
[<ShortRunJob>]
type EndToEndApiBenchmarks() =

    let server = EndToEndHandlers.createTestServer()
    let client = server.CreateClient()

    // Benchmark: Simple JSON response (small payload)
    [<Benchmark(Description = "GET /api/weather - Small JSON (5 items)")>]
    member _.GetWeatherJson() =
        task {
            use! response = client.GetAsync("/api/weather")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: Medium JSON response (array of contacts)
    [<Benchmark(Description = "GET /api/contacts - Medium JSON (10 items)")>]
    member _.GetContactsJson() =
        task {
            use! response = client.GetAsync("/api/contacts")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: Simple HTML rendering
    [<Benchmark(Description = "GET /html/simple - Simple HTML page")>]
    member _.GetSimpleHtml() =
        task {
            use! response = client.GetAsync("/html/simple")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: Complex HTML with table (50 rows)
    [<Benchmark(Description = "GET /html/complex - Complex HTML (50 row table)")>]
    member _.GetComplexHtml() =
        task {
            use! response = client.GetAsync("/html/complex")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: Route with parameters
    [<Benchmark(Description = "GET /api/contacts/123 - Route with parameter")>]
    member _.GetContactById() =
        task {
            use! response = client.GetAsync("/api/contacts/123")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: Error response handling
    [<Benchmark(Description = "GET /api/error - Error response (404)")>]
    member _.GetErrorResponse() =
        task {
            use! response = client.GetAsync("/api/error")
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    // Benchmark: POST with form data
    [<Benchmark(Description = "POST /api/contacts - Form data submission")>]
    member _.PostContactForm() =
        task {
            let formData =
                dict [
                    "Id", "999"
                    "Name", "Test Contact"
                    "Email", "test@example.com"
                    "Phone", "555-9999"
                    "CreatedAt", DateTime.UtcNow.ToString("O")
                ]
            use content = new FormUrlEncodedContent(formData)
            use! response = client.PostAsync("/api/contacts", content)
            let! _ = response.Content.ReadAsStringAsync()
            return ()
        }
        :> Task

    interface IDisposable with
        member _.Dispose() =
            client.Dispose()
            server.Dispose()
