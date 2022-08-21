// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Collections
open Ardashboard
open Ardashboard.EmailService
open Ardashboard.Stores

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

let getHtmlMessages =
    let s = EmailService (BankMessageStore())
    async {
        // let! messages = Async.AwaitTask <| s.GetHtmlBankMessages()
        let! messages = s.GetHtmlBankMessages() |> Async.AwaitTask 
        return messages
    }
    
[<EntryPoint>]
let main argv =
    let message = from "F#" // Call the function
    printfn "Hello world %s" message
    let messages =
        Async.RunSynchronously getHtmlMessages
    let bankTransactions =
        List.ofSeq messages |>
        List.map (fun (msg: HtmlBankMessage) -> msg.ToBankTransaction()) 
    // let bankTransactions = List.map (fun (msg: HtmlBankMessage) -> msg.ToBankTransaction()) htmlBankMessages
    0 // return an integer exit code
    
    
