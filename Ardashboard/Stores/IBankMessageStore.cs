using Ardashboard.EmailService;

namespace Ardashboard.Stores;

public interface IBankMessageStore
{
    IEnumerable<string> GetMessageIds();
    HtmlBankMessage GetHtmlBankMessage(string id);
    void Save(HtmlBankMessage message);
}