﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Data.DTOs.Request
{
	public class CreateSponsorDTO
	{
		public string Name { get; set; }
		public string Email { get; set; }
		public string PhoneNumber { get; set; }
		public IFormFile? AvatarFile { get; set; }
		public int? AccountId { get; set; }
	}
}
