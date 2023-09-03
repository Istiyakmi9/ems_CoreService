﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using BottomhalfCore.Services.Interface;
using ModalLayer.Modal;
using Newtonsoft.Json;
using ServiceLayer.Code.SendEmail;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceLayer.Code
{
    public class TimesheetService : ITimesheetService
    {
        private readonly IDb _db;
        private readonly ITimezoneConverter _timezoneConverter;
        private readonly CurrentSession _currentSession;
        private readonly TimesheetEmailService _timesheetEmailService;

        public TimesheetService(
            IDb db,
            ITimezoneConverter timezoneConverter,
            CurrentSession currentSession,
            TimesheetEmailService timesheetEmailService)
        {
            _db = db;
            _timezoneConverter = timezoneConverter;
            _currentSession = currentSession;
            _timesheetEmailService = timesheetEmailService;
        }

        #region NEW CODE

        public async Task RunWeeklyTimesheetCreation(DateTime TimesheetStartDate)
        {
            try
            {
                var timesheets = _db.GetList<TimesheetDetail>("sp_employee_timesheet_get_bydate", new
                {
                    TimesheetEndDate = TimesheetStartDate
                });

                if (timesheets.Count == 0)
                {
                    var counts = await _db.ExecuteAsync("sp_timesheet_runweekly_data", new
                    {
                        TimesheetStartDate = TimesheetStartDate
                    }, true);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            await Task.CompletedTask;
        }

        public List<TimesheetDetail> GetTimesheetByFilterService(TimesheetDetail timesheetDetail)
        {
            if (timesheetDetail.EmployeeId <= 0 || timesheetDetail.ClientId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid data used to get the records.");

            FilterModel filter = new FilterModel();
            filter.SearchString = $"1=1 and EmployeeId = {timesheetDetail.EmployeeId} and ClientId = {timesheetDetail.ClientId} ";

            if (timesheetDetail.TimesheetStatus == (int)ItemStatus.Pending)
                filter.SearchString += $"and TimesheetStatus = {timesheetDetail.TimesheetStatus} and IsSaved = false and IsSubmitted = false";

            else if (timesheetDetail.TimesheetStatus == (int)ItemStatus.Submitted)
                filter.SearchString += $"and IsSubmitted = true";

            else if (timesheetDetail.TimesheetStatus == (int)ItemStatus.Saved)
                filter.SearchString += $"and IsSaved = true";

            else if (timesheetDetail.TimesheetStatus == (int)ItemStatus.Rejected)
                filter.SearchString += $"and TimesheetStatus = {timesheetDetail.TimesheetStatus}";

            else if (timesheetDetail.TimesheetStatus == (int)ItemStatus.Approved)
                filter.SearchString += $"and TimesheetStatus = {timesheetDetail.TimesheetStatus}";

            var Result = _db.GetList<TimesheetDetail>("sp_employee_timesheet_filter", new
            {
                filter.SearchString,
                filter.PageIndex,
                filter.PageSize,
                filter.SortBy
            });

            if (Result == null)
                throw HiringBellException.ThrowBadRequest("Unable to get client detail. Please contact to admin.");

            return Result;
        }

        private async Task CreateTimesheetWeekDays(TimesheetDetail timesheetDetail, ShiftDetail shiftDetail)
        {
            List<WeeklyTimesheetDetail> weeklyTimesheetDetails = new List<WeeklyTimesheetDetail>();
            DateTime startDate = _timezoneConverter.ToTimeZoneDateTime(timesheetDetail.TimesheetStartDate, _currentSession.TimeZone);
            DateTime endDate = _timezoneConverter.ToTimeZoneDateTime(timesheetDetail.TimesheetEndDate, _currentSession.TimeZone);

            while (startDate.Date.Subtract(endDate.Date).TotalDays <= 0)
            {
                var item = timesheetDetail.TimesheetWeeklyData.Find(x => x.WeekDay == startDate.DayOfWeek);
                if (item == null)
                {
                    var isweekened = false;
                    switch (startDate.DayOfWeek)
                    {
                        case DayOfWeek.Sunday:
                            isweekened = !shiftDetail.IsSun;
                            break;
                        case DayOfWeek.Monday:
                            isweekened = !shiftDetail.IsMon;
                            break;
                        case DayOfWeek.Tuesday:
                            isweekened = !shiftDetail.IsTue;
                            break;
                        case DayOfWeek.Wednesday:
                            isweekened = !shiftDetail.IsWed;
                            break;
                        case DayOfWeek.Thursday:
                            isweekened = !shiftDetail.IsThu;
                            break;
                        case DayOfWeek.Friday:
                            isweekened = !shiftDetail.IsFri;
                            break;
                        case DayOfWeek.Saturday:
                            isweekened = !shiftDetail.IsSat;
                            break;
                    }

                    weeklyTimesheetDetails.Add(new WeeklyTimesheetDetail
                    {
                        WeekDay = startDate.DayOfWeek,
                        PresentDate = startDate,
                        ActualBurnedMinutes = isweekened ? 0 : shiftDetail.Duration,
                        IsHoliday = false,
                        IsWeekEnd = isweekened,
                        ExpectedBurnedMinutes = isweekened ? 0 : shiftDetail.Duration,
                        IsOpen = true
                    });
                }
                else
                {
                    weeklyTimesheetDetails.Add(item);
                }

                startDate = startDate.AddDays(1);
            }

            timesheetDetail.TimesheetWeeklyData = weeklyTimesheetDetails;
            await Task.CompletedTask;
        }

        public async Task<TimesheetDetail> GetWeekTimesheetDataService(TimesheetDetail timesheetDetail)
        {
            if (timesheetDetail.TimesheetId <= 0)
                throw new HiringBellException("Invalid Timesheet id passed.");

            (TimesheetDetail timesheet, ShiftDetail shiftDetail) = _db.Get<TimesheetDetail, ShiftDetail>("sp_employee_timesheet_getby_id", new
            {
                TimesheetId = timesheetDetail.TimesheetId
            });

            if (shiftDetail == null)
                throw HiringBellException.ThrowBadRequest("Shift detail not found");

            if (timesheet == null)
            {
                timesheet = timesheetDetail;
                timesheet.TimesheetWeeklyData = new List<WeeklyTimesheetDetail>();
                timesheet.TimesheetStartDate = _timezoneConverter.ToTimeZoneDateTime(timesheetDetail.TimesheetStartDate, _currentSession.TimeZone);
                timesheet.TimesheetEndDate = timesheet.TimesheetStartDate.AddDays(6);
            }
            else
            {
                if (!string.IsNullOrEmpty(timesheet.TimesheetWeeklyJson))
                    timesheet.TimesheetWeeklyData = JsonConvert.DeserializeObject<List<WeeklyTimesheetDetail>>(timesheet.TimesheetWeeklyJson);
                else
                    timesheet.TimesheetWeeklyData = new List<WeeklyTimesheetDetail>();

            }

            await CreateTimesheetWeekDays(timesheet, shiftDetail);
            return timesheet;
        }

        private string UpdateOrInsertTimesheetDetail(TimesheetDetail timeSheetDetail, ShiftDetail shiftDetail)
        {
            int ExpectedBurnedMinutes = 0;
            int ActualBurnedMinutes = 0;
            timeSheetDetail.TimesheetWeeklyJson = JsonConvert.SerializeObject(timeSheetDetail.TimesheetWeeklyData);
            timeSheetDetail.TimesheetWeeklyData.ForEach(i =>
            {
                ExpectedBurnedMinutes += shiftDetail.Duration;
                ActualBurnedMinutes += i.ActualBurnedMinutes;
            });

            var result = _db.Execute<TimesheetDetail>(ApplicationConstants.InsertUpdateTimesheet, new
            {
                timeSheetDetail.TimesheetId,
                timeSheetDetail.EmployeeId,
                timeSheetDetail.ClientId,
                timeSheetDetail.TimesheetWeeklyJson,
                ExpectedBurnedMinutes = ExpectedBurnedMinutes,
                ActualBurnedMinutes = ActualBurnedMinutes,
                TotalWeekDays = shiftDetail.TotalWorkingDays,
                TotalWorkingDays = timeSheetDetail.TimesheetWeeklyData.Count(i => i.ActualBurnedMinutes > 0),
                timeSheetDetail.TimesheetStatus,
                timeSheetDetail.TimesheetStartDate,
                timeSheetDetail.TimesheetEndDate,
                timeSheetDetail.UserComments,
                timeSheetDetail.ForYear,
                timeSheetDetail.IsSaved,
                timeSheetDetail.IsSubmitted,
                AdminId = _currentSession.CurrentUserDetail.UserId
            }, true);

            if (string.IsNullOrEmpty(result))
                return null;
            return result;
        }

        public async Task<TimesheetDetail> SubmitTimesheetService(TimesheetDetail timesheetDetail)
        {
            if (timesheetDetail == null || timesheetDetail.TimesheetWeeklyData == null || timesheetDetail.TimesheetWeeklyData.Count == 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Please check you detail.");

            if (timesheetDetail.ClientId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Client id is not valid.");

            ShiftDetail shiftDetail = _db.Get<ShiftDetail>("sp_work_shifts_by_clientId", new { ClientId = timesheetDetail.ClientId });

            timesheetDetail.TimesheetStatus = (int)ItemStatus.Submitted;
            timesheetDetail.IsSubmitted = true;
            timesheetDetail.IsSaved = false;
            var result = this.UpdateOrInsertTimesheetDetail(timesheetDetail, shiftDetail);
            if (string.IsNullOrEmpty(result))
                throw new HiringBellException("Unable to insert/update record. Please contact to admin.");

            await _timesheetEmailService.SendSubmitTimesheetEmail(timesheetDetail);
            return await Task.FromResult(timesheetDetail);
        }

        public async Task<TimesheetDetail> SaveTimesheetService(TimesheetDetail timesheetDetail)
        {
            if (timesheetDetail == null || timesheetDetail.TimesheetWeeklyData == null || timesheetDetail.TimesheetWeeklyData.Count == 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Please check you detail.");

            if (timesheetDetail.ClientId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Client id is not valid.");

            ShiftDetail shiftDetail = _db.Get<ShiftDetail>("sp_work_shifts_by_clientId", new { ClientId = timesheetDetail.ClientId });

            timesheetDetail.IsSubmitted = false;
            timesheetDetail.IsSaved = true;
            var result = this.UpdateOrInsertTimesheetDetail(timesheetDetail, shiftDetail);
            if (string.IsNullOrEmpty(result))
                throw new HiringBellException("Unable to insert/update record. Please contact to admin.");

            return await Task.FromResult(timesheetDetail);
        }

        public async Task<string> ExecuteActionOnTimesheetService(TimesheetDetail timesheetDetail)
        {
            if (timesheetDetail == null || timesheetDetail.TimesheetWeeklyData == null || timesheetDetail.TimesheetWeeklyData.Count == 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Please check you detail.");

            if (timesheetDetail.ClientId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid data submitted. Client id is not valid.");

            ShiftDetail shiftDetail = _db.Get<ShiftDetail>("sp_work_shifts_by_clientId", new { ClientId = timesheetDetail.ClientId });

            timesheetDetail.TimesheetStatus = (int)ItemStatus.Submitted;
            var result = this.UpdateOrInsertTimesheetDetail(timesheetDetail, shiftDetail);
            if (string.IsNullOrEmpty(result))
                throw new HiringBellException("Unable to insert/update record. Please contact to admin.");

            await _timesheetEmailService.SendSubmitTimesheetEmail(timesheetDetail);
            return await Task.FromResult("successfull");
        }

        #endregion

        public List<TimesheetDetail> GetPendingTimesheetByIdService(long employeeId, long clientId)
        {
            List<TimesheetDetail> timesheetDetail = new List<TimesheetDetail>();
            DateTime current = DateTime.UtcNow;

            var currentTimesheetDetail = _db.Get<TimesheetDetail>("sp_employee_timesheet_get", new
            {
                EmployeeId = employeeId,
                ClientId = clientId,
                UserTypeId = _currentSession.CurrentUserDetail.RoleId,
                ForYear = current.Year,
                ForMonth = current.Month,
            });

            timesheetDetail = JsonConvert.DeserializeObject<List<TimesheetDetail>>(currentTimesheetDetail.TimesheetWeeklyJson);
            return timesheetDetail;
        }

        public List<DailyTimesheetDetail> GetEmployeeTimeSheetService(TimesheetDetail timesheetDetail)
        {
            var now = DateTime.UtcNow;
            int daysInMonth = DateTime.DaysInMonth(timesheetDetail.ForYear, timesheetDetail.ForMonth);
            var lastDate = new DateTime(timesheetDetail.ForYear, timesheetDetail.ForMonth, daysInMonth);
            var firstDate = new DateTime(timesheetDetail.ForYear, timesheetDetail.ForMonth, 1);
            List<TimesheetDetail> currentTimesheetDetail = _db.GetList<TimesheetDetail>("sp_employee_timesheet_getby_empid", new
            {
                timesheetDetail.EmployeeId,
                timesheetDetail.ForYear,
                timesheetDetail.ClientId,
                firstDate,
                lastDate
            });

            if (currentTimesheetDetail.Count <= 0)
                throw HiringBellException.ThrowBadRequest("Timesheet is not found");

            return BuildFinalTimesheet(currentTimesheetDetail);
        }

        public List<DailyTimesheetDetail> BuildFinalTimesheet(List<TimesheetDetail> timesheetDetail)
        {
            List<DailyTimesheetDetail> monthlyTimesheet = new List<DailyTimesheetDetail>();
            timesheetDetail.ForEach(x =>
            {
                if (string.IsNullOrEmpty(x.TimesheetWeeklyJson))
                    throw HiringBellException.ThrowBadRequest("Weeklytimesheet not found");

                var dailyTimesheet = JsonConvert.DeserializeObject<List<DailyTimesheetDetail>>(x.TimesheetWeeklyJson);
                dailyTimesheet.ForEach(i =>
                {
                    i.TimesheetStatus = x.TimesheetStatus;
                });

                monthlyTimesheet.AddRange(dailyTimesheet);
            });

            return monthlyTimesheet;
        }

        public BillingDetail EditEmployeeBillDetailService(GenerateBillFileDetail fileDetail)
        {
            BillingDetail billingDetail = default(BillingDetail);
            var now = DateTime.UtcNow;
            int daysInMonth = DateTime.DaysInMonth(fileDetail.ForYear, now.Month);
            var lastDate = new DateTime(fileDetail.ForYear, now.Month, daysInMonth);
            var firstDate = new DateTime(fileDetail.ForYear, now.Month, 1);

            var Result = _db.FetchDataSet("sp_EmployeeBillDetail_ById", new
            {
                CompanyId = _currentSession.CurrentUserDetail.CompanyId,
                EmployeeId = fileDetail.EmployeeId,
                ClientId = fileDetail.ClientId,
                FileId = fileDetail.FileId,
                FirstDate = firstDate,
                LastDate = lastDate,
                ForYear = fileDetail.ForYear
            });

            if (Result.Tables.Count != 4)
                throw HiringBellException.ThrowBadRequest("Server error. Unable to get detail.");

            billingDetail = new BillingDetail();
            billingDetail.FileDetail = Result.Tables[0];
            billingDetail.Employees = Result.Tables[1];
            
            List<TimesheetDetail> currentTimesheetDetail = Converter.ToList<TimesheetDetail>(Result.Tables[2]);
            billingDetail.TimesheetDetails = BuildFinalTimesheet(currentTimesheetDetail);
            billingDetail.Organizations = Result.Tables[3];

            return billingDetail;
        }
    }
}
