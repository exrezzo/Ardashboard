module F.Core.EmailService

open System
open System.Text
open F.Core.Domain

type IHtmlBankMessageStore =
    abstract GetMessageIds: unit -> seq<string>
    abstract GetHtmlBankMessages: seq<string> -> seq<HtmlBankMsg>
    
type IHtmlBankMessageCache =
    abstract GetMessageIds: unit -> seq<string>
    abstract GetHtmlBankMessages: unit -> seq<HtmlBankMsg>
    abstract SaveHtmlBankMessages: seq<HtmlBankMsg> -> unit

    
let decodeHtmlBankMessages (htmlMessages: seq<HtmlBankMsg>) =
    htmlMessages
    |> Seq.map (fun bankMsg -> bankMsg.HtmlBody)
    |> Seq.map (fun (base64stringMsg: string) ->
        base64stringMsg
            .Replace("-", "+")
            .Replace("_", "/")
        |> Convert.FromBase64String
        |> Encoding.UTF8.GetString)
    |> Seq.zip (htmlMessages |> Seq.map (fun msg -> msg.Id))
    |> Seq.map (fun (id, htmlDecoded) -> { Id = id; HtmlBody = htmlDecoded })

let getBankMessages
    (bankMsgStore:IHtmlBankMessageStore)
    (bankMsgCache:IHtmlBankMessageCache) =
    
    let idsToGetFromApi =
        bankMsgStore.GetMessageIds()
        |> Seq.except (bankMsgCache.GetMessageIds())

    let htmlBankMessagesFromCache =
        bankMsgCache.GetHtmlBankMessages()

    htmlBankMessagesFromCache
    |> Seq.toList
    |> List.length
    |> printfn "msgs from cache: %i"

    let htmlBankMessagesFromApi =
        bankMsgStore.GetHtmlBankMessages idsToGetFromApi 
        |> decodeHtmlBankMessages

    htmlBankMessagesFromApi
    |> Seq.toList
    |> List.length
    |> printfn "msgs from api: %i"

    let saveHtmlMessagesToStore =
        bankMsgCache.SaveHtmlBankMessages htmlBankMessagesFromApi
        |> ignore

    htmlBankMessagesFromApi
    |> Seq.append (htmlBankMessagesFromCache)
    |> Seq.map BankTransactionModule.fromHtmlBankMsg
    |> Seq.sortByDescending (fun bt -> bt.Occurred)