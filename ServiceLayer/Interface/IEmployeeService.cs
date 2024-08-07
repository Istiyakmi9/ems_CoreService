﻿using Bot.CoreBottomHalf.CommonModal;
using Bot.CoreBottomHalf.CommonModal.EmployeeDetail;
using Microsoft.AspNetCore.Http;
using ModalLayer.Modal;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ServiceLayer.Interface
{
    public interface IEmployeeService
    {
        List<Employee> GetEmployees(FilterModel filterModel);
        List<AutoCompleteEmployees> EmployeesListDataService(FilterModel filterModel);
        DataSet GetManageEmployeeDetailService(long EmployeeId);
        DataSet GetEmployeeLeaveDetailService(long EmployeeId);
        DataSet LoadMappedClientService(long EmployeeId);
        DataSet GetManageClientService(long EmployeeId);
        DataSet UpdateEmployeeMappedClientDetailService(EmployeeMappedClient employeeMappedClient, bool IsUpdating);
        Employee GetEmployeeByIdService(int EmployeeId, int IsActive);
        List<Employee> ActivateOrDeActiveEmployeeService(int EmployeeId, bool IsActive);
        Task<string> RegisterEmployeeService(Employee employee, IFormFileCollection fileCollection);
        Task RegisterEmployeeByExcelService(Employee employee, UploadedPayrollData emp, EmployeeCalculation employeeCalculation);
        Task<string> UpdateEmployeeService(Employee employee, IFormFileCollection fileCollection);
        dynamic GetBillDetailForEmployeeService(FilterModel filterModel);
        Task<string> GenerateOfferLetterService(EmployeeOfferLetter employeeOfferLetter);
        Task<string> ExportEmployeeService(int CompanyId, int FileType);
        Task<string> UploadEmployeeExcelService(List<Employee> employees, IFormFileCollection formFiles);
        Task<List<Employee>> ReadEmployeeDataService(IFormFileCollection files);
        Task<dynamic> GetEmployeeResignationByIdService(long employeeId);
        Task<string> SubmitResignationService(EmployeeNoticePeriod employeeNoticePeriod);
        Task<string> ManageInitiateExistService(EmployeeNoticePeriod employeeNoticePeriod);
        EmployeeEmailMobileCheck GetEmployeeDetail(EmployeeCalculation employeeCalculation);
        Task<string> RegisterOrUpdateEmployeeDetail(EmployeeCalculation eCal, IFormFileCollection fileCollection, bool isEmpByExcel = false);
        void CreateFinancialStartEndDatetime(EmployeeCalculation employeeCalculation);
    }
}
