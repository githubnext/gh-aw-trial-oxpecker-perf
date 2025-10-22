module About

open Oxpecker.Solid
open Oxpecker.Solid.Meta
open Oxpecker.Solid.Router
open Fable.Core

[<SolidComponent>]
[<ExportDefault>]
let About() : HtmlElement =
    Fragment() {
        Title() { "About" }
        h1() {
            "TodoList example made with Oxpecker.Solid!"
        }
        br()
        br()
        A(href="/", class'="block text-right") {
            "Back"
        }
    }
