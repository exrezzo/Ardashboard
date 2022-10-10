module F.Core.HtmlBankMessageStore

open System.Data
open System.Data.SQLite
open Donald
open F.Core.Domain
open F.Core.EmailService

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
    let query = "INSERT INTO HtmlBankMessage(Id, HtmlBody) values (@Id, @HtmlBody)"
    let ppp = msgs |> Seq.collect (fun (x: HtmlBankMsg) ->
        [["Id", SqlType.String x.Id
          "HtmlBody", SqlType.String x.HtmlBody]])
              |> Seq.toList
    use conn =
        new SQLiteConnection "Data Source=db.db"
    conn.Open()
    let insertResult =
        conn
        |> Db.newCommand query
        |> Db.execMany ppp
    match insertResult with
    | Ok resultValue -> ignore
    | Error errorValue -> failwith (errorValue |> string)
    
let HtmlBankMessageLocalSqliteStore = {
    new IHtmlBankMessageCache with
        member this.GetMessageIds() = messageIds
        member this.GetHtmlBankMessages() = getHtmlBankMessages
        member this.SaveHtmlBankMessages (msgs:seq<HtmlBankMsg>) = saveHtmlMessages msgs |> ignore
}
    