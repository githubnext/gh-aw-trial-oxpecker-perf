namespace PerfTest

open System
open System.Buffers
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open FSharp.UMX

// Real-world payload types based on CRUD example
module RealWorldTypes =
    [<Measure>]
    type private id
    type Id = Guid<id>

    type OrderItem = {
        ProductId: Id
        Amount: uint
        UnitPrice: decimal
    }

    type Order = {
        OrderId: Id
        CustomerId: Id
        Description: string
        Items: OrderItem[]
        Status: string
        TotalAmount: decimal
        CreatedAt: DateTime
        UpdatedAt: DateTime option
        ShippingAddress: string
        BillingAddress: string
        Tags: string[]
    }

    type Product = {
        ProductId: Id
        Name: string
        Description: string
        Quantity: uint
        Price: decimal
        Category: string
        Attributes: Map<string, string>
    }

    type ApiResponse<'T> = {
        Data: 'T
        Success: bool
        Message: string option
        Timestamp: DateTime
    }

    // Generate realistic test data
    let private createOrderItem (productId: Guid) = {
        ProductId = %productId
        Amount = uint(Random.Shared.Next(1, 10))
        UnitPrice = decimal(Random.Shared.Next(10, 1000)) / 10m
    }

    let private createOrder (orderId: Guid) =
        let items =
            Array.init (Random.Shared.Next(1, 5)) (fun _ -> createOrderItem(Guid.NewGuid()))
        {
            OrderId = %orderId
            CustomerId = %(Guid.NewGuid())
            Description = $"Order {orderId.ToString().Substring(0, 8)}"
            Items = items
            Status = [ "Pending"; "Processing"; "Shipped"; "Delivered" ].[Random.Shared.Next(4)]
            TotalAmount = items |> Array.sumBy(fun i -> decimal i.Amount * i.UnitPrice)
            CreatedAt = DateTime.UtcNow.AddDays(-float(Random.Shared.Next(1, 30)))
            UpdatedAt =
                if Random.Shared.Next(2) = 0 then
                    Some DateTime.UtcNow
                else
                    None
            ShippingAddress = "123 Main St, City, State 12345"
            BillingAddress = "123 Main St, City, State 12345"
            Tags = [| "priority"; "express" |]
        }

    let private createProduct (productId: Guid) = {
        ProductId = %productId
        Name = $"Product {productId.ToString().Substring(0, 8)}"
        Description = "High quality product with excellent features and warranty"
        Quantity = uint(Random.Shared.Next(0, 1000))
        Price = decimal(Random.Shared.Next(10, 10000)) / 100m
        Category = [ "Electronics"; "Clothing"; "Books"; "Home" ].[Random.Shared.Next(4)]
        Attributes =
            Map.ofList [
                "color", "blue"
                "size", "medium"
                "brand", "GenericBrand"
                "warranty", "2 years"
            ]
    }

    // Different payload sizes for realistic testing
    let singleOrder = createOrder(Guid.NewGuid())
    let smallOrderList = Array.init 10 (fun _ -> createOrder(Guid.NewGuid()))
    let mediumOrderList = Array.init 100 (fun _ -> createOrder(Guid.NewGuid()))
    let largeOrderList = Array.init 1000 (fun _ -> createOrder(Guid.NewGuid()))

    let singleProduct = createProduct(Guid.NewGuid())
    let productCatalog = Array.init 50 (fun _ -> createProduct(Guid.NewGuid()))

    let apiResponseSingle = {
        Data = singleOrder
        Success = true
        Message = None
        Timestamp = DateTime.UtcNow
    }

    let apiResponseList = {
        Data = smallOrderList
        Success = true
        Message = Some "Orders retrieved successfully"
        Timestamp = DateTime.UtcNow
    }

// STJ configuration for real-world payloads
module STJRealWorld =
    open Oxpecker
    open RealWorldTypes

    let endpoints = [
        GET [
            route "/order/single" <| json singleOrder
            route "/order/small" <| json smallOrderList
            route "/order/medium" <| json mediumOrderList
            route "/order/large" <| json largeOrderList
            route "/product/single" <| json singleProduct
            route "/product/catalog" <| json productCatalog
            route "/api/single" <| json apiResponseSingle
            route "/api/list" <| json apiResponseList
        ]
    ]

    let webApp () =
        let builder =
            WebHostBuilder()
                .UseKestrel()
                .Configure(fun app -> app.UseRouting().UseOxpecker(endpoints) |> ignore)
                .ConfigureServices(fun services -> services.AddRouting().AddOxpecker() |> ignore)
        new TestServer(builder)

// SpanJson configuration for real-world payloads
module SpanJsonRealWorld =
    open Oxpecker
    open SpanJson
    open RealWorldTypes

    type SpanJsonSerializer() =
        interface IJsonSerializer with
            member this.Serialize(value, ctx, chunked) =
                ctx.Response.ContentType <- "application/json; charset=utf-8"
                if chunked then
                    if ctx.Request.Method <> HttpMethods.Head then
                        JsonSerializer.Generic.Utf8.SerializeAsync<_>(value, ctx.Response.Body).AsTask()
                    else
                        Task.CompletedTask
                else
                    task {
                        let buffer = JsonSerializer.Generic.Utf8.SerializeToArrayPool<_>(value)
                        ctx.Response.Headers.ContentLength <- buffer.Count
                        if ctx.Request.Method <> HttpMethods.Head then
                            do! ctx.Response.Body.WriteAsync(buffer)
                        ArrayPool<byte>.Shared.Return(buffer.Array |> Unchecked.nonNull)
                    }

            member this.Deserialize(ctx) = failwith "Not implemented"

    let endpoints = [
        GET [
            route "/order/single" <| json singleOrder
            route "/order/small" <| json smallOrderList
            route "/order/medium" <| json mediumOrderList
            route "/order/large" <| json largeOrderList
            route "/product/single" <| json singleProduct
            route "/product/catalog" <| json productCatalog
            route "/api/single" <| json apiResponseSingle
            route "/api/list" <| json apiResponseList
        ]
    ]

    let webApp () =
        let builder =
            WebHostBuilder()
                .UseKestrel()
                .Configure(fun app -> app.UseRouting().UseOxpecker(endpoints) |> ignore)
                .ConfigureServices(fun services ->
                    services.AddRouting().AddOxpecker().AddSingleton<IJsonSerializer>(SpanJsonSerializer())
                    |> ignore)
        new TestServer(builder)

/// Real-world JSON serialization benchmarks
/// Tests realistic payload sizes and structures based on typical API responses
[<MemoryDiagnoser>]
type JSONRealWorld() =

    let stjServer = STJRealWorld.webApp()
    let spanJsonServer = SpanJsonRealWorld.webApp()
    let stjClient = stjServer.CreateClient()
    let spanJsonClient = spanJsonServer.CreateClient()

    // Single complex object (~500 bytes)
    [<Benchmark>]
    member this.STJ_Order_Single() = stjClient.GetAsync("/order/single")

    [<Benchmark>]
    member this.SpanJson_Order_Single() =
        spanJsonClient.GetAsync("/order/single")

    // Small list (10 items, ~5KB)
    [<Benchmark>]
    member this.STJ_Order_SmallList() = stjClient.GetAsync("/order/small")

    [<Benchmark>]
    member this.SpanJson_Order_SmallList() = spanJsonClient.GetAsync("/order/small")

    // Medium list (100 items, ~50KB)
    [<Benchmark>]
    member this.STJ_Order_MediumList() = stjClient.GetAsync("/order/medium")

    [<Benchmark>]
    member this.SpanJson_Order_MediumList() =
        spanJsonClient.GetAsync("/order/medium")

    // Large list (1000 items, ~500KB)
    [<Benchmark>]
    member this.STJ_Order_LargeList() = stjClient.GetAsync("/order/large")

    [<Benchmark>]
    member this.SpanJson_Order_LargeList() = spanJsonClient.GetAsync("/order/large")

    // Product catalog (50 items with complex types)
    [<Benchmark>]
    member this.STJ_Product_Catalog() = stjClient.GetAsync("/product/catalog")

    [<Benchmark>]
    member this.SpanJson_Product_Catalog() =
        spanJsonClient.GetAsync("/product/catalog")

    // Wrapped API response (common pattern)
    [<Benchmark>]
    member this.STJ_ApiResponse_Single() = stjClient.GetAsync("/api/single")

    [<Benchmark>]
    member this.SpanJson_ApiResponse_Single() = spanJsonClient.GetAsync("/api/single")
