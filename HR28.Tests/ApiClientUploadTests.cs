extern alias web;

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using web::HR28.Web.Models;
using web::HR28.Web.Services;

namespace HR28.Tests;

/// <summary>
/// The web app's file uploads: a successful answer without data (the logo upload's
/// "204 No Content") is a success, not "The upload did not finish".
/// </summary>
public class ApiClientUploadTests
{
    private sealed class FixedAnswer(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private static ApiClient ClientAnswering(HttpResponseMessage response) =>
        new(new HttpClient(new FixedAnswer(response)),
            Options.Create(new ApiSettings { BaseUrl = "http://api.test/api/" }),
            NullLogger<ApiClient>.Instance,
            new HttpContextAccessor());

    private static IFormFile Logo()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "logo", "logo.png");
    }

    [Fact]
    public async Task An_empty_success_answer_is_a_success()
    {
        var api = ClientAnswering(new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await api.PostFileAsync<object>("Settings/logo", Logo(), "token");

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task A_json_success_answer_is_still_read()
    {
        var api = ClientAnswering(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":\"Photo saved.\"}", Encoding.UTF8, "application/json")
        });

        var result = await api.PostFileAsync<Dictionary<string, string>>("Voters/x/photo", Logo(), "token");

        Assert.True(result.Success);
        Assert.Equal("Photo saved.", result.Data!["message"]);
    }

    [Fact]
    public async Task A_refused_upload_shows_the_api_message()
    {
        var api = ClientAnswering(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"The logo is too large. Please choose one under 1 MB.\"}", Encoding.UTF8, "application/json")
        });

        var result = await api.PostFileAsync<object>("Settings/logo", Logo(), "token");

        Assert.False(result.Success);
        Assert.Equal("The logo is too large. Please choose one under 1 MB.", result.Message);
    }
}
