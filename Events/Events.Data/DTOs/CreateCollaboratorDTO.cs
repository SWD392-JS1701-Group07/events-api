﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Data.DTOs
{
    public class CreateCollaboratorDTO
    {
        public int AccountId { get; set; }
        public int EventId { get; set; }
    }
}