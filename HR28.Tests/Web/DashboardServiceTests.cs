using System.Net;
using HR28.Web.Models;
using HR28.Web.Services;

namespace HR28.Tests.Web;

public class DashboardServiceTests
{
    private const string Token = "token";

    [Fact]
    public async Task Read_returns_data_on_success()
    {
        var api = new FakeApi(HttpStatusCode.OK, """[{"id":"6f1c2a52-6c55-4a4f-9a52-2f4f0b1c9d10","name":"Male"}]""");

        var constituencies = await api.CreateService().GetConstituenciesAsync(Token);

        Assert.Equal("Male", Assert.Single(constituencies!).Name);
    }

    [Fact]
    public async Task Read_returns_null_when_not_found()
    {
        var api = new FakeApi(HttpStatusCode.NotFound);

        Assert.Null(await api.CreateService().GetVoterProfileAsync(Guid.NewGuid(), Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Read_throws_friendly_failure_for_other_errors(HttpStatusCode status)
    {
        var api = new FakeApi(status);

        var failure = await Assert.ThrowsAsync<ApiCallException>(
            () => api.CreateService().GetDashboardAsync(Token));

        Assert.Equal(status, failure.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(failure.UserMessage));
        Assert.DoesNotContain("Exception", failure.UserMessage);
    }

    [Fact]
    public async Task Read_without_token_is_treated_as_session_ended()
    {
        var api = new FakeApi(HttpStatusCode.OK, "{}");

        var failure = await Assert.ThrowsAsync<ApiCallException>(
            () => api.CreateService().GetUsersAsync(null));

        Assert.True(failure.IsUnauthorized);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Save_returns_false_with_api_message_for_business_rule()
    {
        var api = new FakeApi(HttpStatusCode.BadRequest, """{"message":"A voter with National ID A123456 already exists."}""");
        var service = api.CreateService();

        var saved = await service.UpdateVoterAsync(Guid.NewGuid(), new VoterCreateEditDto(), Token);

        Assert.False(saved);
        Assert.Equal("A voter with National ID A123456 already exists.", service.LastErrorMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Save_throws_for_session_permission_and_rate_limit(HttpStatusCode status)
    {
        var api = new FakeApi(status);

        var failure = await Assert.ThrowsAsync<ApiCallException>(
            () => api.CreateService().CreatePledgeAsync(new CreatePledgeDto(), Token));

        Assert.Equal(status, failure.StatusCode);
    }

    [Fact]
    public async Task Search_term_is_escaped_in_the_query_string()
    {
        var api = new FakeApi(HttpStatusCode.OK, "[]");

        await api.CreateService().SearchVotersAsync("a&page=999", Token);

        var query = Assert.Single(api.Requests).RequestUri!.Query;
        Assert.Equal("?searchTerm=a%26page%3D999", query);
    }
}
