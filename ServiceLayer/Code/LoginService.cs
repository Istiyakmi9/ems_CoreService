﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using EMailService.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModalLayer.Modal;
using Newtonsoft.Json;
using ServiceLayer.Code.SendEmail;
using ServiceLayer.Interface;
using System;
using System.Data;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ServiceLayer.Code
{
    public class LoginService : ILoginService
    {
        private readonly IDb db;
        private readonly JwtSetting _jwtSetting;
        private readonly IAuthenticationService _authenticationService;
        private readonly IConfiguration _configuration;
        private readonly IEMailManager _emailManager;
        private readonly ICommonService _commonService;
        private readonly ForgotPasswordEmailService _forgotPasswordEmailService;
        private readonly CurrentSession _currentSession;

        public LoginService(IDb db, IOptions<JwtSetting> options,
            IAuthenticationService authenticationService,
            IEMailManager emailManager,
            ICommonService commonService,
            IConfiguration configuration,
            ForgotPasswordEmailService forgotPasswordEmailService,
            CurrentSession currentSession)
        {
            this.db = db;
            _configuration = configuration;
            _jwtSetting = options.Value;
            _authenticationService = authenticationService;
            _emailManager = emailManager;
            _commonService = commonService;
            _forgotPasswordEmailService = forgotPasswordEmailService;
            _currentSession = currentSession;
        }

        public Boolean RemoveUserDetailService(string Token)
        {
            Boolean Flag = false;
            return Flag;
        }
        public UserDetail GetUserDetail(AuthUser authUser)
        {
            UserDetail userDetail = this.db.Get<UserDetail>("sp_UserDetail_GetByMobileOrEmail", new
            {
                email = authUser.Email,
                mobile = authUser.MobileNo,
            });

            return userDetail;
        }

        public string GetUserLoginDetail(UserDetail authUser)
        {
            string encryptedPassword = string.Empty;

            if (!string.IsNullOrEmpty(authUser.EmailId))
                authUser.EmailId = authUser.EmailId.Trim().ToLower();

            var loginDetail = db.Get<UserDetail>("sp_password_get_by_email_mobile", new
            {
                authUser.UserId,
                MobileNo = authUser.Mobile,
                authUser.EmailId
            });

            if (loginDetail != null)
            {
                encryptedPassword = loginDetail.Password;
                authUser.OrganizationId = loginDetail.OrganizationId;
                authUser.CompanyId = loginDetail.CompanyId;
                authUser.UserTypeId = loginDetail.UserTypeId;
            }
            else
            {
                throw new HiringBellException("Please enter a valid email address or mobile number.");
            }

            return encryptedPassword;
        }

        public string FetchUserLoginDetail(UserDetail authUser)
        {
            string encryptedPassword = string.Empty;

            if (!string.IsNullOrEmpty(authUser.EmailId))
                authUser.EmailId = authUser.EmailId.Trim().ToLower();

            var loginDetail = db.Get<UserDetail>("sp_password_get", new
            {
                authUser.UserId,
                MobileNo = authUser.Mobile,
                authUser.EmailId
            });

            if (loginDetail != null)
            {
                encryptedPassword = loginDetail.Password;
                authUser.OrganizationId = loginDetail.OrganizationId;
                authUser.CompanyId = loginDetail.CompanyId;
            }
            else
            {
                throw new HiringBellException("Fail to retrieve user detail.", "UserDetail", JsonConvert.SerializeObject(authUser));
            }

            return encryptedPassword;
        }
        public async Task<LoginResponse> FetchAuthenticatedProviderDetail(UserDetail authUser)
        {
            string ProcedureName = string.Empty;
            if (authUser.UserTypeId == (int)UserType.Admin)
                ProcedureName = "sp_Userlogin_Auth";
            else if (authUser.UserTypeId == (int)UserType.Employee)
                ProcedureName = "sp_Employeelogin_Auth";
            else
                throw new HiringBellException("UserType is invalid. Only system user allowed");

            LoginResponse loginResponse = default;
            if ((!string.IsNullOrEmpty(authUser.EmailId) || !string.IsNullOrEmpty(authUser.Mobile)) && !string.IsNullOrEmpty(authUser.Password))
            {
                loginResponse = await FetchUserDetail(authUser, ProcedureName);
            }

            return loginResponse;
        }

        public async Task<LoginResponse> AuthenticateUser(UserDetail authUser)
        {
            LoginResponse loginResponse = default;

            if ((!string.IsNullOrEmpty(authUser.EmailId) || !string.IsNullOrEmpty(authUser.Mobile)) && !string.IsNullOrEmpty(authUser.Password))
            {
                var encryptedPassword = this.GetUserLoginDetail(authUser);
                encryptedPassword = _authenticationService.Decrypt(encryptedPassword, _configuration.GetSection("EncryptSecret").Value);
                if (encryptedPassword.CompareTo(authUser.Password) != 0)
                {
                    throw new HiringBellException("Invalid userId or password.");
                }

                loginResponse = await FetchUserDetail(authUser, "sp_Employeelogin_Auth");
            }

            return await Task.FromResult(loginResponse);
        }

        private async Task<LoginResponse> FetchUserDetail(UserDetail authUser, string ProcedureName)
        {
            LoginResponse loginResponse = default;
            DataSet ds = await db.GetDataSetAsync(ProcedureName, new
            {
                UserId = authUser.UserId,
                MobileNo = authUser.Mobile,
                EmailId = authUser.EmailId,
                UserTypeId = authUser.UserTypeId,
                PageSize = 1000
            });

            if (ds != null && ds.Tables.Count == 6)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    loginResponse = new LoginResponse();
                    var loginDetail = Converter.ToType<LoginDetail>(ds.Tables[0]);
                    loginResponse.Menu = ds.Tables[1];
                    loginResponse.Department = ds.Tables[3];
                    loginResponse.Roles = ds.Tables[4];
                    loginResponse.UserTypeId = authUser.UserTypeId;
                    var companies = Converter.ToList<Organization>(ds.Tables[5]);
                    loginResponse.Companies = companies.FindAll(x => x.OrganizationId == loginDetail.OrganizationId);
                    var currentCompany = loginResponse.Companies.Find(x => x.CompanyId == loginDetail.CompanyId);
                    loginResponse.EmployeeList = ds.Tables[2].AsEnumerable()
                                                   .Select(x => new AutoCompleteEmployees
                                                   {
                                                       value = x.Field<long>("I"),
                                                       text = x.Field<string>("N"),
                                                       email = x.Field<string>("E"),
                                                       selected = false
                                                   }).ToList<AutoCompleteEmployees>();

                    if (loginDetail != null && currentCompany != null)
                    {
                        var userDetail = new UserDetail
                        {
                            FirstName = loginDetail.FirstName,
                            LastName = loginDetail.LastName,
                            Address = loginDetail.Address,
                            Mobile = loginDetail.Mobile,
                            Email = loginDetail.Email,
                            UserId = loginDetail.UserId,
                            CompanyName = currentCompany.CompanyName,
                            UserTypeId = loginDetail.UserTypeId,
                            OrganizationId = loginDetail.OrganizationId,
                            CompanyId = loginDetail.CompanyId,
                            DesignationId = loginDetail.DesignationId,
                            ManagerName = loginDetail.ManagerName,
                            ReportingManagerId = loginDetail.ReportingManagerId,
                            UpdatedOn = loginDetail.UpdatedOn,
                            EmployeeCurrentRegime = loginDetail.EmployeeCurrentRegime,
                            DOB = loginDetail.DOB,
                            CreatedOn = loginDetail.CreatedOn,
                            WorkShiftId = loginDetail.WorkShiftId,
                            RoleId = loginDetail.RoleId,
                            CompanyCode = authUser.CompanyCode
                        };

                        loginResponse.UserDetail = userDetail;
                        var _token = _authenticationService.Authenticate(userDetail);
                        if (_token != null)
                        {
                            userDetail.Token = _token.Token;
                            userDetail.TokenExpiryDuration = DateTime.Now.AddHours(_jwtSetting.AccessTokenExpiryTimeInSeconds);
                            userDetail.RefreshToken = _token.RefreshToken;
                        }
                    }
                    else
                    {
                        throw HiringBellException.ThrowBadRequest("Fail to get user detail. Please contact to admin.");
                    }
                }
            }

            return loginResponse;
        }

        public string ResetEmployeePassword(UserDetail authUser)
        {
            string Status = string.Empty;
            var encryptedPassword = this.FetchUserLoginDetail(authUser);
            encryptedPassword = _authenticationService.Decrypt(encryptedPassword, _configuration.GetSection("EncryptSecret").Value);
            if (encryptedPassword != authUser.Password)
                throw new HiringBellException("Incorrect old password");

            string newEncryptedPassword = _authenticationService.Encrypt(authUser.NewPassword, _configuration.GetSection("EncryptSecret").Value);
            var result = db.Execute<string>("sp_Reset_Password", new
            {
                EmailId = authUser.EmailId,
                MobileNo = authUser.Mobile,
                NewPassword = newEncryptedPassword,
            }, true);

            if (result == "Update")
            {
                Status = "Password changed successfully, Please logout and login again";
            }
            else
            {
                throw new HiringBellException("Unable to update your password");
            }

            return Status;
        }

        public async Task<bool> RegisterNewCompany(RegistrationForm registrationForm)
        {
            return await Task.Run(() =>
            {
                bool statusFlag = false;
                if (string.IsNullOrEmpty(registrationForm.OrganizationName))
                    throw new HiringBellException { UserMessage = $"Invalid Organization name passed: {registrationForm.OrganizationName}" };

                if (string.IsNullOrEmpty(registrationForm.CompanyName))
                    throw new HiringBellException { UserMessage = $"Invalid Company name passed: {registrationForm.CompanyName}" };

                if (string.IsNullOrEmpty(registrationForm.Mobile))
                    throw new HiringBellException { UserMessage = $"Invalid Mobile number: {registrationForm.Mobile}" };

                if (string.IsNullOrEmpty(registrationForm.EmailId))
                    throw new HiringBellException { UserMessage = $"Invalid Email address passed: {registrationForm.EmailId}" };

                if (string.IsNullOrEmpty(registrationForm.AuthenticationCode))
                    throw new HiringBellException { UserMessage = $"Invalid Authentication Code passed: {registrationForm.AuthenticationCode}" };

                registrationForm.FirstName = "Admin";
                registrationForm.LastName = "User";
                string EncreptedPassword = _authenticationService.Encrypt(
                    _configuration.GetSection("DefaultNewEmployeePassword").Value,
                    _configuration.GetSection("EncryptSecret").Value
                );
                registrationForm.Password = EncreptedPassword;

                var status = this.db.Execute<string>("sp_new_registration", new
                {
                    registrationForm.OrganizationName,
                    registrationForm.CompanyName,
                    registrationForm.Mobile,
                    registrationForm.EmailId,
                    registrationForm.FirstName,
                    registrationForm.LastName,
                    registrationForm.Password
                }, true);

                statusFlag = true;
                return statusFlag;
            });
        }

        public async Task<string> ForgotPasswordService(string email)
        {
            try
            {
                string Status = string.Empty;
                if (string.IsNullOrEmpty(email))
                    throw new HiringBellException("Email is null or empty");

                ValidateEmailId(email);
                UserDetail authUser = new UserDetail();
                authUser.EmailId = email;
                var encryptedPassword = this.FetchUserLoginDetail(authUser);

                if (string.IsNullOrEmpty(encryptedPassword))
                    throw new HiringBellException("Email id is not registered. Please contact to admin");

                var password = _authenticationService.Decrypt(encryptedPassword, _configuration.GetSection("EncryptSecret").Value);

                await _forgotPasswordEmailService.SendForgotPasswordEmail(password, email);
                Status = ApplicationConstants.Successfull;
                return Status;
            }
            catch (Exception)
            {
                throw new HiringBellException("Getting some server error. Please contact to admin.");
            }
        }

        private void BuildEmailBody(EmailTemplate emailTemplate, string password)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("<div>" + emailTemplate.Salutation + "</div>");
            string body = JsonConvert.DeserializeObject<string>(emailTemplate.BodyContent)
                          .Replace("[[NEW-PASSWORD]]", password);

            stringBuilder.Append("<div>" + emailTemplate.EmailClosingStatement + "</div>");
            stringBuilder.Append("<div>" + emailTemplate.SignatureDetail + "</div>");
            stringBuilder.Append("<div>" + emailTemplate.ContactNo + "</div>");

            emailTemplate.BodyContent = body + stringBuilder.ToString();
        }

        private void ValidateEmailId(string email)
        {
            var mail = new MailAddress(email);
            bool isValidEmail = mail.Host.Contains(".");
            if (!isValidEmail)
                throw new HiringBellException("The email is invalid");
        }
    }
}
