using System.Text.RegularExpressions;
using System.Web;
using Ardashboard.Models;

namespace Ardashboard.EmailService;

public class HtmlBankMessage
{
    public string Id { get; init; }
    public string HtmlBody { get; init; }

    public BankTransaction ToBankTransaction()
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(HtmlBody);

        var table = doc.DocumentNode.SelectSingleNode("//table[@class='tabellaDisposizione']");
        var rows = table.Descendants("tr");
        var cleanTableRows =
            rows
                .Select(row => (row.Descendants("th").First().InnerText, row.Descendants("td").First().InnerText))
                .Select(tuple => (HttpUtility.HtmlDecode(tuple.Item1), HttpUtility.HtmlDecode(tuple.Item2)))
                .Select(tuple => (Regex.Replace(tuple.Item1, @"\s+", " ").Trim(),
                    Regex.Replace(tuple.Item2, @"\s+", " ").Trim()));

        var bankTransaction = new BankTransaction();
        foreach (var (transactionProperty, transactionValue) in cleanTableRows)
        {
            switch (transactionProperty)
            {
                case "Importo":
                    bankTransaction.Amount = double.Parse(transactionValue.Replace("euro", "").Trim());
                    break;
                case "Presso":
                    bankTransaction.Place = transactionValue;
                    break;
                case "Data":
                    bankTransaction.DateTime = DateTime.Parse(transactionValue);
                    break;
                case "Ora":
                    bankTransaction.DateTime += TimeOnly.Parse(transactionValue).ToTimeSpan();
                    break;
            }
        }

        return bankTransaction;
    }
}