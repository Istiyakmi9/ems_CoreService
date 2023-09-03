﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using ModalLayer.Modal;
using ModalLayer.Modal.Accounts;
using Newtonsoft.Json;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using static ApplicationConstants;

namespace ServiceLayer.Code
{
    public class SettingService : ISettingService
    {
        private readonly IDb _db;
        private readonly CurrentSession _currentSession;
        private readonly FileLocationDetail _fileLocationDetail;
        private readonly IFileService _fileService;
        private readonly IEvaluationPostfixExpression _postfixToInfixConversion;
        public SettingService(IDb db, CurrentSession currentSession, FileLocationDetail fileLocationDetail, IFileService fileService, IEvaluationPostfixExpression postfixToInfixConversion)
        {
            _db = db;
            _currentSession = currentSession;
            _fileLocationDetail = fileLocationDetail;
            _fileService = fileService;
            _postfixToInfixConversion = postfixToInfixConversion;
        }

        public string AddUpdateComponentService(SalaryComponents salaryComponents)
        {
            salaryComponents = _db.Get<SalaryComponents>("", null);
            return null;
        }

        public PfEsiSetting GetSalaryComponentService(int CompanyId)
        {
            PfEsiSetting pfEsiSettings = new PfEsiSetting();
            var value = _db.Get<PfEsiSetting>("sp_pf_esi_setting_get", new { CompanyId });
            if (value != null)
                pfEsiSettings = value;

            return pfEsiSettings;
        }

        public async Task<PfEsiSetting> PfEsiSetting(int CompanyId, PfEsiSetting pfesiSetting)
        {
            string value = string.Empty;
            var existing = _db.Get<PfEsiSetting>("sp_pf_esi_setting_get", new { CompanyId });
            if (existing != null)
            {
                existing.PFEnable = pfesiSetting.PFEnable;
                existing.IsPfAmountLimitStatutory = pfesiSetting.IsPfAmountLimitStatutory;
                existing.IsPfCalculateInPercentage = pfesiSetting.IsPfCalculateInPercentage;
                existing.IsAllowOverridingPf = pfesiSetting.IsAllowOverridingPf;
                existing.IsPfEmployerContribution = pfesiSetting.IsPfEmployerContribution;
                existing.IsHidePfEmployer = pfesiSetting.IsHidePfEmployer;
                existing.IsPayOtherCharges = pfesiSetting.IsPayOtherCharges;
                existing.IsAllowVPF = pfesiSetting.IsAllowVPF;
                existing.EsiEnable = pfesiSetting.EsiEnable;
                existing.IsAllowOverridingEsi = pfesiSetting.IsAllowOverridingEsi;
                existing.IsHideEsiEmployer = pfesiSetting.IsHideEsiEmployer;
                existing.IsEsiExcludeEmployerShare = pfesiSetting.IsEsiExcludeEmployerShare;
                existing.IsEsiExcludeEmployeeGratuity = pfesiSetting.IsEsiExcludeEmployeeGratuity;
                existing.IsEsiEmployerContributionOutside = pfesiSetting.IsEsiEmployerContributionOutside;
                existing.IsRestrictEsi = pfesiSetting.IsRestrictEsi;
                existing.IsIncludeBonusEsiEligibility = pfesiSetting.IsIncludeBonusEsiEligibility;
                existing.IsIncludeBonusEsiContribution = pfesiSetting.IsIncludeBonusEsiContribution;
                existing.IsEmployerPFLimitContribution = pfesiSetting.IsEmployerPFLimitContribution;
                existing.EmployerPFLimit = pfesiSetting.EmployerPFLimit;
                existing.MaximumGrossForESI = pfesiSetting.MaximumGrossForESI;
                existing.EsiEmployeeContribution = pfesiSetting.EsiEmployeeContribution;
                existing.EsiEmployerContribution = pfesiSetting.EsiEmployerContribution;
            }
            else
                existing = pfesiSetting;

            pfesiSetting.Admin = _currentSession.CurrentUserDetail.UserId;
            value = _db.Execute<PfEsiSetting>("sp_pf_esi_setting_insupd", existing, true);
            if (string.IsNullOrEmpty(value))
                throw new HiringBellException("Unable to update PF Setting.");
            else
                await updateComponentByUpdatingPfEsiSetting(existing);

            return existing;
        }

        private async Task updateComponentByUpdatingPfEsiSetting(PfEsiSetting pfesiSetting)
        {
            var ds = _db.GetDataSet("sp_salary_group_and_components_get", new { CompanyId = pfesiSetting.CompanyId });

            if (!ds.IsValidDataSet(ds))
                throw HiringBellException.ThrowBadRequest("Invalid result got from salary and group table.");

            // var salaryComponents = Converter.ToList<SalaryComponents>(ds.Tables[1]);
            var groups = Converter.ToList<SalaryGroup>(ds.Tables[0]);

            foreach (var gp in groups)
            {
                var salaryComponents = JsonConvert.DeserializeObject<List<SalaryComponents>>(gp.SalaryComponents);

                var component = salaryComponents.Find(x => x.ComponentId == "EPER-PF");
                if (component == null)
                    throw HiringBellException.ThrowBadRequest("Employer contribution toward PF component not found. Please contact to admin");

                component.DeclaredValue = pfesiSetting.EmployerPFLimit;
                component.EmployerContribution = pfesiSetting.EmployerPFLimit;
                component.Formula = pfesiSetting.EmployerPFLimit.ToString();
                component.IncludeInPayslip = pfesiSetting.IsHidePfEmployer;

                component = salaryComponents.Find(x => x.ComponentId == "ECI");
                if (component == null)
                    throw HiringBellException.ThrowBadRequest("Employer contribution toward insurance component not found. Please contact to admin");

                component.DeclaredValue = pfesiSetting.EsiEmployerContribution + pfesiSetting.EsiEmployeeContribution;
                component.Formula = (pfesiSetting.EsiEmployerContribution + pfesiSetting.EsiEmployeeContribution).ToString();
                component.IncludeInPayslip = pfesiSetting.IsHideEsiEmployer;
                component.EmployerContribution = pfesiSetting.EsiEmployerContribution;
                component.EmployeeContribution = pfesiSetting.EsiEmployeeContribution;

                gp.SalaryComponents = JsonConvert.SerializeObject(component);
                _db.Execute("sp_salary_group_insupd", new
                {
                    gp.SalaryGroupId,
                    gp.CompanyId,
                    gp.SalaryComponents,
                    gp.GroupName,
                    gp.GroupDescription,
                    gp.MinAmount,
                    gp.MaxAmount,
                }, true);
            }

            await Task.CompletedTask;
        }

        public List<OrganizationDetail> GetOrganizationInfo()
        {
            List<OrganizationDetail> organizations = _db.GetList<OrganizationDetail>("sp_organization_setting_get", false);
            return organizations;
        }

        public BankDetail GetOrganizationBankDetailInfoService(int OrganizationId)
        {
            BankDetail result = _db.Get<BankDetail>("sp_bank_accounts_get_by_orgId", new { OrganizationId });
            return result;
        }

        public Payroll GetPayrollSetting(int CompanyId)
        {
            var result = _db.Get<Payroll>("sp_payroll_cycle_setting_getById", new { CompanyId });
            return result;
        }

        public string InsertUpdatePayrollSetting(Payroll payroll)
        {
            ValidatePayrollSetting(payroll);

            var status = _db.Execute<Payroll>("sp_payroll_cycle_setting_intupd",
                new
                {
                    PayrollCycleSettingId = payroll.PayrollCycleSettingId,
                    CompanyId = payroll.CompanyId,
                    OrganizationId = payroll.OrganizationId,
                    PayFrequency = payroll.PayFrequency,
                    PayCycleMonth = payroll.PayCycleMonth,
                    PayCycleDayOfMonth = payroll.PayCycleDayOfMonth,
                    PayCalculationId = payroll.PayCalculationId,
                    IsExcludeWeeklyOffs = payroll.IsExcludeWeeklyOffs,
                    IsExcludeHolidays = payroll.IsExcludeHolidays,
                    AdminId = _currentSession.CurrentUserDetail.UserId,
                    DeclarationEndMonth = payroll.PayCycleMonth == 1 ? 12 : payroll.PayCycleMonth - 1
                },
                true
            );

            if (string.IsNullOrEmpty(status))
            {
                throw new HiringBellException("Fail to insert or update.");
            }

            return status;
        }

        private void ValidatePayrollSetting(Payroll payroll)
        {
            if (payroll.CompanyId <= 0)
                throw new HiringBellException("Compnay is mandatory. Please selecte your company first.");

            if (payroll.PayCycleMonth < 0)
                throw HiringBellException.ThrowBadRequest("Please select payroll month first");

            if (string.IsNullOrEmpty(payroll.PayFrequency))
                throw HiringBellException.ThrowBadRequest("Please select pay frequency first");

            if (payroll.PayCycleDayOfMonth < 0)
                throw HiringBellException.ThrowBadRequest("Please select pay cycle day of month first");

            if (payroll.PayCalculationId < 0)
                throw HiringBellException.ThrowBadRequest("Please select payment type first");
        }

        public string InsertUpdateSalaryStructure(List<SalaryStructure> salaryStructure)
        {
            var status = string.Empty;

            return status;
        }

        public async Task<string> UpdateGroupSalaryComponentDetailService(string componentId, int groupId, SalaryComponents component)
        {
            if (groupId <= 0)
                throw new HiringBellException("Invalid groupId");

            if (string.IsNullOrEmpty(componentId))
                throw new HiringBellException("Invalid component passed.");

            decimal formulavalue = 0;
            if (component.Formula != ApplicationConstants.AutoCalculation)
                formulavalue = calculateExpressionUsingInfixDS(component.Formula, 0);

            SalaryGroup salaryGroup = _db.Get<SalaryGroup>("sp_salary_group_getById", new { SalaryGroupId = groupId });
            if (salaryGroup == null)
                throw new HiringBellException("Unable to get salary group. Please contact admin");

            salaryGroup.GroupComponents = JsonConvert.DeserializeObject<List<SalaryComponents>>(salaryGroup.SalaryComponents);

            var existingComponent = salaryGroup.GroupComponents.Find(x => x.ComponentId == component.ComponentId);
            if (existingComponent == null)
            {
                salaryGroup.GroupComponents.Add(component);
            }
            else
            {
                if (string.IsNullOrEmpty(component.Formula))
                    throw new HiringBellException("Given formula is not correct or unable to submit. Please try again or contact to admin");

                if (component.Formula.Contains('%'))
                {
                    int result = 0;
                    var value = int.TryParse(new string(component.Formula.SkipWhile(x => !char.IsDigit(x))
                     .TakeWhile(x => char.IsDigit(x))
                     .ToArray()), out result);
                    existingComponent.PercentageValue = result;
                    existingComponent.MaxLimit = 0;
                    existingComponent.DeclaredValue = 0;
                    existingComponent.CalculateInPercentage = true;
                }
                else
                {
                    int result = 0;
                    var value = int.TryParse(new string(component.Formula.SkipWhile(x => !char.IsDigit(x))
                     .TakeWhile(x => char.IsDigit(x))
                     .ToArray()), out result);
                    existingComponent.DeclaredValue = result;
                    existingComponent.MaxLimit = 0;
                    existingComponent.PercentageValue = 0;
                    existingComponent.CalculateInPercentage = false;
                }
                existingComponent.Formula = component.Formula;
                existingComponent.IncludeInPayslip = component.IncludeInPayslip;
            }

            salaryGroup.SalaryComponents = JsonConvert.SerializeObject(salaryGroup.GroupComponents);
            var status = await _db.ExecuteAsync("sp_salary_group_insupd", new
            {
                salaryGroup.SalaryGroupId,
                salaryGroup.CompanyId,
                salaryGroup.SalaryComponents,
                salaryGroup.GroupName,
                salaryGroup.GroupDescription,
                salaryGroup.MinAmount,
                salaryGroup.MaxAmount,
                AdminId = _currentSession.CurrentUserDetail.UserId
            }, true);

            if (!ApplicationConstants.IsExecuted(status.statusMessage))
                throw new HiringBellException("Fail to update the record.");

            return status.statusMessage;
        }

        private decimal calculateExpressionUsingInfixDS(string expression, decimal declaredAmount)
        {
            if (string.IsNullOrEmpty(expression))
                return declaredAmount;

            if (!expression.Contains("()"))
                expression = string.Format("({0})", expression);

            List<string> operatorStact = new List<string>();
            var expressionStact = new List<object>();
            int index = 0;
            var lastOp = "";
            var ch = "";

            while (index < expression.Length)
            {
                ch = expression[index].ToString();
                if (ch.Trim() == "")
                {
                    index++;
                    continue;
                }
                int number;
                if (!int.TryParse(ch.ToString(), out number))
                {
                    switch (ch)
                    {
                        case "+":
                        case "-":
                        case "/":
                        case "%":
                        case "*":
                            if (operatorStact.Count > 0)
                            {
                                lastOp = operatorStact[operatorStact.Count - 1];
                                if (lastOp == "+" || lastOp == "-" || lastOp == "/" || lastOp == "*" || lastOp == "%")
                                {
                                    lastOp = operatorStact[operatorStact.Count - 1];
                                    operatorStact.RemoveAt(operatorStact.Count - 1);
                                    expressionStact.Add(lastOp);
                                }
                            }
                            operatorStact.Add(ch);
                            break;
                        case ")":
                            while (true)
                            {
                                lastOp = operatorStact[operatorStact.Count - 1];
                                operatorStact.RemoveAt(operatorStact.Count - 1);
                                if (lastOp == "(")
                                {
                                    break;
                                }
                                expressionStact.Add(lastOp);
                            }
                            break;
                        case "(":
                            operatorStact.Add(ch);
                            break;
                    }
                }
                else
                {
                    decimal value = 0;
                    decimal fraction = 0;
                    bool isFractionFound = false;
                    while (true)
                    {
                        ch = expression[index].ToString();
                        if (ch == ".")
                        {
                            index++;
                            isFractionFound = true;
                            break;
                        }

                        if (ch.Trim() == "")
                        {
                            expressionStact.Add($"{value}.{fraction}");
                            break;
                        }

                        if (int.TryParse(ch.ToString(), out number))
                        {
                            if (!isFractionFound)
                                value = Convert.ToDecimal(value + ch);
                            else
                                fraction = Convert.ToDecimal(fraction + ch);
                            index++;
                        }
                        else
                        {
                            index--;
                            expressionStact.Add($"{value}.{fraction}");
                            break;
                        }
                    }
                }

                index++;
            }

            var exp = expressionStact.Aggregate((x, y) => x.ToString() + " " + y.ToString()).ToString();
            // return _postfixToInfixConversion.evaluatePostfix(exp);
            return declaredAmount;
        }

        public async Task<List<SalaryComponents>> ActivateCurrentComponentService(List<SalaryComponents> components)
        {
            List<SalaryComponents> salaryComponents = new List<SalaryComponents>();
            var salaryComponent = _db.GetList<SalaryComponents>("sp_salary_components_get");
            if (salaryComponent != null)
            {
                SalaryComponents componentItem = null;
                Parallel.ForEach<SalaryComponents>(salaryComponent, x =>
                {
                    componentItem = components.Find(i => i.ComponentId == x.ComponentId);
                    if (componentItem != null)
                    {
                        x.IsOpted = componentItem.IsOpted;
                        x.ComponentCatagoryId = componentItem.ComponentCatagoryId;
                    }
                });

                var updateComponents = (from n in salaryComponent
                                        select new
                                        {
                                            n.ComponentId,
                                            n.ComponentFullName,
                                            n.ComponentDescription,
                                            n.CalculateInPercentage,
                                            n.TaxExempt,
                                            n.ComponentTypeId,
                                            n.ComponentCatagoryId,
                                            n.PercentageValue,
                                            n.MaxLimit,
                                            n.DeclaredValue,
                                            n.RejectedAmount,
                                            n.AcceptedAmount,
                                            UploadedFileIds = string.IsNullOrEmpty(n.UploadedFileIds) ? "[]" : n.UploadedFileIds,
                                            n.Formula,
                                            n.EmployeeContribution,
                                            n.EmployerContribution,
                                            n.IncludeInPayslip,
                                            n.IsAdHoc,
                                            n.AdHocId,
                                            n.Section,
                                            n.SectionMaxLimit,
                                            n.IsAffectInGross,
                                            n.RequireDocs,
                                            n.IsOpted,
                                            n.IsActive,
                                            CreatedOn = DateTime.UtcNow,
                                            n.UpdatedOn,
                                            n.CreatedBy,
                                            UpdatedBy = _currentSession.CurrentUserDetail.UserId
                                        }).ToList<object>();

                var status = await _db.BatchInsetUpdate<SalaryComponents>(updateComponents);
                if (string.IsNullOrEmpty(status))
                    throw new HiringBellException("Unable to update detail");
            }
            else
                throw new HiringBellException("Invalid component passed.");

            return salaryComponent;
        }

        public List<SalaryComponents> FetchComponentDetailByIdService(int componentTypeId)
        {
            if (componentTypeId < 0)
                throw new HiringBellException("Invalid component type passed.");

            List<SalaryComponents> salaryComponent = _db.GetList<SalaryComponents>("sp_salary_components_get_type", new { ComponentTypeId = componentTypeId });
            if (salaryComponent == null)
                throw new HiringBellException("Fail to retrieve component detail.");

            return salaryComponent;
        }

        public List<SalaryComponents> FetchActiveComponentService()
        {
            List<SalaryComponents> salaryComponent = _db.GetList<SalaryComponents>("sp_salary_components_get");
            if (salaryComponent == null)
                throw new HiringBellException("Fail to retrieve component detail.");

            return salaryComponent;
        }

        public List<SalaryComponents> UpdateSalaryComponentDetailService(string componentId, SalaryComponents component)
        {
            List<SalaryComponents> salaryComponents = null;

            if (string.IsNullOrEmpty(componentId))
                throw new HiringBellException("Invalid component passed.");

            salaryComponents = _db.GetList<SalaryComponents>("sp_salary_components_get_type", new { ComponentTypeId = 0 });
            if (salaryComponents == null)
                throw new HiringBellException("Fail to retrieve component detail.");

            var salaryComponent = salaryComponents.Find(x => x.ComponentId == componentId);
            if (salaryComponent != null)
            {
                salaryComponent.CalculateInPercentage = component.CalculateInPercentage;
                salaryComponent.TaxExempt = component.TaxExempt;
                salaryComponent.IsActive = component.IsActive;
                salaryComponent.TaxExempt = component.TaxExempt;
                salaryComponent.RequireDocs = component.RequireDocs;
                salaryComponent.IncludeInPayslip = component.IncludeInPayslip;
                salaryComponent.AdminId = _currentSession.CurrentUserDetail.UserId;

                var status = _db.Execute<SalaryComponents>("sp_salary_components_insupd", new
                {
                    salaryComponent.ComponentId,
                    salaryComponent.ComponentFullName,
                    salaryComponent.ComponentDescription,
                    salaryComponent.ComponentCatagoryId,
                    salaryComponent.CalculateInPercentage,
                    salaryComponent.TaxExempt,
                    salaryComponent.ComponentTypeId,
                    salaryComponent.PercentageValue,
                    salaryComponent.MaxLimit,
                    salaryComponent.DeclaredValue,
                    salaryComponent.AcceptedAmount,
                    salaryComponent.RejectedAmount,
                    salaryComponent.UploadedFileIds,
                    salaryComponent.Formula,
                    salaryComponent.EmployeeContribution,
                    salaryComponent.EmployerContribution,
                    salaryComponent.IncludeInPayslip,
                    salaryComponent.IsAdHoc,
                    salaryComponent.AdHocId,
                    salaryComponent.Section,
                    salaryComponent.SectionMaxLimit,
                    salaryComponent.IsAffectInGross,
                    salaryComponent.RequireDocs,
                    salaryComponent.IsOpted,
                    salaryComponent.IsActive,
                    salaryComponent.AdminId
                }, true);

                if (!ApplicationConstants.IsExecuted(status))
                    throw new HiringBellException("Fail to update the record.");
            }
            else
            {
                throw new HiringBellException("Invalid component passed.");
            }

            return salaryComponents;
        }
    }
}