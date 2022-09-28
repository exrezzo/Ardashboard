module F.Core.EmailService

open System
open System.Data.SQLite
open System.IO
open System.Text.RegularExpressions
open System.Web
open Ardashboard.Infrastructure
open FSharp.Data
open RepoDb

type HtmlBankMsg = { Id: string; HtmlBody: string }

module BankTransaction =
    type BankTransaction =
        { Amount: double
          Place: string
          Occurred: DateTime }

    let private getTextFromHtmlNode (html: HtmlNode) =
        let htmlDecode =
            HttpUtility.HtmlDecode(html.InnerText())

        Regex.Replace(htmlDecode, @"\s+", " ").Trim()

    let fromHtmlBankMsg (htmlBankMsg: HtmlBankMsg) =
        let table =
            HtmlDocument
                .Load(new StringReader(htmlBankMsg.HtmlBody))
                .CssSelect "table.tabellaDisposizione"
            |> Seq.head

        let transactionKeyValues =
            table.Descendants["tr"]
            |> Seq.map (fun tableRow ->
                (tableRow.Descendants["th"]
                 |> Seq.head
                 |> getTextFromHtmlNode,
                 tableRow.Descendants["td"]
                 |> Seq.head
                 |> getTextFromHtmlNode))

        let mutable amount = 0.0
        let mutable place = ""
        let mutable date = DateTime.Now
        let mutable hour = TimeOnly.MinValue

        transactionKeyValues
        |> Seq.iter (fun (row) ->
            match row with
            | ("Importo", value) -> amount <- Double.Parse(value.Replace("euro", "").Trim())
            | ("Presso", value) -> place <- value
            | ("Data", value) -> date <- DateTime.Parse value
            | ("Ora", value) -> hour <- TimeOnly.Parse value
            | _ -> failwith "Unknown header in table for transaction")


        { Amount = amount
          Place = place
          Occurred = date + hour.ToTimeSpan() }


module GmailServiceModule =
    open System.Threading
    open Google.Apis.Auth.OAuth2
    open Google.Apis.Gmail.v1
    open Google.Apis.Gmail.v1.Data
    open Google.Apis.Services
    open Google.Apis.Util.Store
    open Newtonsoft.Json.Linq

    let private gmailUserCredentials (clientId: string) (clientSecret: string) =
        GoogleWebAuthorizationBroker.AuthorizeAsync(
            ClientSecrets(ClientId = clientId, ClientSecret = clientSecret),
            [| GmailService.Scope.GmailReadonly |],
            "user",
            CancellationToken.None,
            FileDataStore("token.json", true)
        )
        |> Async.AwaitTask

    let credentialsFromSecretsFile =
        let jsonObj = File.ReadAllText "secrets.json" |> JObject.Parse
        let clientId = jsonObj.SelectToken "GmailUserCredentials.ClientId" |> string
        let clientSecret = jsonObj.SelectToken "GmailUserCredentials.ClientSecret" |> string
        (clientId, clientSecret)    // maybe it's an overkill to create an ad-hoc type
    let private ardashBoardUserCredential =
        let (clientId, clientSecret) = credentialsFromSecretsFile
        gmailUserCredentials
            clientId
            clientSecret

    let private getGmailService =
        new GmailService(
            BaseClientService.Initializer(
                HttpClientInitializer =
                    (ardashBoardUserCredential
                     |> Async.RunSynchronously),
                ApplicationName = "Ardashboard"
            )
        )

    let getMessageIds =
        let gms =
            getGmailService.Users.Messages.List("me")

        gms.Q <- "from:webank@webank.it subject:autorizzato pagamento"

        gms.Execute().Messages
        |> List.ofSeq
        |> Seq.map (fun htmlMessage -> htmlMessage.Id)

    let getHtmlBankMessages (messageIds: seq<string>) =
        Seq.map
            (fun messageId ->
                getGmailService
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

module HtmlBankMessageRepository =
    open System.Data
    open Donald

    // todo: the same query for retrieving messages, simplify.
    let ofDataReader (rd: IDataReader) : HtmlBankMsg =
        { Id = rd.ReadString "Id"
          HtmlBody = rd.ReadString "HtmlBody" }

    let messageIds =
        let sql = "select * from HtmlBankMessage"

        use conn =
            new SQLiteConnection "Data Source=db.db"

        let res =
            conn |> Db.newCommand sql |> Db.query ofDataReader

        match res with
        | Ok result' -> result' |> Seq.map (fun msg -> msg.Id)
        | _ -> List.Empty

    let getHtmlBankMessages =
        let sql = "select * from HtmlBankMessage"

        use conn =
            new SQLiteConnection "Data Source=db.db"

        let res =
            conn |> Db.newCommand sql |> Db.query ofDataReader

        match res with
        | Ok result' -> result' |> Seq.ofList
        | _ -> Seq.empty


    let saveHtmlMessages (msgs: seq<HtmlBankMsg>) =
        if not SQLiteBootstrap.IsInitialized then
            SQLiteBootstrap.Initialize()

        use sqLiteConnection =
            new SQLiteConnection("Data Source=db.db")

        msgs
        |> Seq.iter (fun msg ->
            sqLiteConnection.Insert<HtmlBankMessageStored>(HtmlBankMessageStored(Id = msg.Id, HtmlBody = msg.HtmlBody))
            |> ignore)

module EmailServiceModule =
    open System
    open System.Text

    let private decodeHtmlBankMessages (htmlMessages: seq<HtmlBankMsg>) =
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

    let getBankMessages =
        let idsToGetFromApi =
            GmailServiceModule.getMessageIds
            |> Seq.except HtmlBankMessageRepository.messageIds

        let htmlBankMessagesFromStore =
            HtmlBankMessageRepository.getHtmlBankMessages

        htmlBankMessagesFromStore
        |> Seq.toList
        |> List.length
        |> printfn "msgs from store: %i"

        let htmlBankMessagesFromApi =
            GmailServiceModule.getHtmlBankMessages idsToGetFromApi
            |> decodeHtmlBankMessages

        htmlBankMessagesFromApi
        |> Seq.toList
        |> List.length
        |> printfn "msgs from api: %i"

        let saveHtmlMessagesToStore =
            HtmlBankMessageRepository.saveHtmlMessages htmlBankMessagesFromApi
            |> ignore

        htmlBankMessagesFromApi
        |> Seq.append (htmlBankMessagesFromStore)
        |> Seq.map BankTransaction.fromHtmlBankMsg
        |> Seq.sortByDescending (fun bt -> bt.Occurred)