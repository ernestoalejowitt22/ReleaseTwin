using System.Security.Claims;
using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Endpoints;
using ReleaseTwin.Hosted.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// plan-tier-gating: PlanTier is the first enum exposed through the dashboard JSON contract —
// without this, System.Text.Json's default serializes it as a raw integer, inconsistent with every
// other status-like value in this API (Classification, CleanupStatus, Outcome) already being strings.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// usage-metering design.md: single DynamoDB table (real AWS in production; DynamoDB Local for local
// dev, selected via Aws:DynamoDb:ServiceUrl; the in-memory fake is the test fallback when neither AWS
// config nor a local endpoint is present — same three-tier role the old EF Core setup played).
var dynamoDbServiceUrl = builder.Configuration["Aws:DynamoDb:ServiceUrl"];
var tableName = (builder.Configuration["Aws:DynamoDb:TablePrefix"] ?? "") + "ReleaseTwinHosted";
var useRealDynamoDb = !string.IsNullOrWhiteSpace(dynamoDbServiceUrl) || !string.IsNullOrWhiteSpace(builder.Configuration["Aws:Region"]);

if (useRealDynamoDb)
{
    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
    {
        var config = new AmazonDynamoDBConfig();
        if (!string.IsNullOrWhiteSpace(dynamoDbServiceUrl))
        {
            // DynamoDB Local — no real AWS credentials needed, but the SDK still requires *some*
            // credential object to be configured; DynamoDB Local ignores their values entirely.
            config.ServiceURL = dynamoDbServiceUrl;
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration["Aws:Region"]))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["Aws:Region"]);
        }

        // Real AWS: the SDK's own default credential chain (env vars, shared config, IAM role) —
        // never a hardcoded key, same "no hardcoded credentials" rule this project applies everywhere.
        return string.IsNullOrWhiteSpace(dynamoDbServiceUrl)
            ? new AmazonDynamoDBClient(config)
            : new AmazonDynamoDBClient(new Amazon.Runtime.BasicAWSCredentials("local", "local"), config);
    });
    builder.Services.AddSingleton<IHostedTable>(sp => new DynamoDbHostedTable(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));
}
else
{
    builder.Services.AddSingleton<IHostedTable, InMemoryHostedTable>();
}

builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<ICaseReportRepository, CaseReportRepository>();
builder.Services.AddScoped<IFlagProofReportRepository, FlagProofReportRepository>();
builder.Services.AddScoped<IUsageCounterRepository, UsageCounterRepository>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<ConnectionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<GitHubConnectionFlowService>();
builder.Services.AddSingleton<IConnectionStateService, ConnectionStateService>();
builder.Services.AddHttpClient("GitHubConnection");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentOrganizationAccessor>();

// design.md: two distinct, explicitly-named auth schemes — "ClerkJwt" for web-originated (BFF)
// requests from the Next.js frontend, and ApiTokenDefaults.Scheme for the CLI's ingest uploads.
// Both are now Bearer-shaped (the old cookie scheme made them structurally impossible to confuse;
// that guarantee now lives entirely in each endpoint group explicitly restricting which scheme it
// accepts via AddAuthenticationSchemes — see IngestEndpoints.cs and the dashboard-equivalent
// endpoints below — not in the credential's shape).
//
// hosted-react-frontend: the web session is no longer a cookie established by challenging Clerk
// directly — Next.js (via @clerk/nextjs) owns the Clerk session entirely and attaches a Clerk
// session JWT when it calls this API. This API's only job is verifying that JWT against Clerk's
// public JWKS, then resolving it to a ReleaseTwin Organization/AppUser (a Clerk JWT only carries
// Clerk's own `sub` claim — it has no idea about our own rows).
var clerkDomain = builder.Configuration["Clerk:Domain"] is { Length: > 0 } domain ? domain : "not-configured.accounts.dev";

const string ClerkJwtScheme = "ClerkJwt";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = ClerkJwtScheme;
    })
    .AddJwtBearer(ClerkJwtScheme, options =>
    {
        // Clerk's OAuth Authorization Server metadata document (confirmed against Clerk's own docs)
        // lives at this well-known path and includes a jwks_uri field the OIDC-shaped configuration
        // manager ASP.NET Core uses under the hood can parse. If this turns out not to be compatible
        // once tested against a real Clerk app (tasks.md 7.1), fall back to setting options.Configuration
        // to a hand-built OpenIdConnectConfiguration pointing directly at
        // https://{clerkDomain}/.well-known/jwks.json instead of relying on discovery.
        options.MetadataAddress = $"https://{clerkDomain}/.well-known/oauth-authorization-server";
        // Verified against a real Clerk JWT (web-cypress-e2e): without this, ASP.NET Core's default
        // inbound claim mapping silently renames short claim names like "sub"/"email" to legacy
        // XML-namespaced ClaimTypes URIs, so FindFirst("sub") below never matches even though the
        // token genuinely has that claim — this keeps claim types exactly as they appear in the JWT.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = $"https://{clerkDomain}",
            ValidateIssuer = true,
            // Clerk's default session token (no custom JWT template configured) does not set an
            // audience aimed at this API specifically — verify at tasks.md 7.1 and tighten this if a
            // JWT template gets configured later.
            ValidateAudience = false,
            NameClaimType = "sub",
        };

        options.Events = new JwtBearerEvents
        {
            // The direct analog of clerk-registration's OAuthEvents.OnCreatingTicket: fires once the
            // token's signature/issuer are confirmed valid, before the request proceeds.
            OnTokenValidated = async context =>
            {
                var provisioning = context.HttpContext.RequestServices.GetRequiredService<ProvisioningService>();
                var clerkUserId = context.Principal?.FindFirst("sub")?.Value
                    ?? throw new InvalidOperationException("Clerk JWT is missing a 'sub' claim.");
                // Not guaranteed present on the default session token (needs a custom Clerk JWT
                // template to include name/email) — fall back to the Clerk user id, same pattern
                // ProvisioningService already used for a GitHub login with no display name.
                var displayName = context.Principal?.FindFirst("name")?.Value ?? clerkUserId;
                var email = context.Principal?.FindFirst("email")?.Value;

                // account-provisioning: signup requires no human approval — the first validated
                // request itself provisions the user and their organization, immediately usable.
                var user = await provisioning.GetOrCreateUserAsync(clerkUserId, displayName, email);
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                identity.AddClaim(new Claim("org_id", user.OrganizationId.ToString()));
                identity.AddClaim(new Claim("user_id", user.Id.ToString()));
            },
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(ApiTokenDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapIngestEndpoints();
app.MapDashboardEndpoints();
app.MapConnectionEndpoints();

if (app.Environment.IsDevelopment())
{
    // Dev-only: simulates the backend effect of a real Clerk signup + project creation + token
    // issuance, for local walkthroughs (tasks.md 7.1) without a registered Clerk application.
    // Never mapped outside Development.
    app.MapPost("/dev/seed", async (ProvisioningService provisioning, string login) =>
    {
        var user = await provisioning.GetOrCreateUserAsync($"dev-clerk-{login}", login, null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, $"{login}'s first project");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, project.OrganizationId);
        return Results.Ok(new { organizationId = user.OrganizationId, projectId = project.Id, token = raw });
    });
}

// usage-metering tasks.md 1.4/1.5: auto-provision the table only against DynamoDB Local (a
// developer's own throwaway local instance) — never against real AWS, where table creation is the
// documented provisioning script's job (design.md Non-Goals: no IaC, but also no surprise
// auto-provisioning against a real account).
if (!string.IsNullOrWhiteSpace(dynamoDbServiceUrl))
{
    await TableProvisioning.EnsureTableExistsAsync(app.Services.GetRequiredService<IAmazonDynamoDB>(), tableName);
}

app.Run();

public partial class Program
{
}
