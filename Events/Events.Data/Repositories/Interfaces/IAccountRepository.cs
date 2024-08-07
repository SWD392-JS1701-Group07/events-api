﻿using Events.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Data.Repositories.Interfaces
{
    public interface IAccountRepository
    {
        Task<Account> GetAccount(string username, string password);
        Task<List<Account>> GetAllAccounts(string? searchTerm, string? sortColumn, string? sortOrder, int page, int pageSize);
        Task<Account> GetAccountById(int id);
        Task<List<Account>> GetAccountByRole(int roleId, string? searchTerm, string? sortColumn, string? sortOrder, int page, int pageSize);
        Task<Account> CreateAccount(Account account);
        Task<bool> RegisterAccount(Account account);
        Task<bool> BanAccount(int id);
        Task<bool> UpdateAccount(Account account);
        Task<bool> UpdateProfile(Account account);
        Task<Account> GetAccountByUsername(string username);
        Task<Account> GetAccountByPhoneNumber(string phoneNumber);
        Task<Account> GetAccountByEmail(string email);
        Task<Account> GetAccountByStudentId(string studentId);
        Task<List<Account>> GetAccountsByEmailList(string email);
        Task<List<Account>> GetAccountsByPhoneNumberList(string phoneNumber);
    }
}
