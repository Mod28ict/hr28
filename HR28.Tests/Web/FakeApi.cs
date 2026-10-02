using System.Net;
using System.Text;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HR28.Tests.Web;

/// <summary>Stands in for the HR28 API: answers every request with one fixed response.</summary>
internal sealed class FakeApi : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _json;

    public FakeApi(HttpStatusCode status, string json = "")
    {
        _status = status;
        _json = json;
    }

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        });
    }

    public DashboardService CreateService() =>
        new(new ApiClient(
            new HttpClient(this),
            Options.Create(new ApiSettings { BaseUrl = "https://api.test/api/" }),
            NullLogger<ApiClient>.Instance,
            new HttpContextAccessor()));
}
