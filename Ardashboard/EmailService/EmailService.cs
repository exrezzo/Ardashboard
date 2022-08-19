using System.Data.SQLite;
using System.Text;
using Ardashboard.Stores;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using RepoDb;

namespace Ardashboard.EmailService;

public class EmailService : IEmailService
{
    private readonly IBankMessageStore _bankMessagesStore;
    private readonly GmailService _gmailService;

    public EmailService(IBankMessageStore bankMessagesStore)
    {
        _bankMessagesStore = bankMessagesStore;
        var credPath = "token.json";
        var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets()
            {
                ClientId = "1054055296797-649vhgjueq528r64fh4b9nhvsscst1ks.apps.googleusercontent.com",
                ClientSecret = "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"
            },
            new[] { GmailService.Scope.GmailReadonly },
            "user",
            CancellationToken.None,
            new FileDataStore(credPath, true)).Result;

        // Create Gmail API service.
        _gmailService = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Ardashboard"
        });
    }

    public async Task<IEnumerable<HtmlBankMessage>> GetHtmlBankMessages(CancellationToken cancellationToken = new())
    {
        var messageIds = await _getBankMessageIds(cancellationToken);
        var storedIds = _bankMessagesStore.GetMessageIds();
        var notCachedMessageIds = messageIds.Except(storedIds);
        var htmlBankMessagesFromApi = await _getHtmlBankMessagesFromApi(notCachedMessageIds);
        htmlBankMessagesFromApi.ToList().ForEach(_bankMessagesStore.Save);
        var storedHtmlMessages = storedIds.Select(_bankMessagesStore.GetHtmlBankMessage);
        return htmlBankMessagesFromApi.Concat(storedHtmlMessages);//.Select(message => message.HtmlBody);
    }

    private async Task<IEnumerable<string>> _getBankMessageIds(CancellationToken cancellationToken = new())
    {
        // Define parameters of request.
        var messagesRequest = _gmailService.Users.Messages.List("me");
        messagesRequest.Q = "from:webank@webank.it subject:autorizzato pagamento";
        var listMessagesResponse = await messagesRequest.ExecuteAsync(cancellationToken);
        var messageIds = listMessagesResponse.Messages.Select(message => message.Id);
        return messageIds;
    }

    private async Task<IEnumerable<HtmlBankMessage>> _getHtmlBankMessagesFromApi(IEnumerable<string> ids)
    {
        var messageTasks =
            ids.Select(id => _gmailService.Users.Messages.Get("me", id).ExecuteAsync());
        var messages = await Task.WhenAll(messageTasks);

        var bankHtmlMessages = messages
                .Select(r => r.Payload.Parts.Select(part => part.Body.Data))
                .Select(parts =>
                    parts
                        .Aggregate(new StringBuilder(),
                            (stringBuilder, nextEmailBodyText) => stringBuilder.Append(nextEmailBodyText))
                        .ToString()
                )
                .Select(encodedEmail =>
                {
                    var data = Convert.FromBase64String(
                        encodedEmail
                            .Replace('-', '+')
                            .Replace('_', '/')
                    );
                    var decodedString = Encoding.UTF8.GetString(data);
                    return decodedString;
                })
            ;
        return ids.Zip(bankHtmlMessages, (id, msg) => new HtmlBankMessage() { HtmlBody = msg, Id = id });
    }
}