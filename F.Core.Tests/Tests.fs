module Tests

open System
open System.Data.SQLite
open Donald
open F.Core
open F.Core.Domain
open F.Core.EmailService
open Microsoft.FSharp.Collections
open Xunit
open FsUnit.Xunit
    
[<Fact>]
let ``Write and read Html Bank Message to DB`` () =
    let msgId = "0"
    let htmlBody = "An HTML body"
    use conn =
            new SQLiteConnection "Data Source=db.db"
    let query = "INSERT INTO HtmlBankMessage(Id, HtmlBody) values (@Id, @HtmlBody)"
    conn
        |> Db.newCommand query
        |> Db.setParams
            [ "Id", SqlType.String msgId
              "HtmlBody", SqlType.String htmlBody]
        |> Db.exec
        |> ignore
    
    let sql = "select * from HtmlBankMessage"

    use conn =
        new SQLiteConnection "Data Source=db.db"

    let res =
        conn |> Db.newCommand sql |> Db.query HtmlBankMessageStore.ofDataReader

    match res with
    | Ok result' ->
                   let insertedBankMsg = Seq.head result'
                   should equal insertedBankMsg.Id msgId
                   should equal insertedBankMsg.HtmlBody htmlBody
    | _ -> Seq.empty |> failwith "Html bank message not inserted correctly into db"
    
[<Fact>]
let ``Retrieve messages from cache and from remote store if new ones``() =
    let zerothMsg = {Id="0"; HtmlBody="<p>zeroth</p>"}
    let firstMsg = {Id="1"; HtmlBody="<p>first</p>"}
    let bankMsgStoreMock = {
        new IHtmlBankMessageStore with
            member this.GetMessageIds() = [zerothMsg.Id; firstMsg.Id]
            member this.GetHtmlBankMessages(msgs:seq<string>) =
                Seq.filter (fun (msg:HtmlBankMsg) -> msgs |> Seq.contains msg.Id) [
                {Id = zerothMsg.Id; HtmlBody = "PHA+emVyb3RoPC9wPg=="}
                {Id = firstMsg.Id; HtmlBody = "PHA+Zmlyc3Q8L3A+"}
            ] 
    }
    let bankMsgCacheMock = {
        new IHtmlBankMessageCache with
            member this.GetMessageIds() = [zerothMsg.Id]            
            member this.GetHtmlBankMessages() = [zerothMsg]
            member this.SaveHtmlBankMessages(msgs:seq<HtmlBankMsg>) = ignore |> ignore
    }
    
    let anOccurredDateTime = DateTime.Parse "2022/01/01 15:30"
    let mapper (htmlBankMsg:HtmlBankMsg) =
        match htmlBankMsg.Id with
        | "0" -> {Amount=0.01; Place="Zero world"; Occurred= anOccurredDateTime}
        | "1" -> {Amount=1.11; Place="First world"; Occurred=anOccurredDateTime + TimeSpan.FromSeconds 5}
        | _ -> failwith "soo sad."
    
    let msgs = getBankMessages bankMsgStoreMock bankMsgCacheMock mapper
    let msgsAreEqual = 
        List.zip (msgs |> List.ofSeq) [ mapper firstMsg; mapper zerothMsg; ]
        |> List.map (fun(a, b) -> a = b)
        |> List.forall (fun res -> res = true)
    msgsAreEqual |> should equal true
    
[<Fact>]
let ``Decode encoded Html message in base64 format`` () =
    let encodedText = "PGgxPlRpdG9sbzwvaDE+DQo8cD5DaWFvPC9wPg=="
    
    EmailService.decodeHtmlBankMessages [{Id= "0"; HtmlBody= encodedText}]
    |> Seq.head
    |> should equal {Id="0"; HtmlBody="<h1>Titolo</h1>
<p>Ciao</p>"}

    