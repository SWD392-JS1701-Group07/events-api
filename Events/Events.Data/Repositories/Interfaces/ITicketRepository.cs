﻿using Events.Models.DTOs.Request;
using Events.Models.Models;

namespace Events.Data.Repositories.Interfaces
{
    public interface ITicketRepository
    {
        Task CreateTicket(Ticket ticket);
        Task<IEnumerable<Ticket>> GetTicketFilter(Account account, int? customerId, bool? isBought = null, string? orderId = null, string? searchTern = null, string? includeProps = null);
        Task<bool> CheckTicketExist(string email, string phoneNumber, int eventId);
        Task<Ticket> GetTicketById(string ticketId);
        Task<IEnumerable<Ticket>> GetTicketByEventId(int id, string? searchTerm);
        Task<IEnumerable<Ticket>> GetTicketByOrderId(string id);
        Task<bool> UpdateTicket(Ticket ticket);
	}
}
    