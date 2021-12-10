using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AzureTranslationManager
{
    public class TranslationClient
    {
        private HttpClient Client { get; }

        public TranslationClient(TranslationConfiguration configuration)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(configuration.Endpoint);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", configuration.SubscriptionKey);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", configuration.SubscriptionRegion);

            Client = client;
        }

        public async Task<string> Request(string requestUri, string requestBody)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(requestUri, UriKind.Relative);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await Client.SendAsync(request).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new TranslationException(response.StatusCode, responseBody);
            }

            return responseBody;
        }
    }
}
