using RepoDb.Attributes;

namespace Ardashboard.Infrastructure;
[Map("HtmlBankMessage")]
public class HtmlBankMessageStored
{
    public string Id { get; init; }
    public string HtmlBody { get; init; }
}