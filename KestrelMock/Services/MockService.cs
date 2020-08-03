﻿using KestrelMock.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace KestrelMock.Services
{
    public class MockService
    {
        private readonly MockConfiguration _mockConfiguration;
        private readonly RequestDelegate _next;

        public MockService(IOptions<MockConfiguration> options, RequestDelegate next)
        {
            _mockConfiguration = options.Value;
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await InvokeMock(context);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var errorResponse = new
                {
                    error = ex.ToString()
                };

                await context.Response.WriteAsync(JsonConvert.SerializeObject(errorResponse));
            }
            
            //breakes execution
            //await _next(context);
        }

        protected virtual async Task<bool> InvokeMock(HttpContext context)
        {
            // TODO we may want to cache this instead of loading mappings with each request.
            var mappings = await InputMappingParser.ParsePathMappings(_mockConfiguration);

            string path = context.Request.GetEncodedPathAndQuery();

            string body = null;

            if (context.Request.Body != null)
            {
                using StreamReader reader = new StreamReader(context.Request.Body);
                body = await reader.ReadToEndAsync();
            }

            var matchResult = ResponseMatcher.FindMatchingResponseMock(path, body, context.Request.Method, mappings);

            if (matchResult is null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return true;
            }

            if (matchResult.Headers?.Any() == true)
            {
                foreach (var header in matchResult.Headers)
                {
                    foreach (var key in header.Keys)
                    {
                        context.Response.Headers.Add(key, header[key]);
                    }
                }
            }

            context.Response.StatusCode = matchResult.Status;

            if (!string.IsNullOrWhiteSpace(matchResult.Body))
            {
                string resultBody = matchResult.Body;

                if (matchResult.Replace != null)
                {
                    resultBody = BodyWriterService.UpdateBody(path, matchResult, resultBody);
                }

                await context.Response.WriteAsync(resultBody);
            }

            return true;
        }
    }

}
