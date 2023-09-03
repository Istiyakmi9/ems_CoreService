﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using BottomhalfCore.Services.Interface;
using DocMaker.ExcelMaker;
using DocMaker.PdfService;
using EMailService.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModalLayer.Modal;
using ModalLayer.Modal.Accounts;
using ModalLayer.Modal.Leaves;
using Newtonsoft.Json;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using File = System.IO.File;

namespace ServiceLayer.Code
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IDb _db;
        private readonly CurrentSession _currentSession;
        private readonly IFileService _fileService;
        private readonly FileLocationDetail _fileLocationDetail;
        private readonly IConfiguration _configuration;
        private readonly IAuthenticationService _authenticationService;
        private readonly ILoginService _loginService;
        private readonly IDeclarationService _declarationService;
        private readonly ITimezoneConverter _timezoneConverter;
        private ILogger<EmployeeService> _logger;
        private readonly HtmlToPdfConverter _htmlToPdfConverter;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IEMailManager _eMailManager;
        private readonly ILeaveCalculation _leaveCalculation;
        private readonly ITimesheetService _timesheetService;
        private readonly ExcelWriter _excelWriter;

        public EmployeeService(IDb db,
            CurrentSession currentSession,
            IFileService fileService,
            IConfiguration configuration,
            ILoginService loginService,
            IDeclarationService declarationService,
            IAuthenticationService authenticationService,
            ITimezoneConverter timezoneConverter,
            ILogger<EmployeeService> logger,
            FileLocationDetail fileLocationDetail,
            HtmlToPdfConverter htmlToPdfConverter,
            IHostingEnvironment hostingEnvironment,
            ILeaveCalculation leaveCalculation,
            IEMailManager eMailManager,
            ITimesheetService timesheetService,
            ExcelWriter excelWriter)
        {
            _db = db;
            _leaveCalculation = leaveCalculation;
            _loginService = loginService;
            _authenticationService = authenticationService;
            _configuration = configuration;
            _currentSession = currentSession;
            _fileService = fileService;
            _fileLocationDetail = fileLocationDetail;
            _declarationService = declarationService;
            _timezoneConverter = timezoneConverter;
            _logger = logger;
            _htmlToPdfConverter = htmlToPdfConverter;
            _hostingEnvironment = hostingEnvironment;
            _eMailManager = eMailManager;
            _timesheetService = timesheetService;
            _excelWriter = excelWriter;
        }

        public dynamic GetBillDetailForEmployeeService(FilterModel filterModel)
        {
            filterModel.PageSize = 100;
            List<Employee> employees = GetEmployees(filterModel);
            employees = employees.FindAll(x => x.EmployeeUid != 1);
            List<Organization> organizations = _db.GetList<Organization>("sp_company_get");

            if (employees.Count == 0 || organizations.Count == 0)
                throw HiringBellException.ThrowBadRequest("Unable to get employee and company detail. Please contact to admin.");

            return new { Employees = employees, Organizations = organizations };
        }

        private List<Employee> FilterActiveEmployees(FilterModel filterModel)
        {
            List<Employee> employees = _db.GetList<Employee>("SP_Employee_GetAll", new
            {
                filterModel.SearchString,
                filterModel.SortBy,
                filterModel.PageIndex,
                filterModel.PageSize
            });
            return employees;
        }

        private List<Employee> FilterInActiveEmployees(FilterModel filterModel)
        {
            List<Employee> employees = new List<Employee>();

            List<EmployeeArchiveModal> employeeArchiveModal = _db.GetList<EmployeeArchiveModal>("SP_Employee_GetAllInActive", new
            {
                filterModel.SearchString,
                filterModel.SortBy,
                filterModel.PageIndex,
                filterModel.PageSize
            });

            if (employeeArchiveModal == null || employeeArchiveModal.Count == 0)
                return employees;

            EmployeeCompleteDetailModal employeeJson = null;
            foreach (var item in employeeArchiveModal)
            {
                employeeJson = JsonConvert.DeserializeObject<EmployeeCompleteDetailModal>(item.EmployeeCompleteJsonData);
                if (employeeJson != null)
                {
                    employees.Add(new Employee
                    {
                        FirstName = employeeJson.EmployeeDetail.FirstName,
                        LastName = employeeJson.EmployeeDetail.LastName,
                        Mobile = employeeJson.EmployeeDetail.Mobile,
                        EmployeeUid = item.EmployeeId,
                        Email = employeeJson.EmployeeDetail.Email,
                        LeavePlanId = employeeJson.EmployeeDetail.LeavePlanId,
                        IsActive = employeeJson.EmployeeDetail.IsActive,
                        AadharNo = employeeJson.EmployeeProfessionalDetail.AadharNo,
                        PANNo = employeeJson.EmployeeProfessionalDetail.PANNo,
                        AccountNumber = employeeJson.EmployeeProfessionalDetail.AccountNumber,
                        BankName = employeeJson.EmployeeProfessionalDetail.BankName,
                        IFSCCode = employeeJson.EmployeeProfessionalDetail.IFSCCode,
                        Domain = employeeJson.EmployeeProfessionalDetail.Domain,
                        Specification = employeeJson.EmployeeProfessionalDetail.Specification,
                        ExprienceInYear = employeeJson.EmployeeProfessionalDetail.ExperienceInYear,
                        ActualPackage = employeeJson.EmployeeDetail.ActualPackage,
                        FinalPackage = employeeJson.EmployeeDetail.FinalPackage,
                        TakeHomeByCandidate = employeeJson.EmployeeDetail.TakeHomeByCandidate,
                        ClientJson = employeeJson.EmployeeDetail.ClientJson,
                        Total = employeeArchiveModal[0].Total,
                        UpdatedOn = employeeJson.EmployeeProfessionalDetail.UpdatedOn,
                        CreatedOn = employeeJson.EmployeeProfessionalDetail.CreatedOn
                    });
                }
            }
            return employees;
        }

        public List<Employee> GetEmployees(FilterModel filterModel)
        {
            List<Employee> employees = null;
            if (string.IsNullOrEmpty(filterModel.SearchString))
                filterModel.SearchString = "1=1";


            if (filterModel.IsActive != null && filterModel.IsActive == true)
            {
                if (filterModel.CompanyId > 0)
                    filterModel.SearchString += $" and l.CompanyId = {filterModel.CompanyId} ";
                else
                    filterModel.SearchString += $" and l.CompanyId = {_currentSession.CurrentUserDetail.CompanyId} ";
                employees = FilterActiveEmployees(filterModel);

            }
            else
                employees = FilterInActiveEmployees(filterModel);

            return employees;
        }

        public List<AutoCompleteEmployees> EmployeesListDataService(FilterModel filterModel)
        {
            if (filterModel.CompanyId > 0)
                filterModel.SearchString += $" and l.CompanyId = {filterModel.CompanyId} ";
            else
                filterModel.SearchString += $" and l.CompanyId = {_currentSession.CurrentUserDetail.CompanyId} ";

            List<AutoCompleteEmployees> employees = _db.GetList<AutoCompleteEmployees>("SP_Employee_GetAll", new
            {
                filterModel.SearchString,
                filterModel.PageIndex,
                filterModel.PageSize,
                filterModel.CompanyId
            });

            if (employees == null)
                throw HiringBellException.ThrowBadRequest("Unable to load employee list data.");

            return employees;
        }

        public DataSet GetEmployeeLeaveDetailService(long EmployeeId)
        {
            var result = _db.FetchDataSet("sp_leave_detail_getby_employeeId", new
            {
                EmployeeId,
            });

            if (result == null || result.Tables.Count != 2)
                throw HiringBellException.ThrowBadRequest("Unable to get data.");
            else
            {
                result.Tables[0].TableName = "Employees";
                result.Tables[1].TableName = "LeavePlan";
            }

            return result;
        }

        public DataSet LoadMappedClientService(long EmployeeId)
        {
            var result = _db.FetchDataSet("sp_attandence_detail_by_employeeId", new
            {
                EmployeeId,
            });

            if (result == null || result.Tables.Count != 1)
                throw HiringBellException.ThrowBadRequest("Unable to get data.");
            else
            {
                result.Tables[0].TableName = "AllocatedClients";
            }

            return result;
        }

        public DataSet GetManageEmployeeDetailService(long EmployeeId)
        {
            var resultset = _db.GetDataSet("sp_manage_employee_detail_get", new
            {
                EmployeeId,
                _currentSession.CurrentUserDetail.CompanyId,
                _currentSession.CurrentUserDetail.OrganizationId,
            });

            if (resultset.Tables.Count == 11)
            {
                resultset.Tables[0].TableName = "Employee";
                resultset.Tables[1].TableName = "AllocatedClients";
                resultset.Tables[2].TableName = "FileDetail";
                resultset.Tables[3].TableName = "SalaryDetail";
                resultset.Tables[4].TableName = "Clients";
                resultset.Tables[5].TableName = "EmployeesList";
                resultset.Tables[6].TableName = "Roles";
                resultset.Tables[7].TableName = "LeavePlans";
                resultset.Tables[8].TableName = "Companies";
                resultset.Tables[9].TableName = "WorkShift";
                resultset.Tables[10].TableName = "SalaryGroup";
            }

            return resultset;
        }

        public DataSet GetManageClientService(long EmployeeId)
        {
            var resultset = _db.GetDataSet("SP_MappedClients_Get", new
            {
                employeeId = EmployeeId
            });

            if (resultset.Tables.Count == 1)
            {
                resultset.Tables[0].TableName = "AllocatedClients";
            }
            return resultset;
        }

        public DataSet UpdateEmployeeMappedClientDetailService(EmployeeMappedClient employeeMappedClient, bool IsUpdating)
        {
            if (employeeMappedClient.AssigneDate == null || employeeMappedClient.AssigneDate.Year <= 1900)
                throw HiringBellException.ThrowBadRequest("Assign date is a required field");

            if (employeeMappedClient.EmployeeUid <= 0)
                throw new HiringBellException { UserMessage = "Invalid EmployeeId.", FieldName = nameof(employeeMappedClient.EmployeeUid), FieldValue = employeeMappedClient.EmployeeUid.ToString() };

            if (employeeMappedClient.ClientUid <= 0)
                throw new HiringBellException { UserMessage = "Invalid ClientId.", FieldName = nameof(employeeMappedClient.ClientUid), FieldValue = employeeMappedClient.ClientUid.ToString() };

            var records = _db.GetList<EmployeeMappedClient>("sp_employees_mappedClient_get_by_employee_id", new
            {
                EmployeeId = employeeMappedClient.EmployeeUid
            });

            var first = records.Find(i => i.ClientUid == employeeMappedClient.ClientUid);
            if (first == null)
                first = employeeMappedClient;
            else
            {
                first.ActualPackage = employeeMappedClient.ActualPackage;
                first.ClientUid = employeeMappedClient.ClientUid;
                first.ClientName = employeeMappedClient.ClientName;
                first.FinalPackage = employeeMappedClient.FinalPackage;
                first.TakeHomeByCandidate = employeeMappedClient.TakeHomeByCandidate;
                first.IsPermanent = employeeMappedClient.IsPermanent;
                first.IsActive = employeeMappedClient.IsActive;
                first.BillingHours = employeeMappedClient.BillingHours;
                first.DaysPerWeek = employeeMappedClient.DaysPerWeek;
                first.DateOfJoining = employeeMappedClient.DateOfJoining;
                first.AssigneDate = employeeMappedClient.AssigneDate;
                first.DaysPerWeek = employeeMappedClient.DaysPerWeek;
            }
            this.ValidateEmployeeMapDetails(employeeMappedClient);

            var resultset = _db.GetDataSet("sp_employees_addupdate_remote_client", new
            {
                employeeMappedClientsUid = first.EmployeeMappedClientsUid,
                employeeUid = first.EmployeeUid,
                clientUid = first.ClientUid,
                finalPackage = first.FinalPackage,
                actualPackage = first.ActualPackage,
                takeHome = first.TakeHomeByCandidate,
                isPermanent = first.IsPermanent,
                BillingHours = first.BillingHours,
                DaysPerWeek = first.DaysPerWeek,
                DateOfLeaving = first.DateOfLeaving,
                AssigneDate = first.AssigneDate
            });

            if (!ApplicationConstants.ContainSingleRow(resultset))
                throw HiringBellException.ThrowBadRequest("Fail to insert or update record. Please contact to admin.");

            var weekFirstDay = _timezoneConverter.FirstDayOfWeekUTC(DateTime.UtcNow);
            _timesheetService.RunWeeklyTimesheetCreation(weekFirstDay.AddDays(1));
            return resultset;
        }

        public Employee GetEmployeeByIdService(int EmployeeId, int IsActive)
        {
            Employee employee = _db.Get<Employee>("SP_Employees_ById", new { EmployeeId = EmployeeId, IsActive = IsActive });
            return employee;
        }

        public EmployeeCompleteDetailModal GetEmployeeCompleteDetail(int EmployeeId)
        {
            DataSet ds = _db.FetchDataSet("sp_Employee_GetCompleteDetail", new { EmployeeId = EmployeeId });
            if (ds.Tables.Count != 10)
                throw HiringBellException.ThrowBadRequest("Unable to get employee completed detail");

            EmployeeCompleteDetailModal employeeCompleteDetailModal = new EmployeeCompleteDetailModal
            {
                EmployeeDetail = Converter.ToType<Employee>(ds.Tables[0]),
                PersonalDetail = Converter.ToType<EmployeePersonalDetail>(ds.Tables[1]),
                EmployeeProfessionalDetail = Converter.ToType<EmployeeProfessionDetail>(ds.Tables[2]),
                EmployeeLoginDetail = Converter.ToType<LoginDetail>(ds.Tables[3]),
                EmployeeDeclarations = Converter.ToType<EmployeeDeclaration>(ds.Tables[4]),
                LeaveRequestDetail = Converter.ToType<Leave>(ds.Tables[5]),
                NoticePeriod = Converter.ToType<EmployeeNoticePeriod>(ds.Tables[6]),
                SalaryDetail = Converter.ToType<EmployeeSalaryDetail>(ds.Tables[7]),
                TimesheetDetails = Converter.ToType<TimesheetDetail>(ds.Tables[8]),
                MappedClient = Converter.ToType<EmployeeMappedClient>(ds.Tables[9])
            };

            return employeeCompleteDetailModal;
        }

        private EmployeeArchiveModal GetEmployeeArcheiveCompleteDetail(long EmployeeId)
        {
            EmployeeArchiveModal employeeArcheiveDeatil = _db.Get<EmployeeArchiveModal>("sp_Employee_GetArcheiveCompleteDetail", new { EmployeeId = EmployeeId });
            return employeeArcheiveDeatil;
        }

        private string DeActivateEmployee(int EmployeeId)
        {
            EmployeeCompleteDetailModal employeeCompleteDetailModal = GetEmployeeCompleteDetail(EmployeeId);
            employeeCompleteDetailModal.EmployeeDetail.IsActive = false;
            var result = _db.Execute<EmployeeArchiveModal>("sp_Employee_DeActivate", new
            {
                EmployeeId,
                FullName = string.Concat(employeeCompleteDetailModal.EmployeeDetail.FirstName, " ", employeeCompleteDetailModal.EmployeeDetail.LastName),
                Mobile = employeeCompleteDetailModal.EmployeeDetail.Mobile,
                Email = employeeCompleteDetailModal.EmployeeDetail.Email,
                Package = employeeCompleteDetailModal.EmployeeDetail.FinalPackage,
                DateOfJoining = employeeCompleteDetailModal.EmployeeDetail.CreatedOn,
                DateOfLeaving = DateTime.UtcNow,
                EmployeeCompleteDetailModal = JsonConvert.SerializeObject(employeeCompleteDetailModal),
                AdminId = _currentSession.CurrentUserDetail.UserId
            }, true);
            if (string.IsNullOrEmpty(result))
                throw HiringBellException.ThrowBadRequest("Unable to dea-active the employee. Please contact to admin");

            return result;
        }

        private string ActivateEmployee(int EmployeeId)
        {
            EmployeeArchiveModal employeeArchiveDetail = GetEmployeeArcheiveCompleteDetail(EmployeeId);
            if (employeeArchiveDetail == null)
                throw HiringBellException.ThrowBadRequest("No record found");

            string newEncryptedPassword = _authenticationService.Encrypt(_configuration.GetSection("DefaultNewEmployeePassword").Value, _configuration.GetSection("EncryptSecret").Value);
            EmployeeCompleteDetailModal employeeCompleteDetailModal = JsonConvert.DeserializeObject<EmployeeCompleteDetailModal>(employeeArchiveDetail.EmployeeCompleteJsonData);
            var result = _db.Execute<EmployeeCompleteDetailModal>("sp_Employee_Activate", new
            {
                EmployeeId = employeeArchiveDetail.EmployeeId,
                FirstName = employeeCompleteDetailModal.EmployeeDetail.FirstName,
                LastName = employeeCompleteDetailModal.EmployeeDetail.LastName,
                Mobile = employeeCompleteDetailModal.EmployeeDetail.Mobile,
                Email = employeeCompleteDetailModal.EmployeeDetail.Email,
                IsActive = true,
                ReportingManagerId = employeeCompleteDetailModal.EmployeeDetail.ReportingManagerId,
                DesignationId = employeeCompleteDetailModal.EmployeeDetail.DesignationId,
                UserTypeId = employeeCompleteDetailModal.EmployeeDetail.UserTypeId,
                LeavePlanId = employeeCompleteDetailModal.EmployeeDetail.LeavePlanId,
                PayrollGroupId = employeeCompleteDetailModal.EmployeeDetail.PayrollGroupId,
                SalaryGroupId = employeeCompleteDetailModal.EmployeeDetail.SalaryGroupId,
                CompanyId = employeeCompleteDetailModal.EmployeeDetail.CompanyId,
                NoticePeriodId = employeeCompleteDetailModal.EmployeeDetail.NoticePeriodId,
                SecondaryMobile = employeeCompleteDetailModal.EmployeeDetail.SecondaryMobile,
                PANNo = employeeCompleteDetailModal.EmployeeProfessionalDetail.PANNo,
                AadharNo = employeeCompleteDetailModal.EmployeeProfessionalDetail.AadharNo,
                AccountNumber = employeeCompleteDetailModal.EmployeeProfessionalDetail.AccountNumber,
                BankName = employeeCompleteDetailModal.EmployeeProfessionalDetail.BankName,
                BranchName = employeeCompleteDetailModal.EmployeeProfessionalDetail.BranchName,
                IFSCCode = employeeCompleteDetailModal.EmployeeProfessionalDetail.IFSCCode,
                Domain = employeeCompleteDetailModal.EmployeeProfessionalDetail.Domain,
                Specification = employeeCompleteDetailModal.EmployeeProfessionalDetail.Specification,
                ExprienceInYear = employeeCompleteDetailModal.EmployeeProfessionalDetail.ExperienceInYear,
                LastCompanyName = employeeCompleteDetailModal.EmployeeProfessionalDetail.LastCompanyName,
                ProfessionalDetail_Json = string.IsNullOrEmpty(employeeCompleteDetailModal.EmployeeProfessionalDetail.ProfessionalDetail_Json) ? "{}" : employeeCompleteDetailModal.EmployeeProfessionalDetail.ProfessionalDetail_Json,
                Gender = employeeCompleteDetailModal.PersonalDetail.Gender,
                FatherName = employeeCompleteDetailModal.PersonalDetail.FatherName,
                SpouseName = employeeCompleteDetailModal.PersonalDetail.SpouseName,
                MotherName = employeeCompleteDetailModal.PersonalDetail.MotherName,
                Address = employeeCompleteDetailModal.PersonalDetail.Address,
                State = employeeCompleteDetailModal.PersonalDetail.State,
                City = employeeCompleteDetailModal.PersonalDetail.City,
                Pincode = employeeCompleteDetailModal.PersonalDetail.Pincode,
                IsPermanent = employeeCompleteDetailModal.PersonalDetail.IsPermanent,
                ActualPackage = employeeCompleteDetailModal.PersonalDetail.ActualPackage,
                FinalPackage = employeeCompleteDetailModal.PersonalDetail.FinalPackage,
                TakeHomeByCandidate = employeeCompleteDetailModal.PersonalDetail.TakeHomeByCandidate,
                AccessLevelId = employeeCompleteDetailModal.EmployeeLoginDetail.AccessLevelId,
                Password = newEncryptedPassword,
                EmployeeDeclarationId = employeeCompleteDetailModal.EmployeeDeclarations.EmployeeDeclarationId,
                DocumentPath = employeeCompleteDetailModal.EmployeeDeclarations.DocumentPath,
                DeclarationDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.EmployeeDeclarations.DeclarationDetail) ? "[]" : employeeCompleteDetailModal.EmployeeDeclarations.DeclarationDetail,
                HouseRentDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.EmployeeDeclarations.HouseRentDetail) ? "[]" : employeeCompleteDetailModal.EmployeeDeclarations.HouseRentDetail,
                TotalDeclaredAmount = employeeCompleteDetailModal.EmployeeDeclarations.TotalAmount,
                TotalApprovedAmount = 0,
                LeaveRequestId = employeeCompleteDetailModal.LeaveRequestDetail.LeaveRequestId,
                LeaveDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.LeaveRequestDetail.LeaveDetail) ? "[]" : employeeCompleteDetailModal.LeaveRequestDetail.LeaveDetail,
                Year = employeeCompleteDetailModal.LeaveRequestDetail.Year,
                EmployeeNoticePeriodId = employeeCompleteDetailModal.NoticePeriod.EmployeeNoticePeriodId,
                ApprovedOn = employeeCompleteDetailModal.NoticePeriod.ApprovedOn,
                ApplicableFrom = employeeCompleteDetailModal.NoticePeriod.ApplicableFrom,
                ApproverManagerId = employeeCompleteDetailModal.NoticePeriod.ApproverManagerId,
                ManagerDescription = employeeCompleteDetailModal.NoticePeriod.ManagerDescription,
                AttachmentPath = employeeCompleteDetailModal.NoticePeriod.AttachmentPath,
                EmailTitle = employeeCompleteDetailModal.NoticePeriod.EmailTitle,
                OtherApproverManagerIds = string.IsNullOrEmpty(employeeCompleteDetailModal.NoticePeriod.OtherApproverManagerIds) ? "[]" : employeeCompleteDetailModal.NoticePeriod.OtherApproverManagerIds,
                ITClearanceStatus = employeeCompleteDetailModal.NoticePeriod.ITClearanceStatus,
                ReportingManagerClearanceStatus = employeeCompleteDetailModal.NoticePeriod.ReportingManagerClearanceStatus,
                CanteenClearanceStatus = employeeCompleteDetailModal.NoticePeriod.CanteenClearanceStatus,
                ClientClearanceStatus = employeeCompleteDetailModal.NoticePeriod.ClientClearanceStatus,
                HRClearanceStatus = employeeCompleteDetailModal.NoticePeriod.HRClearanceStatus,
                OfficialLastWorkingDay = employeeCompleteDetailModal.NoticePeriod.OfficialLastWorkingDay,
                PeriodDuration = employeeCompleteDetailModal.NoticePeriod.PeriodDuration,
                EarlyLeaveStatus = employeeCompleteDetailModal.NoticePeriod.EarlyLeaveStatus,
                Reason = employeeCompleteDetailModal.NoticePeriod.Reason,
                CTC = employeeCompleteDetailModal.SalaryDetail.CTC,
                GrossIncome = employeeCompleteDetailModal.SalaryDetail.GrossIncome,
                NetSalary = employeeCompleteDetailModal.SalaryDetail.NetSalary,
                CompleteSalaryDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.SalaryDetail.CompleteSalaryDetail) ? "[]" : employeeCompleteDetailModal.SalaryDetail.CompleteSalaryDetail,
                GroupId = employeeCompleteDetailModal.SalaryDetail.GroupId,
                TaxDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.SalaryDetail.TaxDetail) ? "[]" : employeeCompleteDetailModal.SalaryDetail.TaxDetail,
                TimesheetId = employeeCompleteDetailModal.TimesheetDetails.TimesheetId,
                ClientId = employeeCompleteDetailModal.TimesheetDetails.ClientId,
                TimesheetWeeklyJson = string.IsNullOrEmpty(employeeCompleteDetailModal.TimesheetDetails.TimesheetWeeklyJson) ? "[]" : employeeCompleteDetailModal.TimesheetDetails.TimesheetWeeklyJson,
                ExpectedBurnedMinutes = employeeCompleteDetailModal.TimesheetDetails.ExpectedBurnedMinutes,
                ActualBurnedMinutes = employeeCompleteDetailModal.TimesheetDetails.ActualBurnedMinutes,
                TotalWeekDays = employeeCompleteDetailModal.TimesheetDetails.TotalWeekDays,
                TotalWorkingDays = employeeCompleteDetailModal.TimesheetDetails.TotalWorkingDays,
                TimesheetStatus = employeeCompleteDetailModal.TimesheetDetails.TimesheetStatus,
                MonthTimesheetApprovalState = employeeCompleteDetailModal.TimesheetDetails.TimesheetStatus,
                TimesheetStartDate = employeeCompleteDetailModal.TimesheetDetails.TimesheetStartDate,
                TimesheetEndDate = employeeCompleteDetailModal.TimesheetDetails.TimesheetEndDate,
                UserComments = employeeCompleteDetailModal.TimesheetDetails.UserComments,
                IsSaved = employeeCompleteDetailModal.TimesheetDetails.IsSaved,
                IsSubmitted = employeeCompleteDetailModal.TimesheetDetails.IsSubmitted,
                ForYear = employeeCompleteDetailModal.TimesheetDetails.ForYear,
                EmployeeMappedClientUid = employeeCompleteDetailModal.MappedClient.EmployeeMappedClientsUid,
                ClientName = employeeCompleteDetailModal.MappedClient.ClientName,
                BillingHours = employeeCompleteDetailModal.MappedClient.BillingHours,
                DaysPerWeek = employeeCompleteDetailModal.MappedClient.DaysPerWeek,
                DateOfJoining = employeeCompleteDetailModal.MappedClient.DateOfJoining,
                DateOfLeaving = employeeCompleteDetailModal.MappedClient.DateOfLeaving,
                DOB = employeeCompleteDetailModal.EmployeeDetail.DOB,
                OrganizationId = employeeCompleteDetailModal.EmployeeLoginDetail.OrganizationId,
                AvailableLeaves = employeeCompleteDetailModal.LeaveRequestDetail.AvailableLeaves,
                TotalLeaveApplied = employeeCompleteDetailModal.LeaveRequestDetail.TotalLeaveApplied,
                TotalApprovedLeave = employeeCompleteDetailModal.LeaveRequestDetail.TotalApprovedLeave,
                TotalLeaveQuota = employeeCompleteDetailModal.LeaveRequestDetail.TotalLeaveQuota,
                TotalRejectedAmount = employeeCompleteDetailModal.EmployeeDeclarations.TotalRejectedAmount,
                EmployeeCurrentRegime = employeeCompleteDetailModal.EmployeeDeclarations.EmployeeCurrentRegime,
                DeclarationStartMonth = employeeCompleteDetailModal.EmployeeDeclarations.DeclarationStartMonth,
                DeclarationEndMonth = employeeCompleteDetailModal.EmployeeDeclarations.DeclarationEndMonth,
                DeclarationFromYear = employeeCompleteDetailModal.EmployeeDeclarations.DeclarationFromYear,
                DeclarationToYear = employeeCompleteDetailModal.EmployeeDeclarations.DeclarationToYear,
                WorkShiftId = employeeCompleteDetailModal.EmployeeDetail.WorkShiftId,
                AdminId = _currentSession.CurrentUserDetail.UserId,
                LeaveQuotaDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.LeaveRequestDetail.LeaveQuotaDetail) ?
                                    ApplicationConstants.EmptyJsonArray : employeeCompleteDetailModal.LeaveRequestDetail.LeaveQuotaDetail,
                IsPending = false,
                NewSalaryDetail = string.IsNullOrEmpty(employeeCompleteDetailModal.SalaryDetail.NewSalaryDetail) ?
                                    ApplicationConstants.EmptyJsonArray : employeeCompleteDetailModal.SalaryDetail.NewSalaryDetail,
                AssigneDate = employeeCompleteDetailModal.MappedClient.AssigneDate,
                FinancialStartYear = employeeCompleteDetailModal.EmployeeDeclarations.DeclarationFromYear
            }, true);

            if (string.IsNullOrEmpty(result))
                throw HiringBellException.ThrowBadRequest("Unable to active the employee. Please contact to admin");

            return result;
        }

        public List<Employee> ActivateOrDeActiveEmployeeService(int EmployeeId, bool IsActive)
        {
            if (EmployeeId == 1)
                throw HiringBellException.ThrowBadRequest("You can't delete the admin");

            List<Employee> employees = null;
            var status = string.Empty;
            FilterModel filterModel = new FilterModel
            {
                SearchString = "1=1",
                SortBy = "",
                PageIndex = 1,
                PageSize = 10
            };
            if (IsActive)
            {
                status = DeActivateEmployee(EmployeeId);
                employees = FilterInActiveEmployees(filterModel);
            }
            else
            {
                status = ActivateEmployee(EmployeeId);
                employees = FilterActiveEmployees(filterModel);
            }
            return employees;
        }

        public async Task<string> UpdateEmployeeService(Employee employee, IFormFileCollection fileCollection)
        {
            if (employee.EmployeeUid <= 0)
                throw new HiringBellException { UserMessage = "Invalid EmployeeId.", FieldName = nameof(employee.EmployeeUid), FieldValue = employee.EmployeeUid.ToString() };

            EmployeeCalculation employeeCalculation = new EmployeeCalculation();
            employeeCalculation.employee = employee;
            EmployeeEmailMobileCheck employeeEmailMobileCheck = this.GetEmployeeDetail(employeeCalculation);
            var numOfYears = employeeCalculation.Doj.Year - employeeCalculation.companySetting.FinancialYear;
            if ((numOfYears == 0 || numOfYears == 1)
                &&
                employeeCalculation.Doj.Month >= employeeCalculation.companySetting.DeclarationStartMonth
                &&
                employeeCalculation.Doj.Month <= employeeCalculation.companySetting.DeclarationEndMonth)
                employeeCalculation.IsFirstYearDeclaration = true;
            else
                employeeCalculation.IsFirstYearDeclaration = false;

            if (employeeEmailMobileCheck.EmployeeCount == 0)
                throw HiringBellException.ThrowBadRequest("Employee record not found. Please contact to admin.");

            return await RegisterOrUpdateEmployeeDetail(employeeCalculation, fileCollection);
        }

        public async Task<string> RegisterEmployeeService(Employee employee, IFormFileCollection fileCollection)
        {
            _logger.LogInformation("Starting method: RegisterEmployeeService");

            EmployeeCalculation employeeCalculation = new EmployeeCalculation();
            employeeCalculation.employee = employee;
            _logger.LogInformation("Employee file converted");
            EmployeeEmailMobileCheck employeeEmailMobileCheck = this.GetEmployeeDetail(employeeCalculation);
            employeeCalculation.employeeDeclaration.EmployeeCurrentRegime = ApplicationConstants.DefaultTaxRegin;
            employeeCalculation.Doj = DateTime.UtcNow;
            employeeCalculation.IsFirstYearDeclaration = true;

            if (employeeEmailMobileCheck.EmployeeCount > 0)
                throw HiringBellException.ThrowBadRequest("Employee already exists. Please login first and update detail.");

            await RegisterOrUpdateEmployeeDetail(employeeCalculation, fileCollection);

            _logger.LogInformation("Leaving method: RegisterEmployeeService");
            return ApplicationConstants.Successfull;
        }

        public async Task EmployeeBulkRegistrationService(Employee employee, IFormFileCollection fileCollection)
        {
            EmployeeCalculation employeeCalculation = new EmployeeCalculation();
            employeeCalculation.employee = employee;
            EmployeeEmailMobileCheck employeeEmailMobileCheck = this.GetEmployeeDetail(employeeCalculation);
            employeeCalculation.employeeDeclaration.EmployeeCurrentRegime = ApplicationConstants.DefaultTaxRegin;
            employeeCalculation.Doj = DateTime.UtcNow;
            employeeCalculation.IsFirstYearDeclaration = true;

            if (employeeEmailMobileCheck.EmployeeCount > 0)
                throw HiringBellException.ThrowBadRequest("Employee already exists. Please login first and update detail.");

            await BulkRegistration(employeeCalculation, fileCollection);
        }

        private EmployeeDeclaration GetDeclarationInstance(DataTable declarationTable, Employee employee)
        {
            EmployeeDeclaration employeeDeclaration = null;
            if (declarationTable.Rows.Count == 1)
            {
                employeeDeclaration = Converter.ToType<EmployeeDeclaration>(declarationTable);
                if (employeeDeclaration.SalaryDetail == null)
                {
                    employeeDeclaration.SalaryDetail = new EmployeeSalaryDetail();
                }

                employeeDeclaration.SalaryDetail.CTC = employee.CTC;
            }
            else
            {
                employeeDeclaration = new EmployeeDeclaration
                {
                    SalaryDetail = new EmployeeSalaryDetail
                    {
                        CTC = employee.CTC
                    }
                };
            }

            return employeeDeclaration;
        }

        private EmployeeSalaryDetail GetEmployeeSalaryDetailInstance(DataTable salaryDetailTable, Employee employee)
        {
            EmployeeSalaryDetail employeeSalaryDetail = null;
            if (salaryDetailTable.Rows.Count == 1)
            {
                employeeSalaryDetail = Converter.ToType<EmployeeSalaryDetail>(salaryDetailTable);
            }
            else
            {
                employeeSalaryDetail = new EmployeeSalaryDetail
                {
                    GrossIncome = 0,
                    NetSalary = 0,
                    CompleteSalaryDetail = "[]",
                    TaxDetail = "[]"
                };
            }

            if (employeeSalaryDetail.CTC != employee.CTC)
                employee.IsCTCChanged = true;

            employeeSalaryDetail.CTC = employee.CTC;
            return employeeSalaryDetail;
        }

        private long CheckUpdateDeclarationComponents(EmployeeCalculation employeeCalculation)
        {
            long declarationId = 0;

            if (!string.IsNullOrEmpty(employeeCalculation.employeeDeclaration.DeclarationDetail))
            {
                try
                {
                    List<SalaryComponents> components = JsonConvert.DeserializeObject<List<SalaryComponents>>(employeeCalculation.employeeDeclaration.DeclarationDetail);
                    if (components == null || components.Count == 0)
                        declarationId = employeeCalculation.employeeDeclaration.EmployeeDeclarationId;
                }
                catch
                {
                    declarationId = employeeCalculation.employeeDeclaration.EmployeeDeclarationId;
                    _logger.LogInformation("Salary component not found. Taking from master data.");
                }
            }

            return declarationId;
        }

        private EmployeeEmailMobileCheck GetEmployeeDetail(EmployeeCalculation employeeCalculation)
        {
            _logger.LogInformation("Starting method: GetEmployeeDetail");

            employeeCalculation.CTC = employeeCalculation.employee.CTC;
            employeeCalculation.EmployeeId = employeeCalculation.employee.EmployeeId;
            _logger.LogInformation("Loading data.");
            DataSet resultSet = _db.FetchDataSet("sp_employee_getbyid_to_reg_or_upd", new
            {
                EmployeeId = employeeCalculation.employee.EmployeeUid,
                employeeCalculation.employee.Mobile,
                employeeCalculation.employee.Email,
                employeeCalculation.employee.CompanyId,
                employeeCalculation.employee.CTC,
                employeeCalculation.employee.SalaryGroupId
            });

            if (resultSet == null || resultSet.Tables.Count != 9)
                throw HiringBellException.ThrowBadRequest("Fail to get employee relevent data. Please contact to admin.");

            _logger.LogInformation("[GetEmployeeDetail]: Date fetched total table: " + resultSet.Tables.Count);
            if (resultSet.Tables[4].Rows.Count != 1)
                throw HiringBellException.ThrowBadRequest("Company setting not found. Please contact to admin.");

            Employee employeeDetail = Converter.ToType<Employee>(resultSet.Tables[0]);

            if (employeeDetail.EmployeeUid > 0)
                employeeCalculation.Doj = employeeDetail.CreatedOn;
            else
                employeeCalculation.Doj = DateTime.UtcNow;

            // check if salary group changed
            if (employeeDetail.SalaryGroupId != employeeCalculation.employee.SalaryGroupId)
                employeeCalculation.employee.IsCTCChanged = true;

            // check and get Declaration object
            employeeCalculation.employeeDeclaration = GetDeclarationInstance(resultSet.Tables[1], employeeCalculation.employee);

            // check and get employee salary detail object
            employeeCalculation.employeeSalaryDetail = GetEmployeeSalaryDetailInstance(resultSet.Tables[2], employeeCalculation.employee);

            employeeCalculation.salaryComponents = Converter.ToList<SalaryComponents>(resultSet.Tables[3]);

            // build and bind compnay setting
            employeeCalculation.companySetting = Converter.ToType<CompanySetting>(resultSet.Tables[4]);
            employeeCalculation.PayrollStartDate = new DateTime(employeeCalculation.companySetting.FinancialYear,
                employeeCalculation.companySetting.DeclarationStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);

            employeeCalculation.employeeSalaryDetail.FinancialStartYear = employeeCalculation.companySetting.FinancialYear;

            // got duplication email, mobile or employee id if any
            EmployeeEmailMobileCheck employeeEmailMobileCheck = Converter.ToType<EmployeeEmailMobileCheck>(resultSet.Tables[5]);

            // got duplication email, mobile or employee id if any
            employeeCalculation.salaryGroup = Converter.ToType<SalaryGroup>(resultSet.Tables[6]);

            // getting professional tax detail based on company id
            employeeCalculation.ptaxSlab = Converter.ToList<PTaxSlab>(resultSet.Tables[7]);

            if (employeeCalculation.ptaxSlab.Count == 0)
                throw HiringBellException.ThrowBadRequest("Professional tax not found for the current employee. Please contact to admin.");

            // getting surcharges slab detail based on company id
            employeeCalculation.surchargeSlabs = Converter.ToList<SurChargeSlab>(resultSet.Tables[8]);

            if (employeeCalculation.surchargeSlabs.Count == 0)
                throw HiringBellException.ThrowBadRequest("Surcharges slab not found for the current employee. Please contact to admin.");

            if (employeeDetail != null)
            {
                employeeCalculation.employee.OrganizationId = employeeCalculation.employee.OrganizationId;
                employeeCalculation.employee.EmpProfDetailUid = employeeDetail.EmpProfDetailUid;
            }
            else
            {
                employeeCalculation.employee.OrganizationId = employeeCalculation.employee.OrganizationId;
                employeeCalculation.employee.EmpProfDetailUid = -1;
            }

            if (employeeEmailMobileCheck.EmailCount > 0)
                throw HiringBellException.ThrowBadRequest($"Email id: {employeeCalculation.employee.Email} already exists.");

            if (employeeEmailMobileCheck.MobileCount > 0)
                throw HiringBellException.ThrowBadRequest($"Mobile no: {employeeCalculation.employee.Mobile} already exists.");
            _logger.LogInformation("Leaving method: GetEmployeeDetail");

            _logger.LogInformation("Leaving method: GetEmployeeDetail");
            return employeeEmailMobileCheck;
        }

        private async Task AssignReportingManager(Employee employee)
        {
            if (employee.ReportingManagerId == 0)
            {
                employee.ReportingManagerId = _currentSession.CurrentUserDetail.UserId;
            }

            await Task.CompletedTask;
        }

        private async Task<string> RegisterOrUpdateEmployeeDetail(EmployeeCalculation eCal, IFormFileCollection fileCollection)
        {
            bool IsNewRegistration = false;

            try
            {
                Employee employee = eCal.employee;
                eCal.EmployeeId = eCal.employee.EmployeeUid;

                this.ValidateEmployee(employee);
                this.ValidateEmployeeDetails(employee);
                int empId = Convert.ToInt32(employee.EmployeeUid);

                var professionalDetail = new EmployeeProfessionDetail
                {
                    AadharNo = employee.AadharNo,
                    AccountNumber = employee.AccountNumber,
                    BankName = employee.BankName,
                    BranchName = employee.BranchName,
                    CreatedBy = employee.EmployeeUid,
                    CreatedOn = employee.CreatedOn,
                    Domain = employee.Domain,
                    Email = employee.Email,
                    EmployeeUid = employee.EmployeeUid,
                    EmpProfDetailUid = employee.EmpProfDetailUid,
                    ExperienceInYear = employee.ExperienceInYear,
                    FirstName = employee.FirstName,
                    IFSCCode = employee.IFSCCode,
                    LastCompanyName = employee.LastCompanyName,
                    LastName = employee.LastName,
                    Mobile = employee.Mobile,
                    PANNo = employee.PANNo,
                    SecomdaryMobile = employee.SecondaryMobile,
                    Specification = employee.Specification,
                };

                await AssignReportingManager(employee);

                employee.ProfessionalDetail_Json = JsonConvert.SerializeObject(professionalDetail);


                string EncreptedPassword = _authenticationService.Encrypt(
                    _configuration.GetSection("DefaultNewEmployeePassword").Value,
                    _configuration.GetSection("EncryptSecret").Value
                );

                if (employee.AccessLevelId != (int)RolesName.Admin)
                    employee.UserTypeId = (int)RolesName.User;

                if (string.IsNullOrEmpty(employee.NewSalaryDetail))
                    employee.NewSalaryDetail = "[]";

                employee.EmployeeId = employee.EmployeeUid;
                if (employee.EmployeeUid == 0)
                {
                    // create employee record
                    employee.EmployeeId = await RegisterNewEmployee(employee, eCal.Doj);
                    IsNewRegistration = true;

                    employee.EmployeeUid = employee.EmployeeId;
                    eCal.EmployeeId = employee.EmployeeId;
                    eCal.employee.IsCTCChanged = false;
                    eCal.employeeDeclaration.EmployeeId = employee.EmployeeId;
                }

                _currentSession.TimeZoneNow = _timezoneConverter.ToTimeZoneDateTime(DateTime.UtcNow, _currentSession.TimeZone);
                _logger.LogInformation("Leaving form set current time zone");

                await _declarationService.CalculateSalaryNDeclaration(eCal, true);

                long declarationId = CheckUpdateDeclarationComponents(eCal);
                var employeeId = _db.Execute<Employee>("sp_Employees_InsUpdate", new
                {
                    employee.EmployeeUid,
                    employee.OrganizationId,
                    employee.FirstName,
                    employee.LastName,
                    employee.Mobile,
                    employee.Email,
                    employee.LeavePlanId,
                    employee.PayrollGroupId,
                    employee.SalaryGroupId,
                    employee.CompanyId,
                    employee.NoticePeriodId,
                    employee.SecondaryMobile,
                    employee.FatherName,
                    employee.MotherName,
                    employee.SpouseName,
                    employee.Gender,
                    employee.State,
                    employee.City,
                    employee.Pincode,
                    employee.Address,
                    employee.PANNo,
                    employee.AadharNo,
                    employee.AccountNumber,
                    employee.BankName,
                    employee.BranchName,
                    employee.IFSCCode,
                    employee.Domain,
                    employee.Specification,
                    employee.ExprienceInYear,
                    employee.LastCompanyName,
                    employee.IsPermanent,
                    employee.ActualPackage,
                    employee.FinalPackage,
                    employee.TakeHomeByCandidate,
                    employee.ReportingManagerId,
                    employee.DesignationId,
                    employee.ProfessionalDetail_Json,
                    Password = EncreptedPassword,
                    employee.AccessLevelId,
                    employee.UserTypeId,
                    employee.CTC,
                    eCal.employeeSalaryDetail.GrossIncome,
                    eCal.employeeSalaryDetail.NetSalary,
                    eCal.employeeSalaryDetail.CompleteSalaryDetail,
                    eCal.employeeSalaryDetail.TaxDetail,
                    employee.DOB,
                    RegistrationDate = eCal.Doj,
                    EmployeeDeclarationId = declarationId,
                    DeclarationDetail = GetDeclarationBasicFields(eCal.salaryComponents),
                    employee.WorkShiftId,
                    IsPending = false,
                    employee.NewSalaryDetail,
                    IsNewRegistration,
                    AdminId = _currentSession.CurrentUserDetail.UserId
                },
                    true
                );


                if (string.IsNullOrEmpty(employeeId) || employeeId == "0")
                {
                    throw HiringBellException.ThrowBadRequest("Fail to insert or update record. Contact to admin.");
                }

                eCal.EmployeeId = Convert.ToInt64(employeeId);
                if (fileCollection != null && fileCollection.Count > 0)
                {
                    var files = fileCollection.Select(x => new Files
                    {
                        FileUid = employee.FileId,
                        FileName = fileCollection[0].Name,
                        Email = employee.Email,
                        FileExtension = string.Empty
                    }).ToList<Files>();

                    var ownerPath = Path.Combine(_fileLocationDetail.UserFolder, $"{nameof(UserType.Employee)}_{eCal.EmployeeId}");
                    _fileService.SaveFile(ownerPath, files, fileCollection, employee.OldFileName);

                    var fileInfo = (from n in files
                                    select new
                                    {
                                        FileId = n.FileUid,
                                        FileOwnerId = eCal.EmployeeId,
                                        FileName = n.FileName.Contains(".") ? n.FileName : n.FileName + "." + n.FileExtension,
                                        FilePath = n.FilePath,
                                        FileExtension = n.FileExtension,
                                        UserTypeId = (int)UserType.Employee,
                                        AdminId = _currentSession.CurrentUserDetail.UserId
                                    }).ToList();

                    var batchResult = await _db.BulkExecuteAsync("sp_userfiledetail_Upload", fileInfo, true);
                }

                // var ResultSet = this.GetManageEmployeeDetailService(eCal.EmployeeId);
                return employeeId;
            }
            catch
            {
                if (IsNewRegistration && eCal.employee.EmployeeId > 0)
                    _db.Execute("sp_employee_delete_by_EmpId", new { EmployeeId = eCal.employee.EmployeeId }, false);
                
                throw;
            }
        }

        private async Task<long> RegisterNewEmployee(Employee employee, DateTime doj)
        {
            var result = await _db.ExecuteAsync("sp_employees_create", new
            {
                employee.FirstName,
                employee.LastName,
                employee.Mobile,
                employee.Email,
                employee.LeavePlanId,
                employee.PayrollGroupId,
                employee.SalaryGroupId,
                employee.ReportingManagerId,
                employee.DesignationId,
                RegistrationDate = doj,
                employee.CompanyId,
                employee.NoticePeriodId,
                employee.WorkShiftId,
                employee.UserTypeId,
                AdminId = _currentSession.CurrentUserDetail.UserId
            }, true);

            if (string.IsNullOrEmpty(result.statusMessage))
                throw HiringBellException.ThrowBadRequest("Fail to register new employee.");

            long employeeId = Convert.ToInt64(result.statusMessage);
            if (employeeId == 0)
                throw HiringBellException.ThrowBadRequest("Fail to register new employee.");

            return employeeId;
        }

        private string GetDeclarationBasicFields(List<SalaryComponents> salaryComponents)
        {
            var basicFields = salaryComponents.Select(x => new
            {
                x.ComponentId,
                x.DeclaredValue,
                x.Section,
                x.MaxLimit,
                x.ComponentFullName,
                x.UploadedFileIds
            }).ToList();

            return JsonConvert.SerializeObject(basicFields);
        }

        private async Task BulkRegistration(EmployeeCalculation eCal, IFormFileCollection fileCollection)
        {
            try
            {
                Employee employee = eCal.employee;
                eCal.Doj = employee.DateOfJoining;
                eCal.EmployeeId = eCal.employee.EmployeeUid;

                this.ValidateEmployee(employee);
                this.ValidateEmployeeDetails(employee);
                int empId = Convert.ToInt32(employee.EmployeeUid);

                var professionalDetail = new EmployeeProfessionDetail
                {
                    AadharNo = employee.AadharNo,
                    AccountNumber = employee.AccountNumber,
                    BankName = employee.BankName,
                    BranchName = employee.BranchName,
                    CreatedBy = employee.EmployeeUid,
                    CreatedOn = employee.CreatedOn,
                    Domain = employee.Domain,
                    Email = employee.Email,
                    EmployeeUid = employee.EmployeeUid,
                    EmpProfDetailUid = employee.EmpProfDetailUid,
                    ExperienceInYear = employee.ExperienceInYear,
                    FirstName = employee.FirstName,
                    IFSCCode = employee.IFSCCode,
                    LastCompanyName = employee.LastCompanyName,
                    LastName = employee.LastName,
                    Mobile = employee.Mobile,
                    PANNo = employee.PANNo,
                    SecomdaryMobile = employee.SecondaryMobile,
                    Specification = employee.Specification,
                };

                await AssignReportingManager(employee);

                employee.ProfessionalDetail_Json = JsonConvert.SerializeObject(professionalDetail);


                string EncreptedPassword = _authenticationService.Encrypt(
                    _configuration.GetSection("DefaultNewEmployeePassword").Value,
                    _configuration.GetSection("EncryptSecret").Value
                );

                if (employee.AccessLevelId != (int)RolesName.Admin)
                    employee.UserTypeId = (int)RolesName.User;

                if (string.IsNullOrEmpty(employee.NewSalaryDetail))
                    employee.NewSalaryDetail = "[]";

                employee.EmployeeUid = employee.EmployeeId;
                if (employee.EmployeeUid == 0)
                {
                    // create employee record
                    var result = _db.Execute("sp_employee_lastId", new { IsActive = true }, true);
                    if (string.IsNullOrEmpty(result.statusMessage))
                        throw HiringBellException.ThrowBadRequest("Fail to get last employee entry.");

                    long id = Convert.ToInt64(result.statusMessage);
                    if (id <= 0)
                        throw HiringBellException.ThrowBadRequest("Fail to get last employee entry.");

                    employee.EmployeeId = id + 1;
                    employee.EmployeeUid = employee.EmployeeId;
                    eCal.EmployeeId = employee.EmployeeId;
                    eCal.employeeDeclaration.EmployeeId = employee.EmployeeId;
                }

                _currentSession.TimeZoneNow = _timezoneConverter.ToTimeZoneDateTime(DateTime.UtcNow, _currentSession.TimeZone);
                await _declarationService.CalculateSalaryNDeclaration(eCal, true);

                long declarationId = CheckUpdateDeclarationComponents(eCal);
                var employeeId = _db.Execute<Employee>("sp_Employees_InsUpdate", new
                {
                    employee.EmployeeUid,
                    employee.OrganizationId,
                    employee.FirstName,
                    employee.LastName,
                    employee.Mobile,
                    employee.Email,
                    employee.LeavePlanId,
                    employee.PayrollGroupId,
                    employee.SalaryGroupId,
                    employee.CompanyId,
                    employee.NoticePeriodId,
                    employee.SecondaryMobile,
                    employee.FatherName,
                    employee.MotherName,
                    employee.SpouseName,
                    employee.Gender,
                    employee.State,
                    employee.City,
                    employee.Pincode,
                    employee.Address,
                    employee.PANNo,
                    employee.AadharNo,
                    employee.AccountNumber,
                    employee.BankName,
                    employee.BranchName,
                    employee.IFSCCode,
                    employee.Domain,
                    employee.Specification,
                    employee.ExprienceInYear,
                    employee.LastCompanyName,
                    employee.IsPermanent,
                    employee.ActualPackage,
                    employee.FinalPackage,
                    employee.TakeHomeByCandidate,
                    employee.ReportingManagerId,
                    employee.DesignationId,
                    employee.ProfessionalDetail_Json,
                    Password = EncreptedPassword,
                    employee.AccessLevelId,
                    employee.UserTypeId,
                    employee.CTC,
                    eCal.employeeSalaryDetail.GrossIncome,
                    eCal.employeeSalaryDetail.NetSalary,
                    eCal.employeeSalaryDetail.CompleteSalaryDetail,
                    eCal.employeeSalaryDetail.TaxDetail,
                    employee.DOB,
                    RegistrationDate = employee.DateOfJoining,
                    EmployeeDeclarationId = declarationId,
                    DeclarationDetail = JsonConvert.SerializeObject(eCal.salaryComponents),
                    employee.WorkShiftId,
                    IsPending = false,
                    employee.NewSalaryDetail,
                    AdminId = _currentSession.CurrentUserDetail.UserId
                },
                    true
                );


                if (string.IsNullOrEmpty(employeeId) || employeeId == "0")
                {
                    throw HiringBellException.ThrowBadRequest("Fail to insert or update record. Contact to admin.");
                }

                eCal.EmployeeId = Convert.ToInt64(employeeId);
                if (fileCollection.Count > 0)
                {
                    var files = fileCollection.Select(x => new Files
                    {
                        FileUid = employee.FileId,
                        FileName = fileCollection[0].Name,
                        Email = employee.Email,
                        FileExtension = string.Empty
                    }).ToList<Files>();

                    var ownerPath = Path.Combine(_fileLocationDetail.UserFolder, $"{nameof(UserType.Employee)}_{eCal.EmployeeId}");
                    _fileService.SaveFile(ownerPath, files, fileCollection, employee.OldFileName);

                    var fileInfo = (from n in files
                                    select new
                                    {
                                        FileId = n.FileUid,
                                        FileOwnerId = eCal.EmployeeId,
                                        FileName = n.FileName,
                                        FilePath = n.FilePath,
                                        FileExtension = n.FileExtension,
                                        UserTypeId = (int)UserType.Employee,
                                        AdminId = _currentSession.CurrentUserDetail.UserId
                                    }).ToList();

                    var batchResult = await _db.BulkExecuteAsync("sp_userfiledetail_Upload", fileInfo, true);
                }
            }
            catch
            {
                throw;
            }
        }

        private void ValidateEmployeeDetails(Employee employee)
        {

            if (employee.ActualPackage < 0)
                throw new HiringBellException { UserMessage = "Invalid Actual Package.", FieldName = nameof(employee.ActualPackage), FieldValue = employee.ActualPackage.ToString() };

            if (employee.FinalPackage < 0)
                throw new HiringBellException { UserMessage = "Invalid Final Package.", FieldName = nameof(employee.FinalPackage), FieldValue = employee.FinalPackage.ToString() };

            if (employee.TakeHomeByCandidate < 0)
                throw new HiringBellException { UserMessage = "Invalid TakeHome By Candidate.", FieldName = nameof(employee.TakeHomeByCandidate), FieldValue = employee.TakeHomeByCandidate.ToString() };

            if (employee.FinalPackage < employee.ActualPackage)
                throw new HiringBellException { UserMessage = "Final package must be greater that or equal to Actual package.", FieldName = nameof(employee.FinalPackage), FieldValue = employee.FinalPackage.ToString() };

            if (employee.ActualPackage < employee.TakeHomeByCandidate)
                throw new HiringBellException { UserMessage = "Actual package must be greater that or equal to TakeHome package.", FieldName = nameof(employee.ActualPackage), FieldValue = employee.ActualPackage.ToString() };
        }

        private void ValidateEmployeeMapDetails(EmployeeMappedClient employee)
        {

            if (employee.ActualPackage < 0)
                throw new HiringBellException { UserMessage = "Invalid Actual Package.", FieldName = nameof(employee.ActualPackage), FieldValue = employee.ActualPackage.ToString() };

            if (employee.FinalPackage < 0)
                throw new HiringBellException { UserMessage = "Invalid Final Package.", FieldName = nameof(employee.FinalPackage), FieldValue = employee.FinalPackage.ToString() };

            if (employee.TakeHomeByCandidate < 0)
                throw new HiringBellException { UserMessage = "Invalid TakeHome By Candidate.", FieldName = nameof(employee.TakeHomeByCandidate), FieldValue = employee.TakeHomeByCandidate.ToString() };

            if (employee.FinalPackage < employee.ActualPackage)
                throw new HiringBellException { UserMessage = "Final package must be greater that or equal to Actual package.", FieldName = nameof(employee.FinalPackage), FieldValue = employee.FinalPackage.ToString() };

            if (employee.ActualPackage < employee.TakeHomeByCandidate)
                throw new HiringBellException { UserMessage = "Actual package must be greater that or equal to TakeHome package.", FieldName = nameof(employee.ActualPackage), FieldValue = employee.ActualPackage.ToString() };
        }

        private void ValidateEmployee(Employee employee)
        {
            if (string.IsNullOrEmpty(employee.Email))
                throw new HiringBellException { UserMessage = "Email id is a mandatory field.", FieldName = nameof(employee.Email), FieldValue = employee.Email.ToString() };

            if (string.IsNullOrEmpty(employee.AccountNumber))
                throw new HiringBellException { UserMessage = "Account number is a mandatory field.", FieldName = nameof(employee.AccountNumber), FieldValue = employee.AccountNumber.ToString() };

            if (string.IsNullOrEmpty(employee.BankName))
                throw new HiringBellException { UserMessage = "Bank name is a mandatory field.", FieldName = nameof(employee.BankName), FieldValue = employee.BankName.ToString() };

            if (string.IsNullOrEmpty(employee.IFSCCode))
                throw new HiringBellException { UserMessage = "IFSC code is a mandatory field.", FieldName = nameof(employee.IFSCCode), FieldValue = employee.IFSCCode.ToString() };

            if (string.IsNullOrEmpty(employee.PANNo))
                throw new HiringBellException { UserMessage = "Pan No is a mandatory field.", FieldName = nameof(employee.PANNo), FieldValue = employee.PANNo.ToString() };

            if (string.IsNullOrEmpty(employee.FirstName))
                throw new HiringBellException { UserMessage = "First Name is a mandatory field.", FieldName = nameof(employee.FirstName), FieldValue = employee.FirstName.ToString() };

            if (string.IsNullOrEmpty(employee.LastName))
                throw new HiringBellException { UserMessage = "Last Name is a mandatory field.", FieldName = nameof(employee.LastName), FieldValue = employee.LastName.ToString() };

            if (string.IsNullOrEmpty(employee.Mobile) || employee.Mobile.Contains("."))
                throw new HiringBellException { UserMessage = "Mobile number is a mandatory field.", FieldName = nameof(employee.Mobile), FieldValue = employee.Mobile.ToString() };

            if (employee.Mobile.Length < 10 || employee.Mobile.Length > 10)
                throw new HiringBellException { UserMessage = "Mobile number must be only 10 digit.", FieldName = nameof(employee.Mobile), FieldValue = employee.Mobile.ToString() };

            if (employee.DesignationId <= 0)
                throw new HiringBellException { UserMessage = "Designation is a mandatory field.", FieldName = nameof(employee.DesignationId), FieldValue = employee.DesignationId.ToString() };

            if (employee.ReportingManagerId < 0)
                employee.ReportingManagerId = 0;

            if (employee.UserTypeId <= 0)
                throw new HiringBellException { UserMessage = "User Type is a mandatory field.", FieldName = nameof(employee.UserTypeId), FieldValue = employee.UserTypeId.ToString() };

            if (employee.AccessLevelId <= 0)
                throw new HiringBellException { UserMessage = "Role is a mandatory field.", FieldName = nameof(employee.AccessLevelId), FieldValue = employee.AccessLevelId.ToString() };

            if (employee.CTC <= 0)
                throw new HiringBellException { UserMessage = "CTC is a mandatory field.", FieldName = nameof(employee.CTC), FieldValue = employee.CTC.ToString() };

            if (employee.OrganizationId <= 0)
                throw new HiringBellException("Invalid organization selected. Please contact to admin");

            if (employee.CompanyId <= 0)
                throw new HiringBellException("Invalid company selected. Please contact to admin");

            if (employee.DOB == null)
                throw new HiringBellException { UserMessage = "Date of birth is a mandatory field.", FieldName = nameof(employee.DOB), FieldValue = employee.DOB.ToString() };

            var mail = new MailAddress(employee.Email);
            bool isValidEmail = mail.Host.Contains(".");
            if (!isValidEmail)
                throw new HiringBellException { UserMessage = "The email is invalid.", FieldName = nameof(employee.Email), FieldValue = employee.Email.ToString() };

            if (!employee.IsPayrollOnCTC)
            {
                if (employee.SalaryGroupId <= 0)
                    throw HiringBellException.ThrowBadRequest("Please select salary group");
            }
            else
            {
                employee.SalaryGroupId = 0;
            }
        }

        public async Task<string> GenerateOfferLetterService(EmployeeOfferLetter employeeOfferLetter)
        {
            ValidateEmpOfferLetter(employeeOfferLetter);

            var company = _db.Get<OrganizationDetail>("sp_company_getById", new { CompanyId = employeeOfferLetter.CompanyId });
            string employeeName = employeeOfferLetter.FirstName + "_" + employeeOfferLetter.LastName;
            var html = GetHtmlString(company, employeeOfferLetter);
            var folderPath = GeneratedPdfOfferLetter(html, employeeName);
            var file = new FileDetail
            {
                FileName = employeeName,
                FilePath = folderPath
            };
            EmailSenderModal emailSenderModal = new EmailSenderModal
            {
                To = new List<string> { employeeOfferLetter.Email }, //receiver.Email,
                CC = new List<string>(),
                BCC = new List<string>(),
                FileDetails = new List<FileDetail> { file },
                Subject = "Offer Letter",
                Body = "Email Body",
                Title = "Title"
            };

            await _eMailManager.SendMailAsync(emailSenderModal);
            return "Generated successfuly";
        }

        private string GeneratedPdfOfferLetter(string html, string employeeName)
        {
            var folderPath = Path.Combine(_fileLocationDetail.DocumentFolder, "Employee_Offer_Letter");
            if (!Directory.Exists(Path.Combine(_hostingEnvironment.ContentRootPath, folderPath)))
                Directory.CreateDirectory(Path.Combine(_hostingEnvironment.ContentRootPath, folderPath));

            var destinationFilePath = Path.Combine(_hostingEnvironment.ContentRootPath, folderPath,
               employeeName + $".{ApplicationConstants.Pdf}");
            _htmlToPdfConverter.ConvertToPdf(html, destinationFilePath);
            return folderPath;
        }

        private string GetHtmlString(OrganizationDetail organization, EmployeeOfferLetter employee)
        {
            string html = string.Empty;
            var LetterType = 1;
            var result = _db.Get<AnnexureOfferLetter>("sp_annexure_offer_letter_getby_lettertype", new { CompanyId = 1, LetterType });
            if (File.Exists(result.FilePath))
                html = File.ReadAllText(result.FilePath);
            else
                throw HiringBellException.ThrowBadRequest("Offer letter is not found. Please contact to admin");

            html = html.Replace("[[Company-Name]]", organization.CompanyName).
            Replace("[[Generate-Date]]", DateTime.Now.ToString("dd MMM, yyyy")).
            Replace("[[Company-Address]]", organization.City).
            Replace("[[Employee-Name]]", employee.FirstName + " " + employee.LastName).
            Replace("[[CTC]]", employee.CTC.ToString()).
            Replace("[[Designation]]", employee.Designation).
            Replace("[[Joining-Date]]", employee.JoiningDate.ToString("dd MMM, yyyy"));

            return html;
        }

        private void ValidateEmpOfferLetter(EmployeeOfferLetter employeeOfferLetter)
        {
            if (employeeOfferLetter.CompanyId <= 0)
                throw new HiringBellException("Invalid company selected");

            if (employeeOfferLetter.CTC <= 0)
                throw new HiringBellException("CTC is invalid. Please enter a valid CTC");

            if (string.IsNullOrEmpty(employeeOfferLetter.FirstName))
                throw new HiringBellException("First name is null or empty. Please enter a valid first name");

            if (string.IsNullOrEmpty(employeeOfferLetter.LastName))
                throw new HiringBellException("Last name is null or empty. Please enter a valid last name");

            if (string.IsNullOrEmpty(employeeOfferLetter.Email))
                throw new HiringBellException("Email is null or empty. Please enter a valid email");

            if (string.IsNullOrEmpty(employeeOfferLetter.Designation))
                throw new HiringBellException("Designation is null or empty. Please enter a valid designation");

            if (employeeOfferLetter.JoiningDate == null)
                throw new HiringBellException("Date of joining is null. Please enter a valid date of joining");

            var mail = new MailAddress(employeeOfferLetter.Email);
            bool isValidEmail = mail.Host.Contains(".");
            if (!isValidEmail)
                throw new HiringBellException("Email is invalid. Please enter a valid email");
        }

        public async Task<string> ExportEmployeeService(int companyId, int fileType)
        {
            if (companyId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid company. Please login again");

            if (fileType == 0)
                throw HiringBellException.ThrowBadRequest("Invalid type selected. Please select a valid type");

            var folderPath = Path.Combine(_fileLocationDetail.DocumentFolder, "Employees_Excel");
            if (!Directory.Exists(Path.Combine(_hostingEnvironment.ContentRootPath, folderPath)))
                Directory.CreateDirectory(Path.Combine(_hostingEnvironment.ContentRootPath, folderPath));

            var filepath = Path.Combine(folderPath, "Employee_Excel" + $".{ApplicationConstants.Excel}");
            var destinationFilePath = Path.Combine(_hostingEnvironment.ContentRootPath, filepath);

            if (File.Exists(destinationFilePath))
                File.Delete(destinationFilePath);
            FilterModel filterModel = new FilterModel();
            filterModel.PageSize = 1000;
            //filterModel.SearchString += $" and CompanyId = {companyId} ";
            var employees = FilterActiveEmployees(filterModel);
            if (employees.Count > 0)
            {
                if (fileType == 2)
                {
                    var datatable = Converter.ToDataTable(employees);
                    _excelWriter.ToExcel(datatable, destinationFilePath);
                }
            }
            return await Task.FromResult(filepath);
        }

        public async Task<string> UploadEmployeeExcelService(List<Employee> employees, IFormFileCollection formFiles)
        {
            string status = string.Empty;
            if (employees.Count > 0)
            {
                foreach (var x in employees)
                {
                    if (!string.IsNullOrEmpty(x.FirstName))
                    {
                        if (x.CTC < 1000)
                            x.CTC = 1000;

                        x.DesignationId = 13;
                        if (string.IsNullOrEmpty(x.LastName))
                            x.LastName = "NA";

                        x.FirstName = x.FirstName.ToUpper();
                        x.LastName = x.LastName.ToUpper();

                        try
                        {
                            await EmployeeBulkRegistrationService(x, formFiles);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.Message);
                        }
                    }
                }

                status = "success";
                return status;
            }

            return await Task.FromResult(status);
        }

        public async Task<string> UpdateEmployeeByExcelService(Employee employee, UploadedPayrollData uploaded, IFormFileCollection fileCollection)
        {
            if (employee.EmployeeUid <= 0)
                throw new HiringBellException { UserMessage = "Invalid EmployeeId.", FieldName = nameof(employee.EmployeeUid), FieldValue = employee.EmployeeUid.ToString() };

            EmployeeCalculation employeeCalculation = new EmployeeCalculation();
            employeeCalculation.employee = employee;
            this.GetEmployeeDetail(employeeCalculation);

            employeeCalculation.Doj = employee.DateOfJoining;
            employeeCalculation.IsFirstYearDeclaration = false;
            employeeCalculation.employee.IsCTCChanged = true;

            var result = await RegisterOrUpdateEmployeeDetail(employeeCalculation, fileCollection);

            if (!string.IsNullOrEmpty(result))
            {
                List<EmployeeDeclaration> employeeDeclarations = new List<EmployeeDeclaration>();
                string componentId = string.Empty;
                foreach (var item in uploaded.Investments)
                {
                    var values = item.Key.Split(" (");
                    if (values.Length > 0)
                    {
                        componentId = values[0].Trim();
                        EmployeeDeclaration employeeDeclaration = new EmployeeDeclaration
                        {
                            ComponentId = componentId,
                            DeclaredValue = item.Value,
                            Email = employee.Email,
                            EmployeeId = employee.EmployeeUid
                        };
                    }
                }
                try
                {
                    await _declarationService.UpdateBulkDeclarationDetail(employee.EmployeeDeclarationId, employeeDeclarations);
                }
                catch
                {
                    _logger.LogInformation($"Investment not found. Component id: {componentId}. Investment id: {employee.EmployeeDeclarationId}");
                }
            }
            return null;
        }

        private void SetupPreviousEmployerIncome(EmployeeCalculation employeeCalculation, UploadedPayrollData uploaded)
        {
            var pemp = new PreviousEmployerDetail
            {
                TotalIncome = uploaded.PR_EPER_TotalIncome,
                PF_with_80C = uploaded.PR_EPER_PF_80C,
                ProfessionalTax = uploaded.PR_EPER_PT,
                TDS = uploaded.PR_EPER_TDS
            };

            employeeCalculation.previousEmployerDetail = pemp;
        }

        public async Task RegisterEmployeeByExcelService(Employee employee, UploadedPayrollData uploaded)
        {
            _logger.LogInformation("Starting method: RegisterEmployeeService");

            int currentRegimeId = string.IsNullOrEmpty(uploaded.Regime) && uploaded.Regime.ToLower().Contains("new")
                ? ApplicationConstants.NewRegim : ApplicationConstants.OldRegim;

            EmployeeCalculation employeeCalculation = new EmployeeCalculation();
            employeeCalculation.employee = employee;
            _logger.LogInformation("Employee file converted");

            this.GetEmployeeDetail(employeeCalculation);

            employeeCalculation.employeeDeclaration.EmployeeCurrentRegime = ApplicationConstants.DefaultTaxRegin;
            employeeCalculation.Doj = employee.DateOfJoining;
            employeeCalculation.IsFirstYearDeclaration = true;
            employeeCalculation.employee.IsCTCChanged = false;
            employeeCalculation.employeeDeclaration.TaxRegimeDescId = currentRegimeId;

            SetupPreviousEmployerIncome(employeeCalculation, uploaded);
            var result = await RegisterOrUpdateEmployeeDetail(employeeCalculation, null);

            if (!string.IsNullOrEmpty(result))
            {
                string componentId = string.Empty;
                long employeeId = Convert.ToInt64(result);

                List<EmployeeDeclaration> employeeDeclarations = new List<EmployeeDeclaration>();
                Employee emp = _db.Get<Employee>("sp_employee_and_declaration_get_byid", new { EmployeeId = employeeId });

                foreach (var item in uploaded.Investments)
                {
                    var values = item.Key.Split(" (");
                    if (values.Length > 0)
                    {
                        componentId = values[0].Trim();
                        EmployeeDeclaration employeeDeclaration = new EmployeeDeclaration
                        {
                            ComponentId = componentId,
                            DeclaredValue = item.Value,
                            Email = emp.Email,
                            EmployeeId = emp.EmployeeUid,
                            EmployeeDeclarationId = emp.EmployeeDeclarationId
                        };
                        employeeDeclarations.Add(employeeDeclaration);
                    }
                }


                try
                {
                    await _declarationService.UpdateBulkDeclarationDetail(emp.EmployeeDeclarationId, employeeDeclarations);
                }
                catch
                {
                    _logger.LogInformation($"Investment not found. Component id: {componentId}. Investment id: {emp.EmployeeDeclarationId}");
                }
            }

            _logger.LogInformation("Leaving method: RegisterEmployeeService");
        }
    }
}