using System.Net;
using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace HR28.Tests.Web;

public class ApiFailureExceptionFilterTests
{
    [Fact]
    public void Session_ended_clears_session_and_goes_to_sign_in()
    {
        var (filter, context, session, _) = Create(HttpStatusCode.Unauthorized);

        filter.OnException(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(("Auth", "Login"), (redirect.ControllerName, redirect.ActionName));
        Assert.Empty(session.Keys);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void No_permission_goes_to_dashboard_with_message()
    {
        var (filter, context, session, tempData) = Create(HttpStatusCode.Forbidden);

        filter.OnException(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(("Dashboard", "Index"), (redirect.ControllerName, redirect.ActionName));
        Assert.Equal("You don't have permission to do that.", tempData["FlashError"]);
        Assert.Contains("JwtToken", session.Keys);
    }

    [Fact]
    public void No_permission_on_dashboard_shows_error_page_instead_of_looping()
    {
        var (filter, context, _, _) = Create(HttpStatusCode.Forbidden, controller: "Dashboard");

        filter.OnException(context);

        var view = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal(403, view.StatusCode);
    }

    [Fact]
    public void Too_many_requests_shows_friendly_error_page()
    {
        var (filter, context, session, _) = Create(HttpStatusCode.TooManyRequests);

        filter.OnException(context);

        var view = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal("Error", view.ViewName);
        Assert.Equal(429, view.StatusCode);
        Assert.Equal(
            "Too many requests. Please wait a few minutes and try again.",
            Assert.IsType<ErrorViewModel>(view.ViewData.Model).Message);
        Assert.Contains("JwtToken", session.Keys);
    }

    [Fact]
    public void Script_request_gets_json_message_not_a_redirect()
    {
        var (filter, context, _, _) = Create(HttpStatusCode.Unauthorized, xhr: true);

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void Other_exceptions_are_left_alone()
    {
        var (filter, context, _, _) = Create(HttpStatusCode.OK);
        context.Exception = new InvalidOperationException();

        filter.OnException(context);

        Assert.Null(context.Result);
        Assert.False(context.ExceptionHandled);
    }

    private static (ApiFailureExceptionFilter, ExceptionContext, TestSession, ITempDataDictionary) Create(
        HttpStatusCode status, string controller = "Voters", bool xhr = false)
    {
        var session = new TestSession();
        session.SetString("JwtToken", "token");

        var http = new DefaultHttpContext();
        http.Features.Set<Microsoft.AspNetCore.Http.Features.ISessionFeature>(
            new Microsoft.AspNetCore.Http.Features.DefaultSessionFeature { Session = session });

        if (xhr)
            http.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var route = new RouteData();
        route.Values["controller"] = controller;

        var tempData = new TempDataDictionary(http, new NullTempDataProvider());
        var tempDataFactory = new FixedTempDataFactory(tempData);

        var message = status switch
        {
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please wait a few minutes and try again.",
            _ => "Your session has ended. Please sign in again."
        };

        var context = new ExceptionContext(
            new ActionContext(http, route, new ActionDescriptor()),
            new List<IFilterMetadata>())
        {
            Exception = new ApiCallException(status, message)
        };

        var filter = new ApiFailureExceptionFilter(tempDataFactory, new EmptyModelMetadataProvider());

        return (filter, context, session, tempData);
    }

    private sealed class FixedTempDataFactory(ITempDataDictionary tempData) : ITempDataDictionaryFactory
    {
        public ITempDataDictionary GetTempData(HttpContext context) => tempData;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
