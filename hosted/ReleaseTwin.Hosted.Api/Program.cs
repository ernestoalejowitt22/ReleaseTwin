using System.Security.Claims;
using Amazon.AspNetCore.DataProtection.SSM;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReleaseTwin.Hosted.Api.Analytics;
using ReleaseTwin.Hosted.Api.Auth;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Endpoints;
using ReleaseTwin.Hosted.Api.Releases;
using ReleaseTwin.Hosted.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// hosted-platform-deployment design.md: swaps Kestrel for the Lambda-aware server only when
// actually running under Lambda (detected via the standard Lambda environment variables) — a no-op
// under `dotnet run`, so local dev and `npm run e2e` are unaffected. Function URLs share HTTP API's
// payload format, so HttpApi is the correct event source even though there's no API Gateway here.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

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

// hosted-adapter-credentials design.md: Data Protection's default key ring lives on the local
// filesystem, which does not survive a redeploy or work across multiple instances — losing it makes
// every stored adapter credential permanently undecryptable. Real AWS deployments persist the key
// ring to SSM Parameter Store as a SecureString (KMS-encrypted at rest by SSM itself); local/test
// runs keep Data Protection's own default (ephemeral/filesystem) behavior, same as
// ConnectionStateService's existing tests already rely on.
if (useRealDynamoDb)
{
    builder.Services.AddDataProtection()
        .PersistKeysToAWSSystemsManager($"/{tableName}/DataProtection/Keys");
}

// plan-catalog-and-entitlements: load + validate the plan catalog once at startup. A malformed or
// incomplete plans.json throws here and fails the app rather than yielding an empty entitlement set.
builder.Services.AddSingleton(ReleaseTwin.Hosted.Api.Plans.PlanCatalog.Load());
builder.Services.AddSingleton<ReleaseTwin.Hosted.Api.Plans.IEntitlementService, ReleaseTwin.Hosted.Api.Plans.EntitlementService>();
// plan-catalog-and-entitlements: operator allowlist for the admin tier endpoint. Empty/unset ⇒
// nobody is an operator (admin surface closed by default).
builder.Services.AddSingleton<AdminOperators>();

builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<ICaseReportRepository, CaseReportRepository>();
builder.Services.AddScoped<IFlagProofReportRepository, FlagProofReportRepository>();
builder.Services.AddScoped<IUsageCounterRepository, UsageCounterRepository>();
builder.Services.AddScoped<IJourneyRepository, JourneyRepository>();
builder.Services.AddScoped<IJourneyVersionRepository, JourneyVersionRepository>();
builder.Services.AddScoped<IAdapterCredentialRepository, AdapterCredentialRepository>();
builder.Services.AddScoped<IProjectSecretRepository, ProjectSecretRepository>();
builder.Services.AddScoped<IRunEvidenceRepository, RunEvidenceRepository>();

// evidence-store: screenshot blobs live outside the single table. evidence-purge-and-blob-store:
// S3-backed when Evidence:BlobBucket is set (the only store that survives Lambda's ephemeral
// filesystem and works across concurrent instances); filesystem otherwise, for local dev.
var evidenceBlobBucket = builder.Configuration["Evidence:BlobBucket"];
if (!string.IsNullOrWhiteSpace(evidenceBlobBucket))
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Aws:Region"]))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["Aws:Region"]);
        }

        // SDK default credential chain (env / shared config / IAM role) — never a hardcoded key,
        // same rule as the DynamoDB and SNS clients.
        return new AmazonS3Client(config);
    });
    builder.Services.AddSingleton<IEvidenceBlobStore>(sp =>
        new S3EvidenceBlobStore(sp.GetRequiredService<IAmazonS3>(), evidenceBlobBucket));
}
else
{
    builder.Services.AddSingleton<IEvidenceBlobStore>(_ => new FileSystemEvidenceBlobStore(
        builder.Configuration["Evidence:BlobDirectory"]
        ?? Path.Combine(Path.GetTempPath(), "releasetwin-evidence-blobs")));
}
builder.Services.AddScoped<EvidenceIngestService>();
builder.Services.AddScoped<EvidencePurgeService>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<ConnectionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ProjectWritabilityService>();

// billing: Polar (Merchant of Record) seam. Options bind inline from the "Polar" config section
// (SSM-backed in real AWS); empty/absent ⇒ PolarOptions.IsConfigured is false and every billing
// endpoint returns "billing not configured" (safe default, tasks.md 1.2).
var polarOptions = ReleaseTwin.Hosted.Api.Billing.PolarOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(polarOptions);
builder.Services.AddHttpClient<ReleaseTwin.Hosted.Api.Billing.IPolarClient, ReleaseTwin.Hosted.Api.Billing.PolarClient>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Billing.ProcessedBillingEventRepository>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Billing.BillingEventProcessor>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Billing.BillingReconciliationService>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Analytics.TrendService>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Releases.ReleaseRollupService>();
builder.Services.AddScoped<JourneyService>();
builder.Services.AddScoped<AdapterCredentialService>();
builder.Services.AddScoped<ProjectSecretService>();
builder.Services.AddScoped<GitHubConnectionFlowService>();
builder.Services.AddSingleton<IConnectionStateService, ConnectionStateService>();
builder.Services.AddHttpClient("GitHubConnection");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentOrganizationAccessor>();

// operator-alerting: the region here is the same one the API already runs in — no separate
// configuration needed. The client is cheap to construct and only actually used by the scheduled
// digest invocation (see the RELEASETWIN_LAMBDA_TASK branch below), but registering it
// unconditionally keeps this section a single, uniform list of services rather than splitting
// registration by which Lambda function will end up using it.
builder.Services.AddSingleton<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>(_ =>
    new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient());
builder.Services.AddScoped<IOperatorAlertPublisher, SnsOperatorAlertPublisher>();
builder.Services.AddScoped<StalenessDigestService>();

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
                identity.AddClaim(new Claim("user_display_name", displayName));
            },
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(ApiTokenDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

// operator-alerting design.md: two Lambda *function* resources share this one deployment artifact
// (see hosted/terraform/alerting.tf) — the HTTP-serving function (unchanged, this env var unset)
// and a second, scheduled function that sets RELEASETWIN_LAMBDA_TASK=StalenessDigest. An
// EventBridge Scheduled Event's payload has nothing in common with the API Gateway HTTP API v2
// proxy shape AddAWSLambdaHosting(LambdaEventSource.HttpApi) expects, so it can't be routed through
// the same ASP.NET Core request pipeline below — instead this branch runs its own, independent
// Lambda Runtime API loop via LambdaBootstrapBuilder, using `app` only as a already-built DI
// container (its web-hosting pieces — Kestrel, endpoint routing, app.Run() — are never touched in
// this branch). The two functions never run concurrently, so there's no conflict over which
// runtime loop is "the" one for a given process.
if (Environment.GetEnvironmentVariable("RELEASETWIN_LAMBDA_TASK") == "StalenessDigest")
{
    await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(async (Stream _, Amazon.Lambda.Core.ILambdaContext _) =>
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StalenessDigestService>().RunAsync();
        return new MemoryStream();
    }).Build().RunAsync();
    return;
}

// evidence-store: a second scheduled Lambda task, same host pattern as the staleness digest —
// deletes evidence past each project's retention window once a day.
if (Environment.GetEnvironmentVariable("RELEASETWIN_LAMBDA_TASK") == "EvidencePurge")
{
    await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(async (Stream _, Amazon.Lambda.Core.ILambdaContext _) =>
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EvidencePurgeService>().RunAsync();
        return new MemoryStream();
    }).Build().RunAsync();
    return;
}

// billing (design.md D6): a third scheduled Lambda task — the nightly subscription-quantity
// reconciliation backstop. Same host pattern as the staleness digest and evidence purge.
if (Environment.GetEnvironmentVariable("RELEASETWIN_LAMBDA_TASK") == "BillingReconciliation")
{
    await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(async (Stream _, Amazon.Lambda.Core.ILambdaContext _) =>
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReleaseTwin.Hosted.Api.Billing.BillingReconciliationService>().RunAsync();
        return new MemoryStream();
    }).Build().RunAsync();
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// operator-alerting: no request-level log line existed at all before this — "Microsoft.AspNetCore"
// (the category the framework's own built-in request-finished log uses) is set to Warning in both
// appsettings files, so status codes never reached the logs, let alone CloudWatch. This is a
// deliberately minimal, stable-format line (not the framework's own, whose exact text isn't a
// contract) so the CloudWatch Logs metric filter added alongside it has something durable to match
// against — see the "Program" category, left at the Information default.
app.Use(async (context, next) =>
{
    await next();
    app.Logger.LogInformation(
        "http_request_completed status={StatusCode} method={Method} path={Path}",
        context.Response.StatusCode,
        context.Request.Method,
        context.Request.Path);
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapPlansEndpoints();
app.MapAdminEndpoints();
app.MapIngestEndpoints();
app.MapDashboardEndpoints();
ReleaseTwin.Hosted.Api.Billing.BillingEndpoints.MapBillingEndpoints(app);
app.MapTrendEndpoints();
app.MapReleaseEndpoints();
app.MapConnectionEndpoints();
app.MapJourneyEndpoints();
app.MapJourneyFetchEndpoints();
app.MapAdapterCredentialEndpoints();
app.MapAdapterCredentialFetchEndpoints();
app.MapProjectSecretEndpoints();
app.MapProjectSecretFetchEndpoints();
app.MapEvidenceConfigEndpoints();

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
