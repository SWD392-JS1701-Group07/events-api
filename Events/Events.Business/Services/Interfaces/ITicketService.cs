
﻿using Events.Models.DTOs;
using Events.Models.DTOs.Request;
using Events.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Business.Services.Interfaces
{
    public interface ITicketService
    {
		Task<string> CreateTicket(CreateTicketRequest request);
		Task<IEnumerable<TicketDTO>> GetTicketFilter(int accountId = 1, bool? isBought = null, string? orderId = null, string? searchTern = null, string? includeProps = null);
  }
}
