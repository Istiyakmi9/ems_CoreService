﻿using Microsoft.AspNetCore.Http;
using ModalLayer;
using ModalLayer.Modal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceLayer.Interface
{
    public interface ICompanyCalendar
    {
        Task<bool> IsHoliday(DateTime date);
        int CountHolidaysBeforeDate(DateTime date, ShiftDetail shiftDetail);
        int CountHolidaysAfterDate(DateTime date, ShiftDetail shiftDetail);
        Task<bool> IsHolidayBetweenTwoDates(DateTime fromDate, DateTime toDate);
        Task<int> GetHolidayBetweenTwoDates(DateTime fromDate, DateTime toDate);
        Task<bool> IsWeekOff(DateTime date);
        Task<bool> IsWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate);
        Task<List<DateTime>> GetWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate);
        List<Calendar> GetAllHolidayService(FilterModel filterModel);
        List<Calendar> HolidayInsertUpdateService(Calendar calendar);
        List<Calendar> DeleteHolidayService(long CompanyCalendarId);
        Task<int> CountWeekOffBetweenTwoDates(DateTime fromDate, DateTime toDate, ShiftDetail shiftDetail);
        Task<decimal> GetHolidayCountInMonth(int month, int year);
        Task<List<Calendar>> ReadHolidayDataService(IFormFileCollection files);
    }
}
