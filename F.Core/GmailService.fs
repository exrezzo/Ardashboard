module F.Core.GmailService


open System.IO
open System.Threading
open F.Core.EmailService
open Google.Apis.Auth.OAuth2
open Google.Apis.Gmail.v1
open Google.Apis.Gmail.v1.Data
open Google.Apis.Services
open Google.Apis.Util.Store
open Newtonsoft.Json.Linq

open F.Core.Domain

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

let private getMessageIds =
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
    
let GmailServiceImpl = {
    new IHtmlBankMessageStore with
        member this.GetMessageIds() = getMessageIds
        member this.GetHtmlBankMessages(msgIds:seq<string>) = getHtmlBankMessages msgIds
}