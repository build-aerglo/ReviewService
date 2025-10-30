using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;

namespace ReviewService.Infrastructure.Tests.Helpers;

/// <summary>
/// Extension methods to simplify mocking HttpMessageHandler for testing HTTP clients
/// </summary>
public static class HttpMessageHandlerExtensions
{
    /// <summary>
    /// Sets up a mock HTTP request with the specified method and URL pattern
    /// </summary>
    public static ISetup<HttpMessageHandler, Task<HttpResponseMessage>> SetupRequest(
        this Mock<HttpMessageHandler> handler,
        HttpMethod method,
        string urlPattern)
    {
        return handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString().Contains(urlPattern)
                ),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    /// <summary>
    /// Returns a simple HTTP response with the specified status code
    /// </summary>
    public static IReturnsResult<HttpMessageHandler> ReturnsResponse(
        this ISetup<HttpMessageHandler, Task<HttpResponseMessage>> setup,
        HttpStatusCode statusCode,
        string content = "")
    {
        return setup.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }

    /// <summary>
    /// Returns an HTTP response with JSON content
    /// </summary>
    public static IReturnsResult<HttpMessageHandler> ReturnsJsonResponse<T>(
        this ISetup<HttpMessageHandler, Task<HttpResponseMessage>> setup,
        T responseObject,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseObject);
        return setup.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    /// <summary>
    /// Throws an exception when the HTTP request is made
    /// </summary>
    public static IReturnsResult<HttpMessageHandler> Throws<TException>(
        this ISetup<HttpMessageHandler, Task<HttpResponseMessage>> setup)
        where TException : Exception, new()
    {
        return setup.ThrowsAsync(new TException());
    }
}