using Ardashboard.EmailService;
using FluentAssertions;
using Moq;
using Xunit;

namespace EmailServiceTests;

public class BankMessageTests
{
    [Fact]
    public async Task BankHtmlMessage_Is_Correctly_Parsed()
    {
        var esMock = new Mock<IEmailService>();
        esMock
            .Setup(service1 => service1.GetHtmlBankMessages(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new []{new HtmlBankMessage(){ HtmlBody = File.ReadAllText("bankMsgHtml.txt")}});
        var emailService = esMock.Object;
        
        var htmlBankMessages = await emailService.GetHtmlBankMessages(CancellationToken.None);
        var bankTransaction = htmlBankMessages.First().ToBankTransaction();
        bankTransaction.Amount.Should().Be(8.0);
        bankTransaction.Place.Should().NotBeNullOrWhiteSpace();
        bankTransaction.DateTime.Should().Be(DateTime.Parse("16/08/2022 14:39"));
    }
}