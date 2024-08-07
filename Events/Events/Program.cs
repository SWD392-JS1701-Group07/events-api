using Events.API.Controllers;
using Events.Business;
using Events.Business.Services;
using Events.Business.Services.Interfaces;
using Events.Models.Models;
using Events.Data.Repositories;
using Events.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Events.Utils.Helpers;
using Events.Utils;
using static Events.Utils.Helpers.CloudinaryHelper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<EventsDbContext>(
	option => option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64; 
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApiVersioning(options =>
{
	options.ReportApiVersions = true;
	options.DefaultApiVersion = new ApiVersion(1, 0);
	options.Conventions.Controller<EventController>().HasApiVersion(new Microsoft.AspNetCore.Mvc.ApiVersion(2, 0));
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Version = "V1.0",
		Title = "API V1",
	});

	c.SwaggerDoc("v2", new OpenApiInfo
	{
		Version = "V2.0",
		Title = "API V2",
	});

	c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Description = "JWT Authorization header using the Bearer scheme",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer"
	});

	c.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "Bearer"}
			},
			Array.Empty<string>()
		}
	});

	// Set the comments path for the Swagger JSON and UI.
	var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
	c.IncludeXmlComments(xmlPath);
});

//CLoudinary
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));

//Bind Vnpay into VnpaySettings
builder.Services.Configure<VnpaySettings>(builder.Configuration.GetSection("Vnpay"));

//Authentication
builder.Services.AddAuthentication(options =>
	options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme).
	AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = false,
			//      ValidIssuer = builder.Configuration["JWT:Issuer"],
			ValidateAudience = false,
			//      ValidAudience = builder.Configuration["JWT:Audience"],
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			RoleClaimType = ClaimTypes.Role,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("1c4890495b93b9e71fee12bf1880242771ad287f814d9553b120de5b82428b0b"))
		};
		options.Events = new JwtBearerEvents
		{
			OnTokenValidated = context =>
			{
				var token = context.SecurityToken as JwtSecurityToken;
				if (token != null && !JWTGenerator.IsTokenValid(token.RawData))
				{
					context.Fail("Token is invalid");
				}
				return Task.CompletedTask;
			}
		};
	});


builder.Services.AddAutoMapper(typeof(Events.Business.Mapping.MappingProfiles));

builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEventScheduleRepository, EventScheduleRepository>();
builder.Services.AddScoped<EmailHelper>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICollaboratorRepository, CollaboratorRepository>();
builder.Services.AddScoped<ICollaboratorService, CollaboratorService>();
builder.Services.AddScoped<ISponsorRepository, SponsorRepository>();
builder.Services.AddScoped<ISponsorService, SponsorService>();
builder.Services.AddScoped<ISponsorshipRepository, SponsorshipRepository>();
builder.Services.AddScoped<ISponsorshipService, SponsorshipService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<VnPayLibrary>();
builder.Services.AddScoped<IVNPayPaymentService, VNPayPaymentService>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<CloudinaryHelper>();
builder.Services.AddScoped<QrHelper>();



builder.Services.AddCors(options => options.AddDefaultPolicy(policyBuilder =>
{
	var frontendUrl = builder.Configuration.GetValue<string>("AppFrontend");
	if (string.IsNullOrEmpty(frontendUrl))
	{
		throw new ArgumentNullException("AppFrontend", "Frontend URL cannot be null or empty.");
	}
	policyBuilder.WithOrigins(frontendUrl,"http://localhost:5173")
					 .AllowAnyMethod()
					 .AllowAnyHeader()
					 .AllowCredentials();
}));
		


// Register the background service
//builder.Services.AddHostedService<EventStatusUpdateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
{
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
		c.SwaggerEndpoint("/swagger/v2/swagger.json", "API V2");
		c.RoutePrefix = "swagger";
	});
	app.UseHttpsRedirection();

	string imagePath = Path.Combine(builder.Environment.ContentRootPath, "Images");
	if (!Directory.Exists(imagePath))
	{
		Directory.CreateDirectory(imagePath);
	}
	app.UseStaticFiles(new StaticFileOptions
	{
		FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "Images")),
		RequestPath = "/Images"
	});

	app.UseAuthentication();
	app.UseAuthorization();

	app.MapControllers();

	app.UseCors();

	app.Run();
}
