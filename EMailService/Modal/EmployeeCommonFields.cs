﻿namespace ModalLayer.Modal
{
    public class EmployeeCommonFields
    {
        public long EmployeeId { set; get; }
        public string FirstName { set; get; }
        public string LastName { set; get; }
        public string Email { set; get; }
        public string Mobile { set; get; }
        public string ReportingMangaerName { set; get; }
        public long ReportingManagerId { set; get; } // This could be any thing Manager, Reportingmanage, DeliveryManager, CTO or CEO
        public int ProjectId { set; get; }
        public string ProjectName { set; get; }
    }
}
