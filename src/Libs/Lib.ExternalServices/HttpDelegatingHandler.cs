using System.Diagnostics;
using System.Net.Http.Headers;

namespace Lib.ExternalServices
{
    public class HttpDelegatingHandler(HttpMessageHandler? innerHandler = null) : DelegatingHandler(
        innerHandler ?? new HttpClientHandler())
    {
        private readonly string[] _types = ["html", "text", "xml", "json", "txt", "x-www-form-urlencoded"];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            var start = DateTime.Now;
            var msg = $"[{request.RequestUri?.PathAndQuery} -  Request]";

            Debug.WriteLine($"{msg}========Request Start==========");
            Debug.WriteLine(
                $"{msg} {request.Method} {request.RequestUri?.PathAndQuery} {request.RequestUri?.Scheme}/{request.Version}");
            Debug.WriteLine($"{msg} Host: {request.RequestUri?.Scheme}://{request.RequestUri?.Host}");

            foreach (var header in request.Headers)
            {
                Debug.WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    Debug.WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
                }

                Debug.WriteLine($"{msg} Content:");

                if (request.Content is StringContent || IsTextBasedContentType(request.Headers) ||
                    IsTextBasedContentType(request.Content.Headers))
                {
                    var result = await request.Content.ReadAsStringAsync(cancellationToken);

                    Debug.WriteLine($"{msg} {string.Join("", result.Take(256))}...");
                }
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            Debug.WriteLine($"{msg}==========Request End==========");

            msg = $"[{request.RequestUri?.PathAndQuery} - Response]";

            Debug.WriteLine($"{msg}=========Response Start=========");

            Debug.WriteLine(
                $"{msg} {request.RequestUri?.Scheme.ToUpper()}/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");

            foreach (var header in response.Headers)
            {
                Debug.WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    Debug.WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
                }

                Debug.WriteLine($"{msg} Content:");

                if (response.Content is StringContent || IsTextBasedContentType(response.Headers) ||
                    IsTextBasedContentType(response.Content.Headers))
                {
                    var result = await response.Content.ReadAsStringAsync(cancellationToken);

                    Debug.WriteLine($"{msg} {string.Join("", result.Take(256))}...");
                }

                var str = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine(str);
            }

            Debug.WriteLine($"{msg} Duration: {DateTime.Now - start}");
            Debug.WriteLine($"{msg}==========Response End==========");
            return response;
        }

        private bool IsTextBasedContentType(HttpHeaders headers)
        {
            if (!headers.TryGetValues("Content-Type", out var values))
            {
                return false;
            }

            var header = string.Join(" ", values).ToLowerInvariant();

            return _types.Any(t => header.Contains(t));
        }
    }
}