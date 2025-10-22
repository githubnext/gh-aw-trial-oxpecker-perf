open Browser
open App
open Oxpecker.Solid
open Oxpecker.Solid.Meta
open Oxpecker.Solid.Router
open Fable.Core.JsInterop

importAll "./index.css"

// Lazy load the About component for code splitting
let LazyAbout() = lazy' (fun () -> importComponent "./About.jsx")

[<SolidComponent>]
let Layout (props: RootProps) : HtmlElement =
    MetaProvider() {
        Title() { "TODO list" }
        Suspense(fallback = p() { "Loading..." }) { props.children }
    }

[<SolidComponent>]
let appRouter() =
    Router(root=Layout) {
        Route(path="/", component'=App)
        Route(path="/about", component'=LazyAbout)
    }

render (appRouter, document.getElementById "root")
