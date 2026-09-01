using System.Security.Claims;
using Amazon.AspNetCore.DataProtection.SSM;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReleaseTwin.Hosted.Api;
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

// add-feature-flag-seam: the feature-flag seam (design D1/D3). Registry + provider are singletons;
// the provider is a plain in-process static resolver — no streaming socket, no background thread, so
// it is safe across Lambda freeze/thaw. Adopting LaunchDarkly later = swap the FeatureProvider
// registration here for LaunchDarkly.OpenFeature.ServerProvider; nothing calling IFlagService moves.
builder.Services.AddSingleton(ReleaseTwin.Hosted.Api.Flags.FlagRegistry.Load());
builder.Services.AddSingleton<OpenFeature.FeatureProvider>(sp =>
    new ReleaseTwin.Hosted.Api.Flags.StaticFlagProvider(
        sp.GetRequiredService<ReleaseTwin.Hosted.Api.Flags.FlagRegistry>(),
        sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Flags.IFlagContextFactory, ReleaseTwin.Hosted.Api.Flags.FlagContextFactory>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Flags.IFlagService, ReleaseTwin.Hosted.Api.Flags.FlagService>();
builder.Services.AddSingleton<ReleaseTwin.Hosted.Api.Plans.IEntitlementService, ReleaseTwin.Hosted.Api.Plans.EntitlementService>();
// plan-catalog-and-entitlements: operator allowlist for the admin tier endpoint. Empty/unset ⇒
// nobody is an operator (admin surface closed by default).
builder.Services.AddSingleton<AdminOperators>();

builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMembershipRepository, MembershipRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<INotificationTargetRepository, NotificationTargetRepository>();
builder.Services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
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
    // data-export: the built ZIP is put in the same bucket under exports/ and downloaded via a
    // presigned URL, so it never streams back through the Lambda (design D2).
    builder.Services.AddSingleton<ReleaseTwin.Hosted.Api.Services.DataExport.IExportArchiveStore>(sp =>
        new ReleaseTwin.Hosted.Api.Services.DataExport.S3ExportArchiveStore(sp.GetRequiredService<IAmazonS3>(), evidenceBlobBucket));
}
else
{
    builder.Services.AddSingleton<IEvidenceBlobStore>(_ => new FileSystemEvidenceBlobStore(
        builder.Configuration["Evidence:BlobDirectory"]
        ?? Path.Combine(Path.GetTempPath(), "releasetwin-evidence-blobs")));
    builder.Services.AddSingleton<ReleaseTwin.Hosted.Api.Services.DataExport.IExportArchiveStore,
        ReleaseTwin.Hosted.Api.Services.DataExport.NullExportArchiveStore>();
}
builder.Services.AddScoped<EvidenceIngestService>();
builder.Services.AddScoped<EvidencePurgeService>();
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Services.DataExport.ExportArchiveBuilder>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<OrganizationMembersService>();
// company-and-domain-launch: real invite email via SES v2 when Notifications:FromAddress is set;
// structured-log fallback otherwise (local dev, tests, any deploy not yet wired to SES). The accept
// link is always in the invite endpoint's response, so the flow works either way.
var notificationsFromAddress = builder.Configuration["Notifications:FromAddress"];
if (!string.IsNullOrWhiteSpace(notificationsFromAddress))
{
    builder.Services.AddSingleton<Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2>(_ =>
    {
        var config = new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Config();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Aws:Region"]))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["Aws:Region"]);
        }

        // SDK default credential chain (env / shared config / IAM role) — never a hardcoded key,
        // same rule as the DynamoDB, SNS, and S3 clients.
        return new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client(config);
    });
    builder.Services.AddScoped<IInvitationEmailSender>(sp => new SesInvitationEmailSender(
        sp.GetRequiredService<Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2>(),
        notificationsFromAddress,
        sp.GetRequiredService<ILogger<SesInvitationEmailSender>>()));
}
else
{
    builder.Services.AddScoped<IInvitationEmailSender, LoggingInvitationEmailSender>();
}

// run-notifications (design D6): outbound delivery is off the ingest path. Ingest enqueues onto SQS
// (or a no-op when no queue is configured — tests, local); a second Lambda
// (RELEASETWIN_LAMBDA_TASK=NotificationDispatch) drains it. The dispatch HttpClient follows no
// redirects and times out fast so a hostile or slow target can't tie it up.
builder.Services.AddScoped<NotificationDispatchService>();
builder.Services.AddScoped<EvidenceSharingService>();
builder.Services.AddHttpClient(NotificationDispatchService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5))
    // security-hardening-pre-pilot D5: SocketsHttpHandler so ConnectCallback can pin the connection to
    // the exact IP OutboundUrlValidator approved (no independent re-resolution). TLS SNI/cert
    // validation still uses the request's original hostname.
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = NotificationDispatchService.ConnectToPinnedAddressAsync,
    });
// run-notifications: the SSRF check needs to resolve a customer-supplied host. Injected so tests are
// deterministic and offline.
builder.Services.AddSingleton<Func<string, System.Net.IPAddress[]>>(_ => System.Net.Dns.GetHostAddresses);

var notificationsQueueUrl = builder.Configuration["Notifications:QueueUrl"];
if (!string.IsNullOrWhiteSpace(notificationsQueueUrl))
{
    builder.Services.AddSingleton<Amazon.SQS.IAmazonSQS>(_ =>
    {
        var config = new Amazon.SQS.AmazonSQSConfig();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Aws:Region"]))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["Aws:Region"]);
        }
        return new Amazon.SQS.AmazonSQSClient(config);
    });
    builder.Services.AddSingleton<INotificationQueue>(sp => new SqsNotificationQueue(
        sp.GetRequiredService<Amazon.SQS.IAmazonSQS>(),
        notificationsQueueUrl!,
        sp.GetRequiredService<ILogger<SqsNotificationQueue>>()));
}
else
{
    builder.Services.AddSingleton<INotificationQueue, NullNotificationQueue>();
}
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
// billing-metrics-digest: the nightly reconciliation Lambda also composes an operator digest of
// billing-integrity + abuse signals. Thresholds bind inline from the "BillingMetrics" section.
builder.Services.AddSingleton(ReleaseTwin.Hosted.Api.Billing.BillingMetricsOptions.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<ReleaseTwin.Hosted.Api.Billing.BillingMetricsCollector>();
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
builder.Services.AddScoped<IOrganizationAccessGuard>(sp => sp.GetRequiredService<CurrentOrganizationAccessor>());

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

// security-hardening-pre-pilot D1: Clerk's default session token carries no API-specific `aud`.
// Audience validation stays OFF until a Clerk JWT template that sets an `aud` for this API exists
// and its value is supplied here (CLERK_AUDIENCE repo var → Clerk__Audience). Presence of the config
// value — not a feature flag — is the switch, because this is a deploy-ordering concern (template
// before enforcement) that disappears once the template is live.
var clerkAudience = builder.Configuration["Clerk:Audience"];
var validateClerkAudience = !string.IsNullOrWhiteSpace(clerkAudience);

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
            // security-hardening-pre-pilot D1: on only when Clerk:Audience is configured (a Clerk JWT
            // template that sets this API's `aud` must exist first). Unset ⇒ back-compat: issuer +
            // signature + expiry only, as before.
            ValidateAudience = validateClerkAudience,
            ValidAudience = clerkAudience,
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
                // security-hardening-pre-pilot D1/D2: when the Clerk JWT template supplies `email` it
                // is the account's primary, provider-verified address; downstream email-match checks
                // (invitation acceptance) rely on that. Absent claim ⇒ null (never ""), and a null
                // email is treated as a non-match by those checks, never as a wildcard.
                var email = context.Principal?.FindFirst("email")?.Value;

                // account-provisioning: signup requires no human approval — the first validated
                // request itself provisions the user and their organization, immediately usable.
                // org-membership (design D1a): the /invitations/<token> page forwards the token so
                // provisioning can skip minting a throwaway org for someone joining an existing one.
                var pendingInvite = context.HttpContext.Request.Headers["X-Invite-Token"].ToString();
                var user = await provisioning.GetOrCreateUserAsync(
                    clerkUserId, displayName, email,
                    string.IsNullOrEmpty(pendingInvite) ? null : pendingInvite);
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                identity.AddClaim(new Claim("user_id", user.Id.ToString()));
                identity.AddClaim(new Claim("user_display_name", displayName));

                // org-membership: resolve the active organization + the caller's role in it, once per
                // request. The BFF may name a chosen org via the X-Org-Id header; it is honoured only
                // when the caller is actually a member of it, otherwise their default org is used.
                var membershipService = context.HttpContext.RequestServices.GetRequiredService<MembershipService>();
                var memberships = await membershipService.GetMembershipsAsync(user);
                if (memberships.Count > 0)
                {
                    ReleaseTwin.Hosted.Api.Data.Entities.Membership? active = null;
                    if (Guid.TryParse(context.HttpContext.Request.Headers[CurrentOrganizationAccessor.ActiveOrgHeader], out var requested))
                    {
                        active = memberships.FirstOrDefault(m => m.OrganizationId == requested);
                    }
                    active ??= memberships
                        .OrderBy(m => m.CreatedAt)
                        .ThenBy(m => m.OrganizationId)
                        .First();

                    identity.AddClaim(new Claim("org_id", active.OrganizationId.ToString()));
                    identity.AddClaim(new Claim("org_role", active.Role.ToString()));
                }
                // No memberships and no legacy org (e.g. signed up via an invite that has not been
                // accepted yet): no org_id claim — org-scoped endpoints return 403 until they join.
            },
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(ApiTokenDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

// security-hardening-pre-pilot D7: per-caller ceilings on the ingest / share-link / billing-webhook
// surface. See RateLimiting.cs for the policies, sizing, and the RateLimiting:Enabled kill-switch.
builder.Services.AddReleaseTwinRateLimiting(builder.Configuration);

var app = builder.Build();

// security-hardening-pre-pilot D1: make the audience-validation mode visible in the logs so a
// misconfigured Clerk template (or a not-yet-created one) is obvious rather than silent.
app.Logger.LogInformation(
    validateClerkAudience
        ? "clerk_jwt_audience_validation enabled audience={Audience}"
        : "clerk_jwt_audience_validation disabled (Clerk:Audience not set)",
    clerkAudience);

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

// run-notifications (design D6): a fourth Lambda task, but SQS-triggered rather than scheduled — the
// event is a batch of queued RunNotification messages. Same shared-artifact pattern; parsed here
// with a minimal SQS shape rather than taking a dependency on Amazon.Lambda.SQSEvents. Returns the
// partial-batch-failure response so a message that fails at the message level (bad JSON, org load
// error) is retried by SQS and lands in the DLQ after the redrive limit, without re-notifying
// targets that already succeeded.
if (Environment.GetEnvironmentVariable("RELEASETWIN_LAMBDA_TASK") == "NotificationDispatch")
{
    var sqsJson = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(async (Stream input, Amazon.Lambda.Core.ILambdaContext _) =>
    {
        var batch = await System.Text.Json.JsonSerializer.DeserializeAsync<ReleaseTwin.Hosted.Api.Services.SqsBatch>(input, sqsJson);
        var failures = new List<object>();
        foreach (var record in batch?.Records ?? [])
        {
            try
            {
                var notification = System.Text.Json.JsonSerializer.Deserialize<ReleaseTwin.Hosted.Api.Services.RunNotification>(record.Body ?? "", sqsJson)
                    ?? throw new InvalidOperationException("empty message body");
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationDispatchService>().DispatchAsync(notification);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "notification_dispatch_failed messageId={MessageId}", record.MessageId);
                failures.Add(new { itemIdentifier = record.MessageId });
            }
        }
        return new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { batchItemFailures = failures }));
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
    try
    {
        await next();
    }
    catch (ReleaseTwin.Hosted.Api.Services.ForbiddenException ex) when (!context.Response.HasStarted)
    {
        // org-membership: the caller has no membership in the active organization, or their role does
        // not permit the operation. One place, so no endpoint needs its own try/catch.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "forbidden", detail = ex.Message });
    }
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

// security-hardening-pre-pilot D7: after auth so the ingest policy can partition by the token-hash
// claim; before endpoints so a rejected billing-webhook request never reaches signature verification.
app.UseRateLimiter();

app.MapRazorPages();
app.MapPlansEndpoints();
app.MapAdminEndpoints();
app.MapIngestEndpoints();
app.MapDashboardEndpoints();
app.MapMembershipEndpoints();
app.MapNotificationEndpoints();
app.MapShareLinkEndpoints();
app.MapExportEndpoints();
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
