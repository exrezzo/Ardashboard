using System.Data.SQLite;
using Ardashboard.EmailService;
using RepoDb;

namespace Ardashboard.Stores;

public class BankMessageStore : IBankMessageStore
{
    public BankMessageStore()
    {
        RepoDb.SQLiteBootstrap.Initialize();
    }

    public IEnumerable<string> GetMessageIds()
    {
        using var conn = new SQLiteConnection("Data Source=db.db");
        var htmlBankMessages = conn.QueryAll<HtmlBankMessage>();
        return htmlBankMessages.Select(msg => msg.Id);
    }

    public HtmlBankMessage GetHtmlBankMessage(string id)
    {
        using var conn = new SQLiteConnection("Data Source=db.db");
        var htmlBankMessages = conn.Query<HtmlBankMessage>(msg => msg.Id.Equals(id));
        return htmlBankMessages.FirstOrDefault();
    }

    public void Save(HtmlBankMessage message)
    {
        using var conn = new SQLiteConnection("Data Source=db.db");
        conn.Insert(message);
    }
}