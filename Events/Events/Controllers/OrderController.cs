﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Events.Business.Services;
using Events.Business.Services.Interfaces;
using Events.Data.Repositories.Interfaces;
using Events.Models.DTOs;
using Events.Models.DTOs.Request;
using Events.Models.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Protocol.Plugins;

namespace Events.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "v1")]
    [Route("api/event/order")]
    [ApiVersionNeutral]
    public class OrderController : ControllerBase
    {
		private readonly IOrderService _orderService;
		private readonly IConfiguration _configuration;

		public OrderController(IOrderService orderService, IConfiguration configuration)
		{
			_orderService=orderService;
			_configuration=configuration;
		}

		[HttpPost("create")]
        public async Task<IActionResult> CreateOrderAndPayment([FromBody] CreateOrderRequest request)
        {
            if (request == null || !ModelState.IsValid)
            {
				return BadRequest(new BaseResponse
				{
					StatusCode = StatusCodes.Status400BadRequest,
					Message = "Invalid request body",
					IsSuccess = false
				});
			}
			var response = await _orderService.CreateOrderAndPayment(request, HttpContext);
			return StatusCode(response.StatusCode, response);
		}

        [HttpGet("/callback")]
        public async Task<IActionResult> Callback()
        {
            var response = await _orderService.HandlePaymentCallback(Request.Query);
			string? message;
			var url = $"{_configuration["AppFrontend"]}/callback";
			if(response.IsSuccess)
			{
				var data = response.Data as PaymentResponseModel;
				if (data!.VnPayResponseCode == "00")
				{
					message = "Payment success! Thanks your order";
					url += $"?success=true&orderId={data.OrderId}&orderDescription={data.OrderDescription}&amount={data.Amount}&transactionNo={data.TransactionId}&message={message}";
				}
				else if(data!.VnPayResponseCode == "24")
				{
					message = "Transaction canceled, Please payment again!";
					url += $"?success=false&orderId={data.OrderId}&orderDescription={data.OrderDescription}&amount={data.Amount}&transactionNo={data.TransactionId}&message={message}";
				}
				else
				{
					message = "An error occurred during the payment process, Please payment again!";
					url += $"?success=false&orderId={data.OrderId}&orderDescription={data.OrderDescription}&amount={data.Amount}&transactionNo={data.TransactionId}&message={message}";
				}
			}
			else
			{
				return StatusCode(response.StatusCode, response);

			}
			return Redirect(url);
        }

		[HttpGet("/orders/{id}", Name = nameof(GetOrderById))]
		public async Task<IActionResult> GetOrderById([FromRoute] string id) 
		{
			var response = await _orderService.GetOrderByOrderId(id);
			return StatusCode(response.StatusCode, response);
		}

		[HttpGet("/orders")]
        public async Task<IActionResult> GetAllOrder([FromQuery] bool? isBought = null,
													[FromQuery] string? searchTerm = null,
													[FromQuery] string email = "johnDoe1@gmail.com")
		{
			var response = await _orderService.GetOrderFilter(email, isBought, searchTerm, includeProps: "Customer,Tickets,Transactions");
			return StatusCode(response.StatusCode, response);
		}
	}
}