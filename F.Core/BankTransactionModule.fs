module F.Core.BankTransactionModule

open System
open System.IO
open System.Text.RegularExpressions
open System.Web
open F.Core.Domain
open FSharp.Data

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