﻿using Events.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Data.Repositories.Interfaces
{
    public interface IEventScheduleRepository
    {
        Task<List<EventSchedule>> GetAllEventSchedule();
        Task<List<EventSchedule>> GetEventScheduleById(int id);
    }
}