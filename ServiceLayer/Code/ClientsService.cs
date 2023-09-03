﻿using BottomhalfCore.DatabaseLayer.Common.Code;
using BottomhalfCore.Services.Code;
using Microsoft.AspNetCore.Http;
using ModalLayer.Modal;
using ServiceLayer.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ServiceLayer.Code
{
    public class ClientsService : IClientsService
    {
        private readonly IDb _db;
        private readonly CommonFilterService _commonFilterService;
        private readonly CurrentSession _currentSession;
        private readonly IFileService _fileService;
        private readonly FileLocationDetail _fileLocationDetail;
        public ClientsService(IDb db, CommonFilterService commonFilterService, CurrentSession currentSession, IFileService fileService, FileLocationDetail fileLocationDetail)
        {
            _db = db;
            _commonFilterService = commonFilterService;
            _currentSession = currentSession;
            _fileService = fileService;
            _fileLocationDetail = fileLocationDetail;
        }
        public List<Organization> GetClients(FilterModel filterModel)
        {
            List<Organization> client = _commonFilterService.GetResult<Organization>(filterModel, "SP_Clients_Get");
            return client;
        }

        public DataSet GetClientDetailById(long ClientId, bool IsActive, int UserTypeId)
        {
            //if (ClientId <= 0)
            //    throw new HiringBellException { UserMessage = "Invalid ClientId", FieldName = nameof(ClientId), FieldValue = ClientId.ToString() };

            //Organization client = default;
            var resultSet = _db.GetDataSet("SP_Client_ById", new
            {
                ClientId = ClientId,
                IsActive = IsActive,
                UserTypeId = UserTypeId,
                CompanyId = _currentSession.CurrentUserDetail.CompanyId
            });

            if (resultSet.Tables.Count != 3)
                throw HiringBellException.ThrowBadRequest("Got server error. Please contact to admin.");

            resultSet.Tables[0].TableName = "client";
            resultSet.Tables[1].TableName = "file";
            resultSet.Tables[2].TableName = "shifts";

            return resultSet;
        }

        public async Task<Organization> RegisterClient(Organization client, IFormFileCollection fileCollection, bool isUpdating)
        {
            try
            {
                if (isUpdating == true)
                {
                    if (client.ClientId <= 0)
                        throw new HiringBellException { UserMessage = "Invalid ClientId", FieldName = nameof(client.ClientId), FieldValue = client.ClientId.ToString() };
                }

                ClientValidation(client);
                Organization organization = null;

                organization = _db.Get<Organization>("SP_Client_IntUpd", new
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    PrimaryPhoneNo = client.PrimaryPhoneNo,
                    SecondaryPhoneNo = client.SecondaryPhoneNo,
                    MobileNo = client.MobileNo,
                    Email = client.Email,
                    OtherEmail_1 = client.OtherEmail_1,
                    OtherEmail_2 = client.OtherEmail_2,
                    OtherEmail_3 = client.OtherEmail_3,
                    OtherEmail_4 = client.OtherEmail_4,
                    Fax = client.Fax,
                    GSTNO = client.GSTNo,
                    PanNo = client.PanNo,
                    Pincode = client.Pincode,
                    Country = client.Country,
                    State = client.State,
                    City = client.City,
                    FirstAddress = client.FirstAddress,
                    SecondAddress = client.SecondAddress,
                    ThirdAddress = client.ThirdAddress,
                    ForthAddress = client.ForthAddress,
                    IFSC = client.IFSC,
                    AccountNo = client.AccountNo,
                    BankName = client.BankName,
                    BranchName = client.BranchName,
                    client.WorkShiftId,
                    CompanyId = _currentSession.CurrentUserDetail.CompanyId,
                    AdminId = _currentSession.CurrentUserDetail.UserId,
                }, true);

                if (organization.ClientId <= 0)
                    throw new Exception("fail to get client id");

                if (fileCollection.Count > 0)
                {
                    var files = fileCollection.Select(x => new Files
                    {
                        FileUid = client.FileId,
                        FileName = fileCollection[0].Name,
                        Email = client.Email,
                        FileExtension = string.Empty
                    }).ToList<Files>();

                    var ownerFolderPath = Path.Combine(_fileLocationDetail.UserFolder, $"{UserType.Client}_{organization.ClientId}");
                    _fileService.SaveFile(ownerFolderPath, files, fileCollection, client.OldFileName);

                    var fileInfo = (from n in files
                                    select new
                                    {
                                        FileId = n.FileUid,
                                        FileOwnerId = organization.ClientId,
                                        FileName = n.FileName.Contains(".") ? n.FileName : n.FileName+"."+n.FileExtension,
                                        FilePath = n.FilePath,
                                        FileExtension = n.FileExtension,
                                        UserTypeId = (int)UserType.Client,
                                        AdminId = _currentSession.CurrentUserDetail.UserId
                                    }).ToList();
                    

                    var batchResult = await _db.BulkExecuteAsync("sp_userfiledetail_Upload", fileInfo, true);
                }
                organization.GSTNo = client.GSTNo;
                return organization;
            }
            catch (Exception)
            {
                throw;
            }

        }

        private void ClientValidation(Organization organization)
        {
            if (string.IsNullOrEmpty(organization.Email))
                throw new HiringBellException { UserMessage = "Email id is a mandatory field.", FieldName = nameof(organization.Email), FieldValue = organization.Email.ToString() };

            if (string.IsNullOrEmpty(organization.ClientName))
                throw new HiringBellException { UserMessage = "First Name is a mandatory field.", FieldName = nameof(organization.ClientName), FieldValue = organization.ClientName.ToString() };

            if (string.IsNullOrEmpty(organization.PrimaryPhoneNo) || organization.PrimaryPhoneNo.Contains("."))
                throw new HiringBellException { UserMessage = "Mobile number is a mandatory field.", FieldName = nameof(organization.PrimaryPhoneNo), FieldValue = organization.PrimaryPhoneNo.ToString() };

            var mail = new MailAddress(organization.Email);
            bool isValidEmail = mail.Host.Contains(".");
            if (!isValidEmail)
                throw new HiringBellException { UserMessage = "The email is invalid.", FieldName = nameof(organization.Email), FieldValue = organization.Email.ToString() };
        }

        public DataSet DeactivateClient(Employee employee)
        {
            if (employee == null || employee.EmployeeUid <= 0)
                throw new HiringBellException("Invalid client detail submitted.");

            var resultSet = _db.FetchDataSet("sp_deactivateOrganization_delandgetall", new
            {
                ClientMappedId = employee.EmployeeMappedClientsUid,
                UserId = employee.EmployeeUid
            });
            return resultSet;
        }
    }
}
