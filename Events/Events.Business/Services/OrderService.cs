﻿using AutoMapper;
using Events.Business.Services.Interfaces;
using Events.Data.Repositories;
using Events.Data.Repositories.Interfaces;
using Events.Models.DTOs;
using Events.Models.DTOs.Request;
using Events.Models.DTOs.Response;
using Events.Models.Models;
using Events.Utils;
using Events.Utils.Helpers;
using MailKit.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Events.Utils.Enums;

namespace Events.Business.Services
{
	public class OrderService : IOrderService
	{
		private readonly IOrderRepository _orderRepository;
		private readonly ITicketRepository _ticketRepository;
		private readonly ITicketService _ticketService;
		private readonly IEventService _eventService;
		private readonly IVNPayPaymentService _vnPayPaymentService;
		private readonly IMapper _mapper;
		private readonly IEventRepository _eventRepository;
		private readonly EventsDbContext _dbContext;
		private readonly ICustomerRepository _customerRepository;
		private readonly ITransactionRepository _transactionRepository;
		private readonly IAccountRepository _accountRepository;
		private readonly EmailHelper _emailHelper;
		private readonly QrHelper _qrHelper;

		public OrderService(IOrderRepository orderRepository, ITicketRepository ticketRepository, ITicketService ticketService, IEventService eventService, IVNPayPaymentService vnPayPaymentService, IMapper mapper, IEventRepository eventRepository, EventsDbContext dbContext, ICustomerRepository customerRepository, ITransactionRepository transactionRepository, IAccountRepository accountRepository, EmailHelper emailHelper, QrHelper qrHelper)
		{
			_orderRepository=orderRepository;
			_ticketRepository=ticketRepository;
			_ticketService=ticketService;
			_eventService=eventService;
			_vnPayPaymentService=vnPayPaymentService;
			_mapper=mapper;
			_eventRepository=eventRepository;
			_dbContext=dbContext;
			_customerRepository=customerRepository;
			_transactionRepository=transactionRepository;
			_accountRepository=accountRepository;
			_emailHelper=emailHelper;
			_qrHelper=qrHelper;
		}

		public async Task<BaseResponse> CreateOrderAndPayment(CreateOrderRequest request, HttpContext context)
		{
			try
			{
				var validationResponse = await ValidateRequest(request);
				if (!validationResponse.IsSuccess)
				{
					return validationResponse;
				}

				//Get total price from db
				var totalPriceFromDb = await _eventService.GetTotalPriceTicketOfEvent(request.Tickets);
				if (request.TotalAmount != totalPriceFromDb)
				{
					return new BaseResponse
					{
						StatusCode = StatusCodes.Status400BadRequest,
						Message = "Total Amount is not correct!",
						IsSuccess = false
					};
				}
				using (var transaction = _dbContext.Database.BeginTransaction())
				{
					try
					{
						// Check if customer already exists
						var customerExist = await _customerRepository.CheckCustomerExist(request.Email, request.PhoneNumber);
						Customer customer;
						if (customerExist)
						{
							customer = await _customerRepository.GetCustomerByEmail(request.Email);
						}
						else
						{
							customer = new Customer
							{
								Name = request.Name,
								PhoneNumber = request.PhoneNumber,
								Email = request.Email,
							};
							await _customerRepository.CreateCustomer(customer);
						}

						// create order record
						var orderEntity = _mapper.Map<Order>(request);
						orderEntity.Id = Guid.NewGuid().ToString();
						orderEntity.OrderDate = DateTime.Now;
						orderEntity.CustomerId = customer.Id;
						orderEntity.Email = customer.Email;
						orderEntity.PhoneNumber = customer.PhoneNumber;
						orderEntity.OrderStatus = request.TotalAmount == 0 ? OrderStatus.Success : OrderStatus.Failed;
						await _orderRepository.CreateOrders(orderEntity);
						// Loop for list of ticket in request
						foreach (var ticketDetail in request.Tickets)
						{
							var ticketEntity = _mapper.Map<Ticket>(ticketDetail);
							ticketEntity.Id = Guid.NewGuid().ToString();
							ticketEntity.IsCheckIn = IsCheckin.No;
							ticketEntity.OrdersId = orderEntity.Id;
							var ticketInfo = _mapper.Map<QrCodeDTO>(ticketEntity);
							ticketInfo.Price = ticketEntity.Price;
							ticketInfo.EventName = await _eventService.GetEventNameByIdAsync(ticketDetail.EventId);
							ticketEntity.Qrcode = await _qrHelper.GenerateQr(JsonSerializer.Serialize(ticketInfo), fileName: $"{ticketInfo.TicketId}.png");
							await _ticketRepository.CreateTicket(ticketEntity);
						}

						// Create transaction record
						if (request.TotalAmount > 0)
						{
							var transactionEntry = new Transaction
							{
								Id = Guid.NewGuid().ToString(),
								OrderId = orderEntity.Id,
								Amount = request.TotalAmount,
								PaymentStatus = PaymentStatus.Failed,
							};
							await _transactionRepository.AddTransaction(transactionEntry);
						}

						await _dbContext.SaveChangesAsync();
						await transaction.CommitAsync();

						if (request.TotalAmount > 0)
						{
							var payUrl = _vnPayPaymentService.CreatePayment(request.TotalAmount.ToString(), $"Payment for order #{orderEntity.Id}", context);
							return new BaseResponse
							{
								StatusCode = StatusCodes.Status200OK,
								IsSuccess = true,
								Message = "Order created. Proceed to payment.",
								Data = payUrl
							};
						}

						var orderFromDb = await _orderRepository.GetOrderByIdAsync(orderEntity.Id);
						var listTicket = orderFromDb.Tickets.ToList();
						if (listTicket != null)
						{
							foreach (var ticket in listTicket)
							{
								_emailHelper.SendEmailToBuyTicketSuccess(ticket.Email, ticket.Qrcode);
							}
						}
						return new BaseResponse { StatusCode = StatusCodes.Status200OK, IsSuccess = true, Message = "Order created successfully. No payment needed!!" };

					}
					catch (Exception)
					{
						await transaction.RollbackAsync();
						throw;
					}
				}

			}
			catch (Exception ex)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status500InternalServerError,
					Message = ex.Message,
					IsSuccess = false
				};
			}
		}

		public async Task<BaseResponse> GetOrderByOrderId(string id)
		{
			try
			{
				var orderFromDb = await _orderRepository.GetOrderByIdAsync(id);

				return new BaseResponse
				{
					StatusCode = StatusCodes.Status200OK,
					Message = "Get Order detail successfully",
					Data = _mapper.Map<OrderDTO>(orderFromDb),
				};
			}
			catch (KeyNotFoundException ex)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status404NotFound,
					Message = ex.Message,
					IsSuccess = false,
				};
			}
			catch (Exception ex)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status500InternalServerError,
					Message = ex.Message,
					IsSuccess = false,
				};
			}
		}

		public async Task<BaseResponse> GetOrderFilter(string email = "johnDoe1@gmail.com", bool? isBought = null, string? searchTern = null, string? includeProps = null)
		{
			try
			{
				//var accountFromDb = await _accountRepository.GetAccountById(accountId)??throw new KeyNotFoundException("Not found account");
				var accountFromDb = await _accountRepository.GetAccountByEmail(email);
				if (accountFromDb == null)
				{
					return new BaseResponse
					{
						StatusCode = StatusCodes.Status404NotFound,
						IsSuccess = false,
						Message = "Account not found!!"
					};
				}
				int? customerId = null;
				if (accountFromDb.RoleId == 2)
				{
					customerId = (await _customerRepository.GetCustomerByEmail(email))?.Id;
				}

				var orderDto = await _orderRepository.GetAllOrdersFitlter(accountFromDb, customerId, isBought, searchTern, includeProps);
				if (!orderDto.Any())
				{
					return new BaseResponse
					{
						StatusCode = StatusCodes.Status404NotFound,
						IsSuccess = false,
						Message = "Not found any order"
					};
				}
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status200OK,
					IsSuccess = true,
					Data = _mapper.Map<IEnumerable<SimpleOrderDTO>>
							(orderDto)
				};

			}
			catch (KeyNotFoundException ex)
			{
				return new BaseResponse
				{
					IsSuccess = false,
					StatusCode = StatusCodes.Status404NotFound,
					Message = ex.Message,
				};
			}
			catch (Exception ex)
			{
				return new BaseResponse
				{
					IsSuccess = false,
					StatusCode= StatusCodes.Status500InternalServerError,
					Message = ex.Message,
				};
			}
		}

		public async Task<BaseResponse> HandlePaymentCallback(IQueryCollection query)
		{
			try
			{
				var response = _vnPayPaymentService.PaymentExecute(query);
				if (response.Success)
				{
					string orderId = "";
					string[] parts = response.OrderDescription.Split('#');
					if (parts.Length > 1)
					{
						orderId = parts[1].Trim();
					}
					response.OrderId = orderId;
					var orderFromDb = await _orderRepository.GetOrderByIdAsync(orderId);
					var transaction = await _transactionRepository.GetTransactionFilter(t => t.OrderId == orderId && t.PaymentStatus == PaymentStatus.Failed,
																									t => t.TransactionDate);
					if (orderFromDb != null)
					{
						if (response.VnPayResponseCode == "00")
						{
							using (var transactionDb = _dbContext.Database.BeginTransaction())
								try
								{
									{
										if (transaction != null)
										{
											_mapper.Map(response, transaction);
											transaction.ResponseMessage = "Payment success!";
											transaction.PaymentStatus = PaymentStatus.Success;
											await _transactionRepository.UpdateTransactionAsync(transaction);
										}

										orderFromDb.OrderStatus = OrderStatus.Success;
										await _orderRepository.UpdateOrderStatusAsync(orderFromDb);

										var responseTicketBought = await _ticketService.GetTicketFilter(isBought: true, orderId: orderFromDb.Id, includeProps: "Orders");
										if (responseTicketBought.IsSuccess)
										{
											var ticketBought = responseTicketBought.Data as IEnumerable<SimpleTicketDTO>;
											if (ticketBought!.Any())
											{
												var eventTicketQuantities = ticketBought!
													.GroupBy(ticket => ticket.EventId)
													.ToDictionary(group => group.Key, group => group.Count());
												bool isSuccess = await _eventService.UpdateTicketQuantity(eventTicketQuantities);
												if (!isSuccess)
												{
													return new BaseResponse
													{
														StatusCode = StatusCodes.Status500InternalServerError,
														Message = "Not enough ticket !!",
														IsSuccess = false
													};
												}
											}
										}
										await transactionDb.CommitAsync();

										//Send Email
										var listTicket = orderFromDb.Tickets.ToList();
										if (listTicket != null)
										{
											foreach (var ticket in listTicket)
											{
												_emailHelper.SendEmailToBuyTicketSuccess(ticket.Email, ticket.Qrcode);
											}
										}

										return new BaseResponse
										{
											StatusCode = StatusCodes.Status200OK,
											Message = "Payment callback handled successfully!!",
											IsSuccess = true,
											Data = response
										};
									}
								}
								catch (Exception)
								{
									await transactionDb.RollbackAsync();
									throw;
								}

						}
						else
						{
							if (transaction != null)
							{
								_mapper.Map(response, transaction);
								transaction.ResponseMessage = "Payment failed!";
								await _transactionRepository.UpdateTransactionAsync(transaction);
							}

							return new BaseResponse
							{
								StatusCode = StatusCodes.Status400BadRequest,
								Message = "Payment failed",
								IsSuccess = false,
								Data = response
							};
						}
					}
					else
					{
						return new BaseResponse
						{
							StatusCode = StatusCodes.Status404NotFound,
							Message = "Order not found",
							IsSuccess = false
						};
					}
				}
				else
				{
					return new BaseResponse
					{
						StatusCode = StatusCodes.Status400BadRequest,
						Message = "Payment execution failed",
						IsSuccess = false,
						Data = response
					};
				}
			}
			catch (KeyNotFoundException e)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status404NotFound,
					Message = e.Message,
					IsSuccess = false
				};
			}
			catch (Exception e)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status500InternalServerError,
					Message = e.Message,
					IsSuccess = false
				};
			}
		}

		private async Task<BaseResponse> ValidateRequest(CreateOrderRequest request)
		{
			var errors = new Dictionary<string, List<string>>();
			// Group tickets by EventId and count the number of tickets for each event
			var eventTicketCounts = request.Tickets
				.GroupBy(ticket => ticket.EventId)
				.ToDictionary(group => group.Key, group => group.Count());
			foreach (var ticketDetail in request.Tickets)
			{
				var eventEntity = await _eventRepository.GetEventById(ticketDetail.EventId);
				string eventName;
				double eventPrice = 0;
				if (eventEntity != null)
				{
					eventName = eventEntity.Name;
					eventPrice = eventEntity!.Price;
					// check ticket exist
					var ticketExists = await _ticketRepository.CheckTicketExist(ticketDetail.Email, ticketDetail.PhoneNumber, ticketDetail.EventId);
					if (ticketExists)
					{
						var key = $"{ticketDetail.Email}-{ticketDetail.PhoneNumber}";
						if (!errors.ContainsKey(key))
						{
							errors.Add(key, new List<string>());
						}
						errors[key].Add($"A ticket with either email '{ticketDetail.Email}' or phone number '{ticketDetail.PhoneNumber}' already exists for event '{eventName}'");
					}

					//check event price
					var keyTicketPrice = $"{ticketDetail.Email}-{ticketDetail.PhoneNumber}";
					if (ticketDetail.Price != eventPrice)
					{
						if (!errors.ContainsKey(keyTicketPrice))
						{
							errors.Add(keyTicketPrice, new List<string>());
						}
						errors[keyTicketPrice].Add($"A ticket with email '{ticketDetail.Email}' and phone number '{ticketDetail.PhoneNumber}' not correct price");
					}
				}

				//check event exist
				else
				{
					var key = $"{ticketDetail.EventId}";
					if (!errors.ContainsKey(key))
					{
						errors.Add(key, new List<string>());
					}
					errors[key].Add($"Not found Event with event id '{ticketDetail.EventId}'");
				}
			}

			// Check if the number of requested tickets exceeds available tickets
			foreach (var eventId in eventTicketCounts.Keys)
			{
				var eventEntity = await _eventRepository.GetEventById(eventId);
				if (eventEntity != null && eventEntity.Remaining < eventTicketCounts[eventId])
				{
					var keyTicketAvailability = $"{eventId}-availability";
					if (!errors.ContainsKey(keyTicketAvailability))
					{
						errors.Add(keyTicketAvailability, new List<string>());
					}
					errors[keyTicketAvailability].Add($"Not enough tickets available for event '{eventEntity.Name}'. Available: {eventEntity.Remaining}, Requested: {eventTicketCounts[eventId]}");
				}
			}

			if (errors.Count!=0)
			{
				return new BaseResponse
				{
					StatusCode = StatusCodes.Status400BadRequest,
					Message = "There are some errors in the request!!",
					IsSuccess = false,
					Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
				};
			}
			return new BaseResponse { IsSuccess = true };
		}
	}
}

