﻿using ModalLayer.Modal;
using ModalLayer.Modal.Accounts;
using ModalLayer.Modal.Leaves;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceLayer.Interface
{
    public interface ILeaveRequestService
    {
        Task<RequestModel> ApprovalLeaveService(LeaveRequestDetail leaveRequestDetail, int filterId = ApplicationConstants.Only);
        Task<RequestModel> RejectLeaveService(LeaveRequestDetail leaveRequestDetail, int filterId = ApplicationConstants.Only);
        List<LeaveRequestNotification> ReAssigneToOtherManagerService(LeaveRequestNotification approvalRequest, int filterId = ApplicationConstants.Only);
        Task LeaveLeaveManagerMigration(List<CompanySetting> companySettings);
    }
}
