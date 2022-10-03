module Tests

open System.Data.SQLite
open Donald
open F.Core.EmailService
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``My test`` () =
    Assert.True(true)
    
    
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
        conn |> Db.newCommand sql |> Db.query HtmlBankMessageRepository.ofDataReader

    match res with
    | Ok result' ->
                   let insertedBankMsg = Seq.head result'
                   should equal insertedBankMsg.Id msgId
                   should equal insertedBankMsg.HtmlBody htmlBody
    | _ -> Seq.empty |> failwith "Html bank message not inserted correctly into db"