﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ModalLayer.Modal;
using Newtonsoft.Json;
using OnlineDataBuilder.ContextHandler;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace SchoolInMindServer.MiddlewareServices
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        public static bool LoggingFlag = false;
        public static string FileLocation;

        public ExceptionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, ApplicationConfiguration applicationConfiguration)
        {
            try
            {
                await _next.Invoke(context);
            }
            catch (HiringBellException exception)
            {
                if (applicationConfiguration.IsLoggingEnabled)
                    await HandleExceptionWriteToFile(context, exception, applicationConfiguration);
                else
                    await HandleHiringBellExceptionMessageAsync(context, exception);
            }
            catch (Exception ex)
            {
                if (applicationConfiguration.IsLoggingEnabled)
                    await HandleExceptionWriteToFile(context, ex, applicationConfiguration);
                else
                    await HandleExceptionMessageAsync(context, ex);
            }
        }

        private static async Task<Task> HandleHiringBellExceptionMessageAsync(HttpContext context, HiringBellException e)
        {
            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            int statusCode = (int)e.HttpStatusCode;
            var result = JsonConvert.SerializeObject(new ApiResponse
            {
                AuthenticationToken = string.Empty,
                HttpStatusCode = e.HttpStatusCode,
                HttpStatusMessage = e.UserMessage,
                ResponseBody = new { e.UserMessage, InnerMessage = e.InnerException?.Message, e.StackTraceDetail }
            });

            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            context.Response.StatusCode = statusCode;
            return await Task.FromResult(context.Response.WriteAsync(result));
        }

        private static async Task<Task> HandleExceptionWriteToFile(HttpContext context, HiringBellException e, ApplicationConfiguration applicationConfiguration)
        {
            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            int statusCode = (int)e.HttpStatusCode;
            var result = new ApiResponse
            {
                AuthenticationToken = string.Empty,
                HttpStatusCode = e.HttpStatusCode,
                HttpStatusMessage = e.UserMessage
            };

            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            context.Response.StatusCode = statusCode;
            await Task.Run(() =>
            {
                var path = Path.Combine(applicationConfiguration.LoggingFilePath, DateTime.Now.ToString("dd_MM_yyyy") + ".txt");
                result.ResponseBody = new { e.UserMessage, InnerMessage = e.InnerException?.Message, e.StackTrace };
                File.AppendAllTextAsync(path, JsonConvert.SerializeObject(result));
            });

            result.ResponseBody = new { e.UserMessage, InnerMessage = e.InnerException?.Message };
            return await Task.FromResult(context.Response.WriteAsync(JsonConvert.SerializeObject(result)));
        }

        private static async Task<Task> HandleExceptionWriteToFile(HttpContext context, Exception e, ApplicationConfiguration applicationConfiguration)
        {
            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            int statusCode = (int)HttpStatusCode.InternalServerError;
            var result = new ApiResponse
            {
                AuthenticationToken = string.Empty,
                HttpStatusCode = HttpStatusCode.InternalServerError,
                HttpStatusMessage = e.Message
            };

            context.Response.ContentType = ApplicationConstants.ApplicationJson;
            context.Response.StatusCode = statusCode;
            await Task.Run(() =>
            {
                var path = Path.Combine(applicationConfiguration.LoggingFilePath, DateTime.Now.ToString("dd_MM_yyyy") + ".txt");
                result.ResponseBody = new { e.Message, InnerMessage = e.InnerException?.Message, e.StackTrace };
                File.AppendAllTextAsync(path, JsonConvert.SerializeObject(result));
            });

            result.ResponseBody = new { e.Message, InnerMessage = e.InnerException?.Message };
            return await Task.FromResult(context.Response.WriteAsync(JsonConvert.SerializeObject(result)));
        }

        private static Task HandleExceptionMessageAsync(HttpContext context, Exception e)
        {
            context.Response.ContentType = "application/json";
            int statusCode = (int)HttpStatusCode.InternalServerError;
            var result = JsonConvert.SerializeObject(new ApiResponse
            {
                AuthenticationToken = string.Empty,
                HttpStatusCode = HttpStatusCode.BadRequest,
                HttpStatusMessage = e.Message,
                ResponseBody = new { e.Message, InnerMessage = e.InnerException?.Message }
            });
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsync(result);
        }
    }
}
