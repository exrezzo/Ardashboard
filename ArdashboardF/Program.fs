// For more information see https://aka.ms/fsharp-console-apps
// printfn "Hello from F#"
module Ardashboard.Ciao

open System.Data
open System.Data.SQLite
open Donald
type BankMsg =
    {
        Id : string
        HtmlBody: string
    }
module BankMsg =
    let ofDataReader (rd:IDataReader) : BankMsg =
        {
            Id = rd.ReadString "Id"
            HtmlBody = rd.ReadString "HtmlBody"
        }
let msgs : Result<BankMsg list, DbError> =
    let sql = "select * from HtmlBankMessage"
    use conn = new SQLiteConnection "Data Source=db.db"
    
    conn
    |> Db.newCommand sql
    |> Db.query BankMsg.ofDataReader