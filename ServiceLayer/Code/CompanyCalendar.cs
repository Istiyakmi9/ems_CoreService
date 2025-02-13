﻿using Bot.CoreBottomHalf.CommonModal;
using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Interface;
using EMailService.Modal;
using Microsoft.AspNetCore.Http;
using ModalLayer.Modal;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Calendar = ModalLayer.Calendar;

namespace ServiceLayer
{
    public class CompanyCalendar : ICompanyCalendar
    {
        private List<Calendar> _calendars;
        private readonly CurrentSession _currentSession;
        private readonly IDb _db;
        private readonly CurrentSession _session;
        private readonly ITimezoneConverter _timezoneConverter;
        private ShiftDetail _shiftDetail;
        private readonly IUtilityService _utilityService;
        public CompanyCalendar(IDb db,
            CurrentSession session,
            CurrentSession currentSession,
            ITimezoneConverter timezoneConverter,
            IShiftService shiftService,
            IUtilityService utilityService)
        {
            _db = db;
            _session = session;
            _currentSession = currentSession;
            _timezoneConverter = timezoneConverter;
            _utilityService = utilityService;
        }

        public void LoadHolidayCalendar()
        {
            if (_calendars == null)
            {
                _calendars = _db.GetList<Calendar>(Procedures.Company_Calendar_Get_By_Company, new { _session.CurrentUserDetail.CompanyId });
            }
        }

        public async Task<bool> IsHoliday(DateTime date)
        {
            bool flag = false;

            LoadHolidayCalendar();
            //var records = _calendars.FirstOrDefault(x => x.StartDate.Date.Subtract(date.Date).TotalDays <= 0
            //                && x.EndDate.Date.Subtract(date.Date).TotalDays >= 0);

            var records = _calendars.Find(x => x.HolidayDate.Date.Subtract(date.Date).TotalDays == 0);
            if (records != null)
                flag = true;

            return await Task.FromResult(flag);
        }

        private bool CheckIsWeekend(DateTime date)
        {
            var flag = false;
            var zoneDate = _timezoneConverter.ToTimeZoneDateTime(date, _currentSession.TimeZone);
            switch (zoneDate.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    if (!_shiftDetail.IsSun)
                        flag = true;
                    break;
                case DayOfWeek.Monday:
                    if (!_shiftDetail.IsMon)
                        flag = true;
                    break;
                case DayOfWeek.Tuesday:
                    if (!_shiftDetail.IsTue)
                        flag = true;
                    break;
                case DayOfWeek.Wednesday:
                    if (!_shiftDetail.IsWed)
                        flag = true;
                    break;
                case DayOfWeek.Thursday:
                    if (!_shiftDetail.IsThu)
                        flag = true;
                    break;
                case DayOfWeek.Friday:
                    if (!_shiftDetail.IsFri)
                        flag = true;
                    break;
                case DayOfWeek.Saturday:
                    if (!_shiftDetail.IsSat)
                        flag = true;
                    break;
            }
            return flag;
        }

        public int CountHolidaysBeforeDate(DateTime date, ShiftDetail shiftDetail)
        {
            _shiftDetail = shiftDetail;
            int totalDays = 0;
            date = date.AddDays(-1);

            LoadHolidayCalendar();
            //var holiday = _calendars.Find(i => i.EndDate.Date.Subtract(date.Date).TotalDays == 0);
            //while (holiday != null)
            //{
            //    while (date.Date.Subtract(holiday.StartDate.Date).TotalDays >= 0)
            //    {
            //        // check date is weekoff or not
            //        // if yes do nothing
            //        // else increament
            //        if (!CheckIsWeekend(date))
            //            totalDays++;

            //        date = date.AddDays(-1);
            //    }

            //    holiday = _calendars.Find(i => i.EndDate.Date.Subtract(date.Date).TotalDays == 0);
            //}

            var holiday = _calendars.Find(i => i.HolidayDate.Date.Subtract(date.Date).TotalDays == 0);
            if (holiday != null && !CheckIsWeekend(date))
                totalDays++;

            return totalDays;
        }

        public int CountHolidaysAfterDate(DateTime date, ShiftDetail shiftDetail)
        {
            _shiftDetail = shiftDetail;
            int totalDays = 0;
            date = date.AddDays(1);

            LoadHolidayCalendar();
            //var holiday = _calendars.Find(i => i.StartDate.Date.Subtract(date.Date).TotalDays == 0);
            //while (holiday != null)
            //{
            //    while (date.Date.Subtract(holiday.EndDate.Date).TotalDays <= 0)
            //    {
            //        // check date is weekoff or not
            //        // if yes do nothing
            //        // else increament
            //        if (!CheckIsWeekend(date))
            //            totalDays++;

            //        date = date.AddDays(1);
            //    }

            //    holiday = _calendars.Find(i => i.StartDate.Date.Subtract(date.Date).TotalDays == 0);
            //}
            var holiday = _calendars.Find(i => i.HolidayDate.Date.Subtract(date.Date).TotalDays == 0);
            if (holiday != null && !CheckIsWeekend(date))
                totalDays++;

            return totalDays;
        }

        public async Task<bool> IsHolidayBetweenTwoDates(DateTime fromDate, DateTime toDate)
        {
            bool flag = false;

            LoadHolidayCalendar();
            //var records = _calendars.Where(x => x.StartDate.Date >= fromDate.Date && x.EndDate.Date <= toDate.Date);
            var records = _calendars.Where(x => x.HolidayDate.Date.Subtract(fromDate.Date).TotalDays >= 0 && x.HolidayDate.Date.Subtract(toDate.Date).TotalDays <= 0);
            if (records.Any())
                flag = true;

            return await Task.FromResult(flag);
        }

        public async Task<int> GetHolidayBetweenTwoDates(DateTime fromDate, DateTime toDate)
        {
            LoadHolidayCalendar();
            //var holidays = _calendars.Count(x => (x.StartDate.Date >= fromDate.Date && x.EndDate.Date <= fromDate.Date));
            var holidays = _calendars.Count(x => x.HolidayDate.Date.Subtract(fromDate.Date).TotalDays >= 0 && x.HolidayDate.Date.Subtract(toDate.Date).TotalDays <= 0);

            return await Task.FromResult(holidays);
        }

        public async Task<decimal> GetHolidayCountInMonth(int month, int year)
        {
            decimal totalDays = 0;
            DateTime fromDate = new DateTime(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            LoadHolidayCalendar();

            //int fullDayHoliday = _calendars.Count(x => (x.StartDate.Date >= fromDate.Date && x.EndDate.Date <= fromDate.Date) && x.IsHalfDay);
            //int halfDayHoliday = _calendars.Count(x => (x.StartDate.Date >= fromDate.Date && x.EndDate.Date <= fromDate.Date) && !x.IsHalfDay);

            int fullDayHoliday = _calendars.Count(x => x.HolidayDate.Date.Subtract(fromDate.Date).TotalDays >= 0 && x.HolidayDate.Date.Subtract(toDate.Date).TotalDays <= 0 && x.IsHalfDay);
            int halfDayHoliday = _calendars.Count(x => x.HolidayDate.Date.Subtract(fromDate.Date).TotalDays >= 0 && x.HolidayDate.Date.Subtract(toDate.Date).TotalDays <= 0 && !x.IsHalfDay);

            totalDays = (decimal)(fullDayHoliday + (halfDayHoliday * 0.5));

            return await Task.FromResult(totalDays);
        }

        public async Task<bool> IsWeekOff(DateTime date)
        {
            bool flag = false;
            if (date.DayOfWeek == DayOfWeek.Sunday || date.DayOfWeek == DayOfWeek.Saturday)
                flag = true;

            return await Task.FromResult(flag);
        }

        public async Task<bool> IsWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate)
        {
            bool flag = false;
            while (fromDate.Date <= toDate.Date)
            {
                if (fromDate.DayOfWeek == DayOfWeek.Saturday || fromDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    flag = true;
                    break;
                }
                fromDate.AddDays(1);
            }
            return await Task.FromResult(flag);
        }

        public async Task<List<DateTime>> GetWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate)
        {
            List<DateTime> holidays = new List<DateTime>();
            while (fromDate.Date <= toDate.Date)
            {
                if (fromDate.DayOfWeek == DayOfWeek.Saturday || fromDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    holidays.Add(fromDate);
                }
                fromDate.AddDays(1);
            }
            return await Task.FromResult(holidays);
        }

        public async Task<int> CountWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate, ShiftDetail shiftDetail)
        {
            int count = 0;
            while (fromDate.Date <= toDate.Date)
            {
                var zoneDate = _timezoneConverter.ToTimeZoneDateTime(fromDate, _currentSession.TimeZone);
                switch (zoneDate.DayOfWeek)
                {
                    case DayOfWeek.Sunday:
                        if (!shiftDetail.IsSun)
                            count++;
                        break;
                    case DayOfWeek.Monday:
                        if (!shiftDetail.IsMon)
                            count++;
                        break;
                    case DayOfWeek.Tuesday:
                        if (!shiftDetail.IsTue)
                            count++;
                        break;
                    case DayOfWeek.Wednesday:
                        if (!shiftDetail.IsWed)
                            count++;
                        break;
                    case DayOfWeek.Thursday:
                        if (!shiftDetail.IsThu)
                            count++;
                        break;
                    case DayOfWeek.Friday:
                        if (!shiftDetail.IsFri)
                            count++;
                        break;
                    case DayOfWeek.Saturday:
                        if (!shiftDetail.IsSat)
                            count++;
                        break;
                }
                fromDate = fromDate.AddDays(1);
            }
            return await Task.FromResult(count);
        }

        public List<Calendar> GetAllHolidayService(FilterModel filterModel)
        {
            var result = _db.GetList<Calendar>(Procedures.Company_Calender_Getby_Filter, new
            {
                filterModel.SearchString,
                filterModel.PageIndex,
                filterModel.PageSize,
                filterModel.SortBy
            });
            return result;
        }

        public List<Calendar> HolidayInsertUpdateService(Calendar calendar)
        {
            var existCalendar = new Calendar();
            ValidateCalender(calendar);
            var result = _db.GetList<Calendar>(Procedures.Company_Calendar_Get_By_Company, new { _currentSession.CurrentUserDetail.CompanyId });
            if (result.Count > 0)
            {
                existCalendar = result.Find(x => x.CompanyCalendarId == calendar.CompanyCalendarId);
                if (existCalendar != null)
                {
                    existCalendar.CompanyId = calendar.CompanyId;
                    existCalendar.EventName = calendar.EventName;
                    existCalendar.IsHoliday = calendar.IsHoliday;
                    existCalendar.IsHalfDay = calendar.IsHalfDay;
                    existCalendar.HolidayDate = calendar.HolidayDate;
                    existCalendar.DescriptionNote = calendar.DescriptionNote;
                    existCalendar.ApplicableFor = calendar.ApplicableFor;
                    existCalendar.Year = calendar.Year;
                    existCalendar.IsPublicHoliday = calendar.IsPublicHoliday;
                    existCalendar.IsCompanyCustomHoliday = calendar.IsCompanyCustomHoliday;
                    existCalendar.Country = calendar.Country;
                }
                else
                {
                    existCalendar = calendar;
                }
            }
            else
            {
                existCalendar = calendar;
            }
            var value = _db.Execute<Calendar>(Procedures.Company_Calendar_Insupd, new
            {
                existCalendar.CompanyCalendarId,
                _currentSession.CurrentUserDetail.CompanyId,
                existCalendar.EventName,
                existCalendar.IsHoliday,
                existCalendar.IsHalfDay,
                existCalendar.HolidayDate,
                existCalendar.DescriptionNote,
                existCalendar.ApplicableFor,
                existCalendar.Year,
                existCalendar.IsPublicHoliday,
                existCalendar.IsCompanyCustomHoliday,
                existCalendar.Country,
                AdminId = _currentSession.CurrentUserDetail.UserId
            }, true);

            if (string.IsNullOrEmpty(value))
                throw HiringBellException.ThrowBadRequest("Fail to insert/ update holiday");

            FilterModel filterModel = new FilterModel
            {
                SearchString = $"1=1 and CompanyId={calendar.CompanyId}"
            };
            return GetAllHolidayService(filterModel);
        }

        private void ValidateCalender(Calendar calendar)
        {
            if (string.IsNullOrEmpty(calendar.DescriptionNote))
                throw HiringBellException.ThrowBadRequest("Description note is null or empty");

            if (string.IsNullOrEmpty(calendar.Country))
                throw HiringBellException.ThrowBadRequest("Country is null or empty");

            if (string.IsNullOrEmpty(calendar.EventName))
                throw HiringBellException.ThrowBadRequest("Event name is null or empty");

            if (calendar.CompanyId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid company id");
        }

        public List<Calendar> DeleteHolidayService(long CompanyCalendarId)
        {
            if (CompanyCalendarId <= 0)
                throw HiringBellException.ThrowBadRequest("Invalid holiday selected. Please select a vlid holiday");

            var result = _db.Execute<Calendar>(Procedures.Company_Calender_Delete_By_Calenderid, new { CompanyCalendarId = CompanyCalendarId }, true);
            if (string.IsNullOrEmpty(result))
                throw HiringBellException.ThrowBadRequest("Fail to delete holiday");

            FilterModel filterModel = new FilterModel
            {
                SearchString = $"1=1 and CompanyId={_session.CurrentUserDetail.CompanyId}"
            };
            return GetAllHolidayService(filterModel);
        }

        public async Task<List<Calendar>> ReadHolidayDataService(IFormFileCollection files)
        {
            try
            {
                var uploadedHolidayData = await _utilityService.ReadExcelData<Calendar>(files);
                var result = await UpdateHolidayData(uploadedHolidayData);
                return result;
            }
            catch
            {
                throw;
            }
        }

        private async Task<List<Calendar>> UpdateHolidayData(List<Calendar> uploadedHolidayData)
        {
            var companyId = _currentSession.CurrentUserDetail.CompanyId;
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var result = _db.GetList<Calendar>(Procedures.Company_Calendar_Get_By_Company, new { CompanyId = companyId });

            foreach (Calendar calendar in uploadedHolidayData)
            {
                var existCalendar = new Calendar();

                calendar.CompanyId = companyId;

                ValidateCalender(calendar);

                calendar.EventName = calendar.EventName.ToUpper();
                calendar.DescriptionNote = calendar.DescriptionNote.ToUpper();
                calendar.Country = textInfo.ToTitleCase(calendar.Country);

                if (result.Count > 0)
                {
                    existCalendar = result.Find(x => _timezoneConverter.ToSpecificTimezoneDateTime(_currentSession.TimeZone, x.StartDate)
                    .Subtract(_timezoneConverter.ToSpecificTimezoneDateTime(_currentSession.TimeZone, calendar.StartDate)).TotalDays == 0);
                    if (existCalendar != null)
                    {
                        existCalendar.CompanyId = calendar.CompanyId;
                        existCalendar.HolidayDate = calendar.HolidayDate;
                        existCalendar.EventName = calendar.EventName;
                        existCalendar.IsHoliday = calendar.IsHoliday;
                        existCalendar.IsHalfDay = calendar.IsHalfDay;
                        existCalendar.DescriptionNote = calendar.DescriptionNote;
                        existCalendar.ApplicableFor = 1;
                        existCalendar.Year = calendar.Year;
                        existCalendar.IsPublicHoliday = calendar.IsPublicHoliday;
                        existCalendar.IsCompanyCustomHoliday = calendar.IsCompanyCustomHoliday;
                        existCalendar.Country = calendar.Country;
                    }
                }
                else
                {
                    existCalendar = calendar;
                }

                existCalendar.HolidayDate = _timezoneConverter.ToUtcTime(existCalendar.HolidayDate, _currentSession.TimeZone);
                existCalendar.AdminId = _currentSession.CurrentUserDetail.UserId;

                var value = _db.Execute<Calendar>(Procedures.Company_Calendar_Insupd, existCalendar, true);
                if (string.IsNullOrEmpty(value))
                    throw HiringBellException.ThrowBadRequest("Fail to insert/ update holiday");
            }

            FilterModel filterModel = new FilterModel
            {
                SearchString = $"1=1 and CompanyId={companyId}"
            };
            var data = GetAllHolidayService(filterModel);

            return await Task.FromResult(data);
        }
    }
}