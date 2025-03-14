﻿using System.Net;

namespace Intersect.Server.Web.Http
{

    public static partial class HttpStatusCodeExtensions
    {

        public static LogLevel ToIntersectLogLevel(this HttpStatusCode httpStatusCode, HttpMethod httpMethod = null)
        {
            // 1xx
            if (httpStatusCode < HttpStatusCode.OK)
            {
                return LogLevel.Trace;
            }

            // 2xx
            if (httpStatusCode < HttpStatusCode.MultipleChoices)
            {
                if (httpMethod == HttpMethod.Get || httpMethod == HttpMethod.Head || httpMethod == HttpMethod.Options)
                {
                    return LogLevel.Debug;
                }

                return LogLevel.Information;
            }

            // 3xx
            if (httpStatusCode < HttpStatusCode.BadRequest)
            {
                return LogLevel.Information;
            }

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (httpStatusCode)
            {
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.RequestEntityTooLarge:
                case HttpStatusCode.RequestUriTooLong:
                case HttpStatusCode.UnsupportedMediaType:
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                    if (httpMethod == HttpMethod.Get ||
                        httpMethod == HttpMethod.Head ||
                        httpMethod == HttpMethod.Options)
                    {
                        return LogLevel.Information;
                    }

                    return LogLevel.Warning;

                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.NotFound:
                case HttpStatusCode.ProxyAuthenticationRequired:
                case HttpStatusCode.Conflict:
                case HttpStatusCode.Gone:
                    if (httpMethod == HttpMethod.Get ||
                        httpMethod == HttpMethod.Head ||
                        httpMethod == HttpMethod.Options)
                    {
                        return LogLevel.Warning;
                    }

                    return LogLevel.Error;

                case (HttpStatusCode) 429:
                    return LogLevel.Error;
            }

            // 4xx
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (httpStatusCode < HttpStatusCode.InternalServerError)
            {
                return LogLevel.Trace;
            }

            return LogLevel.Error;
        }

    }

}
