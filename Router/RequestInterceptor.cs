using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace MyLoadBalancer.Router
{
    public class RequestInterceptor
    {
        private readonly ServerFarm _serverFarm;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly RequestDelegate _nextMiddleware;

        public RequestInterceptor(RequestDelegate nextMiddleware,ServerFarm serverFarm)
        {
            _nextMiddleware = nextMiddleware;
            _serverFarm = serverFarm;
        }

        public async Task Invoke(HttpContext httpContext)
        {

            var targetUrl = BuildTargetUri(httpContext);
            var requestMessage = CreateRequestMessage(targetUrl, httpContext);

            using (var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted))
            {
                httpContext.Response.StatusCode = (int)response.StatusCode;

                CopyResponseHeadersFromTarget(response, httpContext);

                await response.Content.CopyToAsync(httpContext.Response.Body);
            }

            return;
        }

        private Uri BuildTargetUri(HttpContext context)
        {
            var serverUrl = _serverFarm.RoundRobinTargets();
            var targetUrl = new Uri($"{serverUrl}{context.Request.Path}");

            return targetUrl;
        }

        private static HttpRequestMessage CreateRequestMessage(Uri targetUrl, HttpContext context)
        {
            var requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = targetUrl;
            requestMessage.Method = new HttpMethod(context.Request.Method);
            requestMessage.Headers.Host = targetUrl.Host;
            if (HttpMethods.IsPost(context.Request.Method) ||
                HttpMethods.IsPut(context.Request.Method) ||
                HttpMethods.IsPatch(context.Request.Method))
            {
                requestMessage.Content = new StreamContent(context.Request.Body);
            }

            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            return requestMessage;
        }

        private static void CopyResponseHeadersFromTarget(HttpResponseMessage responseMessage,
            HttpContext context)
        {
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers.Add(header.Key, header.Value.ToArray());
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers.Add(header.Key, header.Value.ToArray());
            }

            context.Response.Headers.Remove("transfer-encoding");
        }
    }
}

