﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ModalLayer.Modal;
using Newtonsoft.Json;
using OnlineDataBuilder.ContextHandler;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace OnlineDataBuilder.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : BaseController
    {
        private readonly IEmployeeService _employeeService;
        private readonly HttpContext _httpContext;
        private ILogger<EmployeeController> _logger;

        public EmployeeController(ILogger<EmployeeController> logger, IEmployeeService employeeService, IHttpContextAccessor httpContext)
        {
            _logger = logger;
            _employeeService = employeeService;
            _httpContext = httpContext.HttpContext;
        }

        [HttpPost]
        [Route("GetEmployees")]
        public ApiResponse GetEmployees([FromBody] FilterModel filterModel)
        {
            var Result = _employeeService.GetEmployees(filterModel);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpPost]
        [Route("EmployeesListData")]
        public ApiResponse EmployeesListData([FromRoute] FilterModel filterModel)
        {
            var Result = _employeeService.EmployeesListDataService(filterModel);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("GetManageEmployeeDetail/{EmployeeId}")]
        public ApiResponse GetManageEmployeeDetail(long EmployeeId)
        {
            var Result = _employeeService.GetEmployeeLeaveDetailService(EmployeeId);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("LoadMappedClients/{EmployeeId}")]
        public ApiResponse LoadMappedClients(long EmployeeId)
        {
            var Result = _employeeService.LoadMappedClientService(EmployeeId);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("GetAllManageEmployeeDetail/{EmployeeId}")]
        public ApiResponse GetAllManageEmployeeDetail(long EmployeeId)
        {
            var Result = _employeeService.GetManageEmployeeDetailService(EmployeeId);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("GetManageClient/{EmployeeId}")]
        public ApiResponse GetManageClient(long EmployeeId)
        {
            var Result = _employeeService.GetManageClientService(EmployeeId);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpPost]
        [Authorize(Roles = Role.Admin)]
        [Route("UpdateEmployeeMappedClientDetail/{IsUpdating}")]
        public ApiResponse UpdateEmployeeMappedClientDetail([FromBody] EmployeeMappedClient employeeMappedClient, bool IsUpdating)
        {
            var Result = _employeeService.UpdateEmployeeMappedClientDetailService(employeeMappedClient, IsUpdating);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [HttpGet("GetEmployeeById/{EmployeeId}/{IsActive}")]
        public ApiResponse GetEmployeeById(int EmployeeId, int IsActive)
        {
            var Result = _employeeService.GetEmployeeByIdService(EmployeeId, IsActive);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [Authorize(Roles = Role.Admin)]
        [HttpDelete("ActivateOrDeActiveEmployee/{EmployeeId}/{IsActive}")]
        public ApiResponse DeleteEmployeeById(int EmployeeId, bool IsActive)
        {
            var Result = _employeeService.ActivateOrDeActiveEmployeeService(EmployeeId, IsActive);
            return BuildResponse(Result, HttpStatusCode.OK);
        }

        [Authorize(Roles = Role.Admin)]
        [HttpPost("employeeregistration")]
        public async Task<ApiResponse> EmployeeRegistration()
        {
            try
            {
                StringValues UserInfoData = default(string);
                _httpContext.Request.Form.TryGetValue("employeeDetail", out UserInfoData);
                if (UserInfoData.Count > 0)
                {
                    _logger.LogInformation("Starting method: employeeregistration controller");
                    Employee employee = JsonConvert.DeserializeObject<Employee>(UserInfoData);
                    _logger.LogInformation("Employee converted");
                    IFormFileCollection files = _httpContext.Request.Form.Files;
                    _logger.LogInformation("Employee file converted");
                    var resetSet = await _employeeService.RegisterEmployeeService(employee, files);
                    return BuildResponse(resetSet);
                }
                else
                {
                    return BuildResponse(this.responseMessage, HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [Authorize(Roles = Role.Admin)]
        [HttpPost("updateemployeedetail")]
        public async Task<ApiResponse> UpdateEmployeeDetail()
        {
            try
            {
                StringValues UserInfoData = default(string);
                _httpContext.Request.Form.TryGetValue("employeeDetail", out UserInfoData);
                if (UserInfoData.Count > 0)
                {
                    Employee employee = JsonConvert.DeserializeObject<Employee>(UserInfoData);
                    IFormFileCollection files = _httpContext.Request.Form.Files;
                    var resetSet = await _employeeService.UpdateEmployeeService(employee, files);
                    return BuildResponse(resetSet);
                }
                else
                {
                    return BuildResponse(this.responseMessage, HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [Authorize(Roles = Role.Admin)]
        [HttpPost("GenerateOfferLetter")]
        public async Task<ApiResponse> GenerateOfferLetter(EmployeeOfferLetter employeeOfferLetter)
        {
            var result = await _employeeService.GenerateOfferLetterService(employeeOfferLetter);
            return BuildResponse(result);
        }

        [Authorize(Roles = Role.Admin)]
        [HttpGet("ExportEmployee/{CompanyId}/{FileType}")]
        public async Task<ApiResponse> ExportEmployee([FromRoute] int CompanyId, [FromRoute] int FileType)
        {
            var result = await _employeeService.ExportEmployeeService(CompanyId, FileType);
            return BuildResponse(result);
        }

        [Authorize(Roles = Role.Admin)]
        [HttpPost("UploadEmployeeExcel")]
        public async Task<ApiResponse> UploadEmployeeExcel([FromBody] List<Employee> employees)
        {
            var files = new FormCollection(new Dictionary<string, StringValues>());
            var result = await _employeeService.UploadEmployeeExcelService(employees, files.Files);
            return BuildResponse(result);
        }
    }
}
