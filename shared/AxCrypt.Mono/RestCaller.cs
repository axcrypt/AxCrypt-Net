using System.Collections.Generic;
using System.Linq;

#region Coypright and License

/*
 * AxCrypt - Copyright 2026, AxCrypt AB, All Rights Reserved
 *
 * This file is part of AxCrypt.
 *
 * AxCrypt is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * AxCrypt is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with AxCrypt.  If not, see <http://www.gnu.org/licenses/>.
 *
 * The source is maintained at https://github.com/axcrypt/AxCrypt-Net please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axcrypt.net for more information about the author.
*/

#endregion Coypright and License

using AxCrypt.Abstractions;
using AxCrypt.Abstractions.Rest;
using AxCrypt.Common;
using AxCrypt.Core.Extensions;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Mono
{
    /// <summary>
    /// Sends API requests. Requests with an identity carry a JWT Bearer token obtained from the <see cref="ITokenProvider"/>;
    /// if the API rejects the token the request is repeated once with a fresh one.
    /// </summary>
    internal class RestCaller : IRestCaller
    {
        private const string PassphraseHeaderName = "X-AxCrypt-Passphrase";

        // The API reads the passphrase only on these endpoints (account and key material, secrets). Sending it nowhere else
        // keeps the secret off every other request. A prefix match on the path after "/api/".
        private static readonly string[] PassphraseEndpoints = new string[]
        {
            "users/my/account",
            "usersv2/my/account",
            "secrets/get",
            "secrets/save",
        };

        // One pooled client for the whole app: a client per request exhausts sockets and repeats TLS handshakes.
        private static readonly HttpClient SharedClient = CreateHttpClient();

        public RestCaller()
        {
        }

        #region IRestCaller Members

        public async Task<RestResponse> SendAsync(RestIdentity identity, RestRequest request)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (!New<IInternetState>().Connected)
            {
                throw new OfflineApiException(ExceptionMessage("Internet Unavailable", request));
            }

            try
            {
                RestResponse response = await SendOnceAsync(identity, request).Free();
                if (response.StatusCode == HttpStatusCode.Unauthorized && request.AuthMode == RestAuthMode.Bearer && !identity.IsEmpty)
                {
                    // The access token may have expired or been revoked. Forget it and try again with a refreshed or new one.
                    TokenProviders.Current.Invalidate(AuthorityOf(request), identity.User);
                    response = await SendOnceAsync(identity, request).Free();
                }

                return response;
            }
            catch (TaskCanceledException tcex)
            {
                throw new OfflineApiException(ExceptionMessage("Offline", request), tcex);
            }
            catch (WebException wex)
            {
                throw new OfflineApiException(ExceptionMessage("Offline", request), wex);
            }
            catch (HttpRequestException hrex)
            {
                New<IInternetState>().Clear();
                throw new OfflineApiException(ExceptionMessage("Offline", request), hrex);
            }
        }

        private static string ExceptionMessage(string message, RestRequest request)
        {
            return string.Format(CultureInfo.InvariantCulture, "{2} {1} {0}", request.Url, request.Method, message);
        }

        public string HtmlEncode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }

        public string UrlEncode(string value)
        {
            return WebUtility.UrlEncode(value);
        }

        #endregion IRestCaller Members

        private static async Task<RestResponse> SendOnceAsync(RestIdentity identity, RestRequest request)
        {
            if (request.Method == "GET" && request.Content.Text.Length > 0)
            {
                throw new ArgumentException("You can't send content with a GET request.", "request");
            }

            using (HttpRequestMessage message = CreateMessage(request))
            {
                await AuthenticateAsync(message, identity, request).Free();

                using (CancellationTokenSource timeout = request.Timeout > TimeSpan.Zero ? new CancellationTokenSource(request.Timeout) : new CancellationTokenSource())
                using (HttpResponseMessage httpResponse = await SharedClient.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token).Free())
                {
                    string content = await httpResponse.Content.ReadAsStringAsync().Free();
                    if (request.Method == "GET" && content.Length > 0 && !IsJson(httpResponse))
                    {
                        throw new HttpRequestException("Expected Media Type 'application/json'.");
                    }

                    return new RestResponse(httpResponse.StatusCode, content, httpResponse.Headers.RetryAfter?.Delta);
                }
            }
        }

        private static HttpRequestMessage CreateMessage(RestRequest request)
        {
            HttpMethod method;
            switch (request.Method)
            {
                case "GET":
                    method = HttpMethod.Get;
                    break;

                case "PUT":
                    method = HttpMethod.Put;
                    break;

                case "POST":
                    method = HttpMethod.Post;
                    break;

                case "DELETE":
                    method = HttpMethod.Delete;
                    break;

                default:
                    throw new NotSupportedException("The method '{0}' is not supported.".InvariantFormat(request.Method));
            }

            HttpRequestMessage message = new HttpRequestMessage(method, request.Url);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            foreach (string key in request.Headers.Collection.Keys)
            {
                message.Headers.TryAddWithoutValidation(key, request.Headers.Collection[key]);
            }

            if (request.Method == "PUT" || request.Method == "POST")
            {
                message.Content = new StringContent(request.Content.Text, Encoding.UTF8, "application/json");
            }

            return message;
        }

        private static async Task AuthenticateAsync(HttpRequestMessage message, RestIdentity identity, RestRequest request)
        {
            if (identity.IsEmpty || request.AuthMode == RestAuthMode.None)
            {
                return;
            }

            if (request.AuthMode == RestAuthMode.Basic)
            {
                string credentials = "{0}:{1}".InvariantFormat(identity.User, identity.Password.ToUtf8Base64());
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));
                return;
            }

            string accessToken = await TokenProviders.Current.GetAccessTokenAsync(AuthorityOf(request), identity.User, identity.Password).Free();
            if (accessToken.Length == 0)
            {
                return;
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (identity.Password.Length > 0 && NeedsPassphrase(request.Url))
            {
                message.Headers.TryAddWithoutValidation(PassphraseHeaderName, identity.Password.ToUtf8Base64());
            }
        }

        private static bool NeedsPassphrase(Uri url)
        {
            string path = url.AbsolutePath;
            int api = path.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            string relative = api >= 0 ? path.Substring(api + "/api/".Length) : path.TrimStart('/');
            foreach (string endpoint in PassphraseEndpoints)
            {
                if (relative.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Uri AuthorityOf(RestRequest request)
        {
            return new Uri(request.Url.GetLeftPart(UriPartial.Authority));
        }

        // The API answers errors with application/problem+json as well as application/json.
        private static bool IsJson(HttpResponseMessage response)
        {
            string mediaType = response.Content?.Headers?.ContentType?.MediaType;
            return mediaType != null && (mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
        }

        private static HttpClient CreateHttpClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
#if DEBUG
            handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;
#endif
            // Each request carries its own timeout.
            return new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }
    }
}
