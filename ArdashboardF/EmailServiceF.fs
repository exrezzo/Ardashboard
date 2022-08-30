module ArdashboardF.EmailServiceF


type HtmlBankMsg = { Id: string; HtmlBody: string }

module GmailServiceModule =
    open System.Threading
    open Google.Apis.Auth.OAuth2
    open Google.Apis.Gmail.v1
    open Google.Apis.Gmail.v1.Data
    open Google.Apis.Services
    open Google.Apis.Util.Store

    let private gmailUserCredentials (clientId: string) (clientSecret: string) =
        GoogleWebAuthorizationBroker.AuthorizeAsync(
            ClientSecrets(ClientId = clientId, ClientSecret = clientSecret),
            [| GmailService.Scope.GmailReadonly |],
            "user",
            CancellationToken.None,
            FileDataStore("token.json", true)
        )
        |> Async.AwaitTask

    let private ardashBoardUserCredential =
        gmailUserCredentials
            "1054055296797-649vhgjueq528r64fh4b9nhvsscst1ks.apps.googleusercontent.com"
            "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"

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
    open System.Data.SQLite
    open Donald
    open Ardashboard.EmailService
    open Ardashboard.Stores
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
        let bankMessageStore = BankMessageStore()

        msgs
        |> Seq.map (fun msg -> bankMessageStore.Save(HtmlBankMessage(Id = msg.Id, HtmlBody = msg.HtmlBody)))


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
            |> Seq.toList

        htmlBankMessagesFromApi
        |> Seq.append (htmlBankMessagesFromStore)
