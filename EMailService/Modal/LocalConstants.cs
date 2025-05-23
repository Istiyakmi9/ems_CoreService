﻿namespace EMailService.Modal
{
    public class LocalConstants
    {
        public static string DailyJobsManager = "daily-jobs-manager";
        public static string SendEmail = "attendance_request_action";

        // -----------------  Salary components constant values

        public static string EPF = "EPF";
        public static string EEPF = "EPER-PF";
        public static string SPA = "SPA";
        public static string ECI = "ECI";
        public static string ESI = "ESI";
        public static string EESI = "EPER-SI";

        public const string MonthlyPayFrequency = "monthly";
        public const string DailyPayFrequency = "daily";
        public const string HourlyPayFrequency = "hourly";

        public const string FullDay = "f";
        public const string Present = "p";
        public const string HalfDay = "h";
        public const string Absent = "a";
        public const string ZeroTime = "00:00:00";


        public static int DefaultReportingMangerId = 1;
        public static int DefaultWorkShiftId = 1;
        public static int DefaultLeavePlanId = 1;
        public static int DefaultSalaryGroupId = 1;
        public static int DefaultDesignation = 25;

        public static string EmstumFileService = "emstum";

        public static int Profile = 1;
        public static int Document = 2;
        public static int Resume = 3;
        public static int LeaveAttachment = 4;

        //------ Gender ---------
        public static string Male = "Male";
        public static string Female = "Female";
        public static string Any = "Any";

        //---------- Marital Status ---------------------
        public static int Married = 1;
        public static int Single = 2;
        public static int Separated = 3;
        public static int Widowed = 4;
    }
}
