using System.Text;
using System.Xml.Linq;
using HR28.Application.Common;
using HR28.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Sends SMS through Dhiraagu Bulk SMS (partner XML interface, "TeleMessage" format; owner,
/// 2026-10-15: tested with the client's Bulk SMS login). Settings: DhiraaguSms:Endpoint,
/// DhiraaguSms:Username, DhiraaguSms:Password (server only, /etc/hr28/sms.env). Any doubt
/// counts as "not sent", so a sign-in code nobody received is not left usable. Never logs
/// the message text or the login.
/// </summary>
public class DhiraaguSmsSender : ISmsSender
{
    public const string EndpointSetting = "DhiraaguSms:Endpoint";
    public const string UsernameSetting = "DhiraaguSms:Username";
    public const string PasswordSetting = "DhiraaguSms:Password";
    public const string DefaultEndpoint = "https://bulksms.dhiraagu.com.mv/partners/xmlMessage.jsp";

    /// <summary>Dhiraagu's "message received" status.</summary>
    public const string AcceptedStatus = "100";

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _username;
    private readonly string _password;
    private readonly ILogger<DhiraaguSmsSender> _logger;

    public DhiraaguSmsSender(HttpClient httpClient, string endpoint, string username, string password, ILogger<DhiraaguSmsSender> logger)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"{UsernameSetting} and {PasswordSetting} are both required.");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{EndpointSetting} must be an https address.");

        _httpClient = httpClient;
        _endpoint = endpoint;
        _username = username;
        _password = password;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string phoneNumber, string message)
    {
        var to = MaldivesPhone.ToInternational(phoneNumber);

        if (to == null)
        {
            _logger.LogWarning("SMS not sent: the phone number is not a Maldivian mobile number.");
            return false;
        }

        try
        {
            using var content = new StringContent(BuildRequest(_username, _password, to, message), Encoding.UTF8, "text/xml");
            using var response = await _httpClient.PostAsync(_endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            var (status, description) = ReadStatus(body);

            if (response.IsSuccessStatusCode && status == AcceptedStatus)
                return true;

            _logger.LogError("Dhiraagu did not accept the SMS: HTTP {Http}, status {Status} {Description}.",
                (int)response.StatusCode, status ?? "(none)", description ?? string.Empty);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            _logger.LogError("Dhiraagu could not be reached for an SMS: {Error}.", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>The XML request (values are escaped by XDocument).</summary>
    public static string BuildRequest(string username, string password, string to, string message)
    {
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("TELEMESSAGE",
                new XElement("TELEMESSAGE_CONTENT",
                    new XElement("MESSAGE",
                        new XElement("MESSAGE_INFORMATION", new XElement("SUBJECT", "SMS")),
                        new XElement("USER_FROM",
                            new XElement("CIML",
                                new XElement("NAML",
                                    new XElement("LOGIN_DETAILS",
                                        new XElement("USER_NAME", username),
                                        new XElement("PASSWORD", password))))),
                        new XElement("MESSAGE_CONTENT",
                            new XElement("TEXT_MESSAGE",
                                new XElement("MESSAGE_INDEX", 0),
                                new XElement("TEXT", message))),
                        new XElement("USER_TO",
                            new XElement("CIML",
                                new XElement("DEVICE_INFORMATION",
                                    new XElement("DEVICE_TYPE", new XAttribute("DEVICE_TYPE", "SMS")),
                                    new XElement("DEVICE_VALUE", to)))))),
                new XElement("VERSION", "1.6")));

        return xml.Declaration + xml.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>RESPONSE_STATUS and RESPONSE_STATUS_DESC from Dhiraagu's answer (null when missing).</summary>
    public static (string? Status, string? Description) ReadStatus(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('<'))
            return (null, null);

        var doc = XDocument.Parse(body);
        return (doc.Descendants("RESPONSE_STATUS").FirstOrDefault()?.Value.Trim(),
                doc.Descendants("RESPONSE_STATUS_DESC").FirstOrDefault()?.Value.Trim());
    }
}
