﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModalLayer.Modal;
using OnlineDataBuilder.ContextHandler;
using ServiceLayer.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OnlineDataBuilder.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    [ApiController]
    public class TimesheetRequestController : BaseController
    {
        private readonly ITimesheetRequestService _requestService;
        public TimesheetRequestController(ITimesheetRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpPut("ApproveTimesheet/{TimesheetId}")]
        public async Task<ApiResponse> ApproveTimesheet(int timesheetId)
        {
            var result = await _requestService.ApprovalTimesheetService(timesheetId, null);
            return BuildResponse(result);
        }

        [HttpPut("RejectAction/{TimesheetId}")]
        public async Task<ApiResponse> RejectAction(int timesheetId)
        {
            var result = await _requestService.RejectTimesheetService(timesheetId, null);
            return BuildResponse(result);
        }

        [HttpPut("ReAssigneTimesheet")]
        public IResponse<ApiResponse> ReAssigneToOtherManager(List<DailyTimesheetDetail> dailyTimesheetDetails)
        {
            var result = _requestService.ReAssigneTimesheetService(dailyTimesheetDetails);
            return BuildResponse(result);
        }

        [HttpPut("ApproveTimesheetRequest/{TimesheetId}/{filterId}")]
        public async Task<ApiResponse> ApproveTimesheetRequest([FromRoute] int timesheetId, [FromRoute] int filterId, [FromBody] TimesheetDetail timesheetDetail)
        {
            var result = await _requestService.ApprovalTimesheetService(timesheetId, timesheetDetail, filterId);
            return BuildResponse(result);
        }

        [HttpPut("RejectTimesheetRequest/{TimesheetId}/{filterId}")]
        public async Task<ApiResponse> RejectTimesheetRequest([FromRoute] int timesheetId, [FromRoute] int filterId, [FromBody] TimesheetDetail timesheetDetail)
        {
            var result = await _requestService.RejectTimesheetService(timesheetId, timesheetDetail, filterId);
            return BuildResponse(result);
        }

        [Authorize(Roles = Role.Admin)]
        [HttpPut("ReAssigneTimesheetRequest/{filterId}")]
        public IResponse<ApiResponse> ReAssigneTimesheetRequest([FromRoute] int filterId, [FromBody] List<DailyTimesheetDetail> dailyTimesheetDetails)
        {
            var result = _requestService.ReAssigneTimesheetService(dailyTimesheetDetails, filterId);
            return BuildResponse(result);
        }

        [HttpPost("GetTimesheetRequestData")]
        public async Task<ApiResponse> GetTimesheetRequestData([FromBody] TimesheetDetail timesheetDetail)
        {
            var result = await _requestService.GetTimesheetRequestDataService(timesheetDetail);
            return BuildResponse(result);
        }
    }
}
