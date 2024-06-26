﻿using Bot.CoreBottomHalf.CommonModal.EmployeeDetail;
using ModalLayer.Modal;
using System;

namespace EMailService.Modal.CronJobs
{
    public class LeaveYearEnd : Employee
    {
        public string TimezoneName { set; get; }
        public TimeZoneInfo Timezone { set; get; }
        public string ConnectionString { set; get; }
        public DateTime ProcessingDateTime { set; get; }
    }
}
