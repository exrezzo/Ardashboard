module ArdashboardF.EmailServiceF

open System
open System.Data
open System.Data.SQLite
open System.Text
open System.Threading
open Ardashboard.EmailService
open Ardashboard.Stores
open Google.Apis.Auth.OAuth2
open Google.Apis.Gmail.v1
open Google.Apis.Gmail.v1.Data
open Google.Apis.Services
open Google.Apis.Util.Store
open Donald

type HtmlBankMsgType = { Id: string; HtmlBody: string }

module HtmlBankMsg =
    let ofDataReader (rd: IDataReader) : HtmlBankMsgType =
        { Id = rd.ReadString "Id"
          HtmlBody = rd.ReadString "HtmlBody" }

let gmailUserCredentials (clientId: string) (clientSecret: string) =
    GoogleWebAuthorizationBroker.AuthorizeAsync(
        ClientSecrets(ClientId = clientId, ClientSecret = clientSecret),
        [| GmailService.Scope.GmailReadonly |],
        "user",
        CancellationToken.None,
        FileDataStore("token.json", true)
    )
    |> Async.AwaitTask

let ardashBoardUserCredential =
    gmailUserCredentials
        "1054055296797-649vhgjueq528r64fh4b9nhvsscst1ks.apps.googleusercontent.com"
        "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"

let getGmailService =
    new GmailService(
        BaseClientService.Initializer(
            HttpClientInitializer =
                (ardashBoardUserCredential
                 |> Async.RunSynchronously),
            ApplicationName = "Ardashboard"
        )
    )


let messageIds =
    let gms =
        getGmailService.Users.Messages.List("me")

    gms.Q <- "from:webank@webank.it subject:autorizzato pagamento"

    gms.Execute().Messages
    |> List.ofSeq
    |> Seq.map (fun htmlMessage -> htmlMessage.Id)

// todo: the same query for retrieving messages, simplify.
let messageIdsOfStored =
    let sql = "select * from HtmlBankMessage"

    use conn =
        new SQLiteConnection "Data Source=db.db"

    let res =
        conn
        |> Db.newCommand sql
        |> Db.query HtmlBankMsg.ofDataReader

    match res with
    | Ok result' -> result' |> Seq.map (fun msg -> msg.Id)
    | _ -> List.Empty

let getHtmlBankMessagesFromApi (gmailService: GmailService) (messageIds: seq<string>) =
    Seq.map
        (fun messageId ->
            gmailService
                .Users
                .Messages
                .Get("me", messageId)
                .Execute())
        messageIds
    |> Seq.map (fun msg -> msg.Payload.Parts |> List.ofSeq |> Seq.ofList)
    |> Seq.map (fun (parts: seq<MessagePart>) ->
        Seq.map (fun (part: MessagePart) -> part.Body.Data) parts
        |> String.concat " ")
    |> Seq.zip messageIds
    |> Seq.map (fun (id: string, html: string) -> { Id = id; HtmlBody = html })
let getHtmlBankMessagesFromStore =
    let sql = "select * from HtmlBankMessage"

    use conn =
        new SQLiteConnection "Data Source=db.db"

    let res =
        conn
        |> Db.newCommand sql
        |> Db.query HtmlBankMsg.ofDataReader

    match res with
    | Ok result' -> result' |> Seq.ofList // |> Seq.map (fun msg -> msg.Id)
    | _ -> Seq.empty

let saveHtmlMessagesToStore (msgs: seq<HtmlBankMsgType>) =
    let bankMessageStore = BankMessageStore()
    // let sql =
    //     "insert into HtmlBankMessage (Id, HtmlBody)"
    msgs
    |> Seq.map (fun msg -> bankMessageStore.Save(HtmlBankMessage(Id = msg.Id, HtmlBody = msg.HtmlBody)))
// use conn =
//     new SQLiteConnection "Data Source=db.db"
//
// msgs
// |> Seq.map (fun msg ->
//     let param =
//         [ "Id", SqlType.String (Seq.head msgs).Id
//           "HtmlBody", SqlType.String (Seq.head msgs).HtmlBody ]
//
//     dbCommand conn {
//         cmdText sql
//         cmdParam param
//     }
//     |> Db.exec)
// |> Seq.toList



let decodeHtmlBankMessages (htmlMessages: seq<HtmlBankMsgType>) =
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

let bankMessages =
    let idsToGetFromApi =
        messageIds |> Seq.except messageIdsOfStored

    let htmlBankMessagesFromStore =
        getHtmlBankMessagesFromStore

    htmlBankMessagesFromStore |> Seq.toList |> List.length |> printfn "msgs from store: %i"  
    
    let htmlBankMessagesFromApi =
        getHtmlBankMessagesFromApi getGmailService idsToGetFromApi
        |> decodeHtmlBankMessages

    htmlBankMessagesFromApi |> Seq.toList |> List.length |> printfn "msgs from api: %i"  

    let saveHtmlMessagesToStore =
        saveHtmlMessagesToStore htmlBankMessagesFromApi
        |> Seq.toList

    htmlBankMessagesFromApi
    |> Seq.append (htmlBankMessagesFromStore)


[<EntryPoint>]
let main argv =
    // List.iter (fun msg -> printfn "Ciao %s" msg.Id) (bankMessages |> Seq.toList)
    bankMessages |> Seq.toList |> ignore
    printf "Wow fatto."
    0
