using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace AxCrypt.Abstractions.Rest
{
    public class RestResponse
    {
        public HttpStatusCode StatusCode { get; private set; }

        public string Content { get; private set; }

        /// <summary>
        /// How long the server asks the client to wait before retrying (HTTP 429/503), if it said.
        /// </summary>
        public TimeSpan? RetryAfter { get; private set; }

        public RestResponse(HttpStatusCode statusCode, string content)
        {
            StatusCode = statusCode;
            Content = content;
        }

        public RestResponse(HttpStatusCode statusCode, string content, TimeSpan? retryAfter)
            : this(statusCode, content)
        {
            RetryAfter = retryAfter;
        }
    }
}