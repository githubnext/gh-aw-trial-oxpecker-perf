module App

open Oxpecker.Solid
open Oxpecker.Solid.Router
open Components

[<SolidComponent>]
let App() : HtmlElement =
    div(){
        TodoList()
        //TodoListStore()
        br()
        br()
        A(href="/about", class'="block text-right") {
            "About"
        }
    }
