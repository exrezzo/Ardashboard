namespace Ardashboard.EmailService;

public interface IEmailService
{
    Task<IEnumerable<HtmlBankMessage>> GetHtmlBankMessages(CancellationToken cancellationToken);

}