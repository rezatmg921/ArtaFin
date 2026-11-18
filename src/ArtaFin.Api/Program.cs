using System.Text;
using System.Threading.RateLimiting;
using ArtaFin.Api.CurrentUser;
using ArtaFin.Api.Endpoints;
using ArtaFin.Api.Errors;
using ArtaFin.Api.Security;
using ArtaFin.Application;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Bind and validate JWT settings through DI so host-specific configuration
// providers (including WebApplicationFactory overrides) are applied before validation.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.Issuer) &&
        !string.IsNullOrWhiteSpace(options.Audience) &&
        Encoding.UTF8.GetByteCount(options.SigningKey) >= 32 &&
        options.AccessTokenMinutes is >= 5 and <= 60,
        "Authentication:Jwt must define Issuer, Audience, a SigningKey of at least 32 UTF-8 bytes, and AccessTokenMinutes between 5 and 60.")
    .ValidateOnStart();

builder.Services.AddSingleton<JwtOptions>(provider =>
    provider.GetRequiredService<IOptions<JwtOptions>>().Value);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter the JWT access token. Swagger UI will send it as a Bearer token."
            }
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();

        if (requiresAuthorization)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }

        // Identify these operations by their stable endpoint names rather than
        // matching ApiDescription.RelativePath, which can vary with route formatting.
        var endpointName = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .Select(metadata => metadata.EndpointName)
            .FirstOrDefault();

        var supportsIdempotencyKey =
            context.Description.HttpMethod == "POST" &&
            endpointName is "CreateExpense" or "CreateIncome" or "CreateTransfer";

        if (supportsIdempotencyKey)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional key used to safely retry this financial operation. Reusing the same key with the same request returns the original result; using it with different request data returns 409 Conflict.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MaxLength = 128
                }
            });
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IAccessTokenGenerator>(provider =>
    provider.GetRequiredService<JwtTokenService>());

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ArtaFin API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "ArtaFin API"
}))
.WithName("Health")
.AllowAnonymous();

app.MapAuthenticationEndpoints();
app.MapFinancialBookEndpoints();
app.MapAccountEndpoints();
app.MapAccountListEndpoints();
app.MapAccountLedgerEndpoints();
app.MapAccountBalancesEndpoints();
app.MapTrialBalanceEndpoints();
app.MapIncomeStatementEndpoints();
app.MapBalanceSheetEndpoints();
app.MapCashFlowStatementEndpoints();
app.MapCashFlowAllocationEndpoints();
app.MapCashFlowNonCashDisclosureEndpoints();
app.MapFinancialBookMemberEndpoints();
app.MapJournalEntryEndpoints();
app.MapJournalEntryListEndpoints();
app.MapExpenseEndpoints();
app.MapIncomeEndpoints();
app.MapTransferEndpoints();

app.Run();

public partial class Program
{
}
