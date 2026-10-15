using System.Net;
using System.Text;
using System.Xml.Linq;
using HR28.Application.Common;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HR28.Tests;

/// <summary>Dhiraagu Bulk SMS sender, Maldivian numbers and the per phone number limit.</summary>
public class DhiraaguSmsTests
{
    private const string Endpoint = "https://bulksms.example.test/partners/xmlMessage.jsp";

    private sealed class Capture(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Sent { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Sent = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
        }
    }

    private static string Answer(string status, string description) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><TELEMESSAGE><TELEMESSAGE_CONTENT><RESPONSE><MESSAGE_ID>123</MESSAGE_ID>" +
        $"<RESPONSE_STATUS>{status}</RESPONSE_STATUS><RESPONSE_STATUS_DESC>{description}</RESPONSE_STATUS_DESC></RESPONSE></TELEMESSAGE_CONTENT><VERSION>1</VERSION></TELEMESSAGE>";

    private static DhiraaguSmsSender Sender(Capture handler) =>
        new(new HttpClient(handler), Endpoint, "campaign", "p&ss<word>", NullLogger<DhiraaguSmsSender>.Instance);

    [Theory]
    [InlineData("7771234", "9607771234")]
    [InlineData("9771234", "9609771234")]
    [InlineData("+960 777 1234", "9607771234")]
    [InlineData("9607771234", "9607771234")]
    [InlineData("777-1234", "9607771234")]
    [InlineData("77712345", null)]
    [InlineData("3331234", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Maldivian_mobile_numbers_get_the_country_code(string? number, string? expected) =>
        Assert.Equal(expected, MaldivesPhone.ToInternational(number));

    [Fact]
    public async Task Sends_the_message_in_Dhiraagus_format_with_the_login_escaped()
    {
        var handler = new Capture(HttpStatusCode.OK, Answer("100", "Message received"));

        Assert.True(await Sender(handler).SendAsync("777 1234", "HR28 code: 482913. <Never> share & keep it."));

        var sent = XDocument.Parse(handler.Sent!);
        Assert.Equal("campaign", sent.Descendants("USER_NAME").Single().Value);
        Assert.Equal("p&ss<word>", sent.Descendants("PASSWORD").Single().Value);
        Assert.Equal("9607771234", sent.Descendants("DEVICE_VALUE").Single().Value);
        Assert.Equal("SMS", sent.Descendants("DEVICE_TYPE").Single().Attribute("DEVICE_TYPE")!.Value);
        Assert.Equal("HR28 code: 482913. <Never> share & keep it.", sent.Descendants("TEXT").Single().Value);
    }

    [Theory]
    [InlineData("401", "Invalid login")]
    [InlineData("", "")]
    public async Task Any_other_answer_counts_as_not_sent(string status, string description)
    {
        var handler = new Capture(HttpStatusCode.OK, Answer(status, description));

        Assert.False(await Sender(handler).SendAsync("7771234", "hello"));
    }

    [Fact]
    public async Task An_empty_or_broken_answer_counts_as_not_sent()
    {
        Assert.False(await Sender(new Capture(HttpStatusCode.OK, "")).SendAsync("7771234", "hello"));
        Assert.False(await Sender(new Capture(HttpStatusCode.OK, "FAILED")).SendAsync("7771234", "hello"));
        Assert.False(await Sender(new Capture(HttpStatusCode.OK, "<TELEMESSAGE><unclosed")).SendAsync("7771234", "hello"));
        Assert.False(await Sender(new Capture(HttpStatusCode.BadGateway, Answer("100", "ok"))).SendAsync("7771234", "hello"));
    }

    [Fact]
    public async Task A_number_that_isnt_Maldivian_is_never_sent()
    {
        var handler = new Capture(HttpStatusCode.OK, Answer("100", "ok"));

        Assert.False(await Sender(handler).SendAsync("77712345", "hello"));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void A_half_set_login_or_plain_http_address_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DhiraaguSmsSender(new HttpClient(), Endpoint, "campaign", "", NullLogger<DhiraaguSmsSender>.Instance));
        Assert.Throws<InvalidOperationException>(() =>
            new DhiraaguSmsSender(new HttpClient(), "http://bulksms.example.test/x", "campaign", "pw", NullLogger<DhiraaguSmsSender>.Instance));
    }

    private sealed class CountingSender : ISmsSender
    {
        public int Sent { get; private set; }
        public Task<bool> SendAsync(string phoneNumber, string message) { Sent++; return Task.FromResult(true); }
    }

    [Fact]
    public async Task One_number_gets_at_most_8_messages_an_hour_however_it_is_written()
    {
        var inner = new CountingSender();
        var now = new DateTime(2026, 10, 15, 3, 0, 0, DateTimeKind.Utc);
        var limited = new PhoneLimitedSmsSender(inner, NullLogger<PhoneLimitedSmsSender>.Instance, () => now);

        for (var i = 0; i < PhoneLimitedSmsSender.MaxPerHour; i++)
            Assert.True(await limited.SendAsync(i % 2 == 0 ? "7771234" : "+960 777 1234", "code"));

        Assert.False(await limited.SendAsync("9607771234", "code"));
        Assert.True(await limited.SendAsync("7779999", "code"));

        now = now.AddHours(1);
        Assert.True(await limited.SendAsync("7771234", "code"));
        Assert.Equal(PhoneLimitedSmsSender.MaxPerHour + 2, inner.Sent);
    }
}
