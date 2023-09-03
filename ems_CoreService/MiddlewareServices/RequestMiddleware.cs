﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using BottomhalfCore.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ModalLayer.Modal;
using Newtonsoft.Json;
using ServiceLayer.Code;
using ServiceLayer.Interface;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SchoolInMindServer.MiddlewareServices
{
    public class RequestMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration configuration;
        private readonly string TokenName = "Authorization";
        private readonly ITimezoneConverter _timezoneConverter;
        private IDb _db;

        public RequestMiddleware(RequestDelegate next,
            IConfiguration configuration,
            ITimezoneConverter timezoneConverter)
        {
            this.configuration = configuration;
            _timezoneConverter = timezoneConverter;
            _next = next;
        }

        public async Task Invoke(HttpContext context, CurrentSession currentSession, IDb db)
        {
            try
            {
                DbConfigModal dbConfig = null;
                Parallel.ForEach(context.Request.Headers, header =>
                {
                    if (header.Value.FirstOrDefault() != null)
                    {
                        if (header.Key == TokenName)
                            currentSession.Authorization = header.Value.FirstOrDefault();

                        if (header.Key == "database")
                            dbConfig = JsonConvert.DeserializeObject<DbConfigModal>(header.Value);
                    }
                });

                string userId = string.Empty;
                if (!string.IsNullOrEmpty(currentSession.Authorization))
                {
                    string token = currentSession.Authorization.Replace(ApplicationConstants.JWTBearer, "").Trim();
                    if (!string.IsNullOrEmpty(token) && token != "null")
                    {
                        var handler = new JwtSecurityTokenHandler();
                        handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = false,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = configuration["jwtSetting:Issuer"],
                            ValidAudience = configuration["jwtSetting:Issuer"],
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["jwtSetting:Key"]))
                        }, out SecurityToken validatedToken);

                        JwtSecurityToken securityToken = handler.ReadToken(token) as JwtSecurityToken;
                        ReadToken(securityToken, currentSession);

                        var cs = @$"server={dbConfig.Server};port={dbConfig.Port};database={dbConfig.Database};User Id={dbConfig.UserId};password={dbConfig.Password};Connection Timeout={dbConfig.ConnectionTimeout};Connection Lifetime={dbConfig.ConnectionLifetime};Min Pool Size={dbConfig.MinPoolSize};Max Pool Size={dbConfig.MaxPoolSize};Pooling={dbConfig.Pooling};";
                        db.SetupConnectionString(cs);
                    }
                    else if (dbConfig != null)
                    {
                        var cs = @$"server={dbConfig.Server};port={dbConfig.Port};database={dbConfig.Database};User Id={dbConfig.UserId};password={dbConfig.Password};Connection Timeout={dbConfig.ConnectionTimeout};Connection Lifetime={dbConfig.ConnectionLifetime};Min Pool Size={dbConfig.MinPoolSize};Max Pool Size={dbConfig.MaxPoolSize};Pooling={dbConfig.Pooling};";
                        db.SetupConnectionString(cs);
                    }
                }

                await _next(context);
            }
            catch (HiringBellException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void ReadToken(JwtSecurityToken securityToken, CurrentSession currentSession)
        {
            var userDetail = securityToken.Claims.FirstOrDefault(x => x.Type == ApplicationConstants.JBot).Value;
            currentSession.CompanyCode = securityToken.Claims.FirstOrDefault(x => x.Type == ApplicationConstants.CompanyCode).Value;
            currentSession.CurrentUserDetail = JsonConvert.DeserializeObject<UserDetail>(userDetail);

            currentSession.CurrentUserDetail.RoleId = currentSession.CurrentUserDetail.UserTypeId;

            if (currentSession.CurrentUserDetail == null)
                throw new HiringBellException("Invalid token found. Please contact to admin.");

            if (currentSession.CurrentUserDetail.OrganizationId <= 0
            || currentSession.CurrentUserDetail.CompanyId <= 0)
                throw new HiringBellException("Invalid Organization id or Company id. Please contact to admin.");

            currentSession.CurrentUserDetail.FullName = currentSession.CurrentUserDetail.FirstName
            + " " +
            currentSession.CurrentUserDetail.LastName;

            currentSession.TimeZone = TZConvert.GetTimeZoneInfo("India Standard Time");
            currentSession.TimeZoneNow = _timezoneConverter.ToTimeZoneDateTime(DateTime.UtcNow, currentSession.TimeZone);
        }
    }
}
