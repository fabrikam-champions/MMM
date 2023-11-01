using Microsoft.CodeAnalysis;
using MMM.Analyzers.Enums;
using MMM.Analyzers.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MMM.Analyzers.Helpers
{
    internal static class HttpClientHelper
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string BaseUrl = Environment.GetEnvironmentVariable("MMMBaseUrl") ?? "http://localhost:80/";
        private static readonly string PublishesRoute = Environment.GetEnvironmentVariable("MMMPublishesRoute") ?? $"{(BaseUrl.Contains("?") ? "&" : "?")}direction=publish";
        private static readonly string SubscribesRoute = Environment.GetEnvironmentVariable("MMMSubscribesRoute") ?? $"{(BaseUrl.Contains("?") ? "&" : "?")}direction=subscribe";
        private static readonly string PublishesUrl = $"{BaseUrl}{PublishesRoute}";
        private static readonly string SubscribesUrl = BaseUrl + SubscribesRoute;
        static HttpClientHelper()
        {
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        }

        public static async Task Send(MessageDirection direction, string messageName, string messageSchema, string messageDescription, string moduleName, string assemblyName, string compilationId, string location)
        {
            try
            {
                HttpContent content = new StringContent(messageSchema, Encoding.UTF8, "text/plain");
                string url =  direction == MessageDirection.Publish ? PublishesUrl : SubscribesUrl;
                url = $"{url}{(url.Contains("?") ? "&" : "?")}messageName={WebUtility.UrlEncode(messageName)}&messageDescription={WebUtility.UrlEncode(messageDescription)}&moduleName={WebUtility.UrlEncode(moduleName)}&assemblyName={WebUtility.UrlEncode(assemblyName)}&compilationId={WebUtility.UrlEncode(compilationId)}&location={WebUtility.UrlEncode(location)}";
                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }
}