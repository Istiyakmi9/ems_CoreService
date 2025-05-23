﻿using Bot.CoreBottomHalf.CommonModal.EmployeeDetail;
using EMailService.Modal;
using EMailService.Modal.EmployeeModal;
using Microsoft.AspNetCore.Http;
using ModalLayer.Modal;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ServiceLayer.Interface
{
    public interface IEmployeeService
    {
        (List<Employee> employees, List<RecordHealthStatus> recordHealthStatuse) GetEmployees(FilterModel filterModel);
        DataSet GetManageEmployeeDetailService(long EmployeeId);
        DataSet GetManageClientService(long EmployeeId);
        DataSet UpdateEmployeeMappedClientDetailService(EmployeeMappedClient employeeMappedClient, bool IsUpdating);
        Employee GetEmployeeByIdService(int EmployeeId, int IsActive);
        List<Employee> ActivateOrDeActiveEmployeeService(int EmployeeId, bool IsActive);
        Task<string> RegisterEmployeeService(Employee employee, IFormFileCollection fileCollection, bool IsNewRegistration = false);
        Task RegisterEmployeeByExcelService(Employee employee, UploadedPayrollData emp);
        Task<string> UpdateEmployeeService(Employee employee, IFormFileCollection fileCollection);
        dynamic GetBillDetailForEmployeeService(FilterModel filterModel);
        Task<string> GenerateOfferLetterService(EmployeeOfferLetter employeeOfferLetter);
        Task<byte[]> ExportEmployeeService(int CompanyId, int FileType);
        Task<List<UploadEmpExcelError>> ReadEmployeeDataService(IFormFileCollection files);
        Task<dynamic> GetEmployeeResignationByIdService(long employeeId);
        Task<string> SubmitResignationService(EmployeeNoticePeriod employeeNoticePeriod);
        Task<string> ManageInitiateExistService(EmployeeNoticePeriod employeeNoticePeriod);
        Task<(EmployeeBasicInfo employeeBasic, List<FileDetail> fileDetails)> ManageEmployeeBasicInfoService(EmployeeBasicInfo employeeBasicInfo, IFormFileCollection files);
        Task<string> ManageEmpPerosnalDetailService(EmpPersonalDetail empPersonalDetail);
        Task<string> ManageEmpAddressDetailService(EmployeeAddressDetail employeeAddressDetail);
        Task<string> ManageEmpProfessionalDetailService(EmployeeProfessionalDetail employeeProfessionalDetail);
        Task<string> ManageEmpPrevEmploymentDetailService(PrevEmploymentDetail prevEmploymentDetail);
        Task<string> ManageEmpBackgroundVerificationDetailService(EmployeeBackgroundVerification employeeBackgroundVerification);
        Task<string> ManageEmpNomineeDetailService(EmployeeNomineeDetail employeeNomineeDetail);
        Task<List<RecordHealthStatus>> GetEmployeesRecordHealthStatusService();
        Task<List<RecordHealthStatus>> FixEmployeesRecordHealthStatusService(List<long> employeeIds);
        Task<byte[]> ExportEmployeeWithDataService();
        Task<byte[]> ExportEmployeeSkeletonExcelService();
        Task<List<UploadEmpExcelError>> GetEmployeeUploadErrorLogsService();
        Task<(List<Employee> employees, List<RecordHealthStatus> recordHealthStatus)> DeActiveEmployeeService(long employeeId);
        #region Un-used method

        //EmployeeEmailMobileCheck GetEmployeeDetail(EmployeeCalculation employeeCalculation);
        // Task<string> UploadEmployeeExcelService(List<Employee> employees, IFormFileCollection formFiles);
        //Task<string> RegisterOrUpdateEmployeeDetail(EmployeeCalculation eCal, IFormFileCollection fileCollection, bool isEmpByExcel = false);
        //void CreateFinancialStartEndDatetime(EmployeeCalculation employeeCalculation);
        //DataSet GetEmployeeLeaveDetailService(long EmployeeId);
        //DataSet LoadMappedClientService(long EmployeeId);
        //List<AutoCompleteEmployees> EmployeesListDataService(FilterModel filterModel);

        #endregion
    }
}
