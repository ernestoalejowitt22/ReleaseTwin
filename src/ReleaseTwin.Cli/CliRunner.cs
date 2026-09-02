using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Adapters.AzureDevOps;
using ReleaseTwin.Adapters.Http;
using ReleaseTwin.Adapters.LaunchDarkly;
using ReleaseTwin.Adapters.Ui;
using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Cli.Evidence;
using ReleaseTwin.Cli.Upload;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli;

/// <summary>
/// design.md D5: composes the credential-free HTTP adapter unconditionally, and the Azure DevOps /
/// LaunchDarkly adapters only when all of their respective environment variables are present.
/// Partial configuration of either is treated as a mistake (clear startup error), not a silent skip.
/// </summary>
public sealed class CliRunner
{
    private static readonly string[] AzureDevOpsEnvironmentVariables =
    {
        "AZDO_ORG", "AZDO_PROJECT", "AZDO_PAT", "AZDO_AREA_PATH", "AZDO_VARIABLE_GROUP_ID",
    };

    private static readonly string[] LaunchDarklyEnvironmentVariables =
    {
        "LAUNCHDARKLY_API_TOKEN", "LAUNCHDARKLY_PROJECT_KEY", "LAUNCHDARKLY_ENVIRONMENT_KEY",
    };

    public Task<int> RunAsync(
        string casesDirectory,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken = default,
        HttpMessageHandler? azureDevOpsHandlerForTesting = null,
        HttpMessageHandler? httpAdapterHandlerForTesting = null,
        HttpMessageHandler? uploadHandlerForTesting = null,
        HttpMessageHandler? launchDarklyHandlerForTesting = null,
        HttpMessageHandler? adapterCredentialsHandlerForTesting = null,
        HttpMessageHandler? projectSecretsHandlerForTesting = null,
        HttpMessageHandler? evidenceConfigHandlerForTesting = null) =>
        RunWithConfigAsync(
            () => ReleaseTwinConfig.LoadFor(casesDirectory),
            environment, output, cancellationToken,
            LoadLocalCasesAsync(casesDirectory),
            azureDevOpsHandlerForTesting, httpAdapterHandlerForTesting, uploadHandlerForTesting, launchDarklyHandlerForTesting, adapterCredentialsHandlerForTesting, projectSecretsHandlerForTesting, evidenceConfigHandlerForTesting);

    /// <summary>
    /// hosted-journeys: runs one journey fetched from the hosted API at a specific, pinned version —
    /// through the exact same case-loading/pipeline machinery as a locally-loaded case, only the
    /// YAML's source differs. `RELEASETWIN_API_TOKEN` is required here (unlike the local-directory
    /// path, where it only enables optional report uploads) since it's how the fetch itself
    /// authenticates and how the project scoping the spec requires is enforced.
    /// </summary>
    public Task<int> RunJourneyAsync(
        Guid journeyId,
        int version,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken = default,
        HttpMessageHandler? azureDevOpsHandlerForTesting = null,
        HttpMessageHandler? httpAdapterHandlerForTesting = null,
        HttpMessageHandler? uploadHandlerForTesting = null,
        HttpMessageHandler? launchDarklyHandlerForTesting = null,
        HttpMessageHandler? journeyFetchHandlerForTesting = null,
        HttpMessageHandler? adapterCredentialsHandlerForTesting = null,
        HttpMessageHandler? projectSecretsHandlerForTesting = null,
        HttpMessageHandler? evidenceConfigHandlerForTesting = null) =>
        RunWithConfigAsync(
            () => ReleaseTwinConfig.LoadFor(Directory.GetCurrentDirectory()),
            environment, output, cancellationToken,
            LoadHostedJourneyAsync(journeyId, version, environment, journeyFetchHandlerForTesting),
            azureDevOpsHandlerForTesting, httpAdapterHandlerForTesting, uploadHandlerForTesting, launchDarklyHandlerForTesting, adapterCredentialsHandlerForTesting, projectSecretsHandlerForTesting, evidenceConfigHandlerForTesting);

    private async Task<int> RunWithConfigAsync(
        Func<ReleaseTwinConfig> loadConfig,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken,
        Func<Func<string, string?>, Task<(IReadOnlyList<LoadedCase>? Cases, string? Error)>> loadCasesAsync,
        HttpMessageHandler? azureDevOpsHandlerForTesting,
        HttpMessageHandler? httpAdapterHandlerForTesting,
        HttpMessageHandler? uploadHandlerForTesting,
        HttpMessageHandler? launchDarklyHandlerForTesting,
        HttpMessageHandler? adapterCredentialsHandlerForTesting,
        HttpMessageHandler? projectSecretsHandlerForTesting,
        HttpMessageHandler? evidenceConfigHandlerForTesting)
    {
        ReleaseTwinConfig config;
        try
        {
            config = loadConfig();
        }
        catch (ReleaseTwinConfigException ex)
        {
            output.WriteLine(ex.Message);
            return 1;
        }

        return await RunCoreAsync(
            config, environment, output, cancellationToken, loadCasesAsync,
            azureDevOpsHandlerForTesting, httpAdapterHandlerForTesting, uploadHandlerForTesting, launchDarklyHandlerForTesting, adapterCredentialsHandlerForTesting, projectSecretsHandlerForTesting, evidenceConfigHandlerForTesting);
    }

    // hosted-project-secrets: takes the effective environment-variable resolver (local environment
    // first, hosted-fetched project secrets as fallback) built by RunCoreAsync, so both loading paths
    // parse case files through the exact same `${VAR_NAME}` resolution.
    private static Func<Func<string, string?>, Task<(IReadOnlyList<LoadedCase>? Cases, string? Error)>> LoadLocalCasesAsync(string casesDirectory) => resolveEnvironmentVariable =>
    {
        try
        {
            return Task.FromResult<(IReadOnlyList<LoadedCase>?, string?)>((new CaseFileLoader(casesDirectory, resolveEnvironmentVariable: resolveEnvironmentVariable).LoadAll(), null));
        }
        catch (CaseFileException ex)
        {
            return Task.FromResult<(IReadOnlyList<LoadedCase>?, string?)>((null, $"Failed to load cases: {ex.Message}"));
        }
    };

    private static Func<Func<string, string?>, Task<(IReadOnlyList<LoadedCase>? Cases, string? Error)>> LoadHostedJourneyAsync(
        Guid journeyId, int version, IReadOnlyDictionary<string, string?> environment, HttpMessageHandler? journeyFetchHandlerForTesting) => async resolveEnvironmentVariable =>
    {
        string? Get(string key) => environment.TryGetValue(key, out var value) ? value : null;

        var apiToken = Get("RELEASETWIN_API_TOKEN");
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return (null, "Running a hosted journey requires RELEASETWIN_API_TOKEN to be set.");
        }

        var baseUrl = Get("RELEASETWIN_API_URL") is { Length: > 0 } url ? url : "https://api.releasetwin.example";

        string yamlContent;
        using (var fetchClient = new JourneyFetchClient(baseUrl, apiToken, journeyFetchHandlerForTesting))
        {
            try
            {
                yamlContent = await fetchClient.FetchJourneyVersionAsync(journeyId, version, CancellationToken.None);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JourneyFetchException)
            {
                return (null, $"Failed to fetch journey {journeyId} version {version}: {ex.Message}");
            }
        }

        try
        {
            var fixturesRoot = Get("RELEASETWIN_FIXTURES_ROOT") is { Length: > 0 } root ? root : Path.Combine(Directory.GetCurrentDirectory(), "fixtures");
            var loaded = CaseFileLoader.ForFixturesRoot(fixturesRoot, resolveEnvironmentVariable).ParseYaml($"journey {journeyId}@{version}", yamlContent);
            return (new[] { loaded }, null);
        }
        catch (CaseFileException ex)
        {
            return (null, $"Failed to parse fetched journey {journeyId} version {version}: {ex.Message}");
        }
    };

    private async Task<int> RunCoreAsync(
        ReleaseTwinConfig config,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken,
        Func<Func<string, string?>, Task<(IReadOnlyList<LoadedCase>? Cases, string? Error)>> loadCasesAsync,
        HttpMessageHandler? azureDevOpsHandlerForTesting,
        HttpMessageHandler? httpAdapterHandlerForTesting,
        HttpMessageHandler? uploadHandlerForTesting,
        HttpMessageHandler? launchDarklyHandlerForTesting,
        HttpMessageHandler? adapterCredentialsHandlerForTesting,
        HttpMessageHandler? projectSecretsHandlerForTesting,
        HttpMessageHandler? evidenceConfigHandlerForTesting = null)
    {
        string? Get(string key) => environment.TryGetValue(key, out var value) ? value : null;

        // add-feature-flag-seam: end-to-end proof the flag seam is wired on the CLI surface. Fully
        // offline — resolves from the embedded registry + releasetwin.yaml overrides. The smoke line
        // is RELEASETWIN_DEBUG-only; unknown-override warnings always print. Gates nothing.
        try
        {
            var flagRegistry = ReleaseTwin.Core.FeatureFlags.FlagRegistry.Load();
            foreach (var unknownFlag in flagRegistry.UnknownKeys(config.FeatureFlags.Keys))
            {
                output.WriteLine($"WARN: releasetwin.yaml feature_flags names unknown flag '{unknownFlag}' — ignored.");
            }
            var flagService = new ReleaseTwin.Core.FeatureFlags.FlagService(
                new ReleaseTwin.Core.FeatureFlags.StaticFlagProvider(flagRegistry, config.FeatureFlags), flagRegistry);
            var flagContext = new ReleaseTwin.Core.FeatureFlags.FlagContext(
                TargetingKey: config.Organization,
                ProjectId: config.Project,
                Env: string.IsNullOrEmpty(Get("CI")) ? "development" : "production");
            var smoke = await flagService.GetBooleanAsync("flag-seam-smoke", flagContext, cancellationToken);
            if (!string.IsNullOrEmpty(Get("RELEASETWIN_DEBUG")))
            {
                output.WriteLine($"flag_seam_smoke surface=cli value={smoke}");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"WARN: feature-flag evaluation unavailable: {ex.Message}");
        }

        // cli-runner (hosted-self-serve-platform delta): upload is entirely optional. No token, no
        // upload attempt, no error — the CLI behaves exactly as it did before this capability existed.
        var apiToken = Get("RELEASETWIN_API_TOKEN");
        var apiUrl = Get("RELEASETWIN_API_URL") is { Length: > 0 } configuredUrl ? configuredUrl : "https://api.releasetwin.example";
        IngestClient? ingestClient = apiToken is { Length: > 0 }
            ? new IngestClient(apiUrl, apiToken, uploadHandlerForTesting)
            : null;

        // evidence-capture (cli-runner delta): capture is opt-in and off by default. An explicit
        // RELEASETWIN_EVIDENCE=on|off wins over the hosted per-project default; the hosted default is
        // only consulted when a token is configured and the env var did not decide. Capture only
        // actually runs when there is also a token to upload through.
        var envEvidenceToggle = ParseToggle(Get("RELEASETWIN_EVIDENCE"));
        var captureEvidence = false;
        // The hosted per-project default is only consulted against a real hosted platform — i.e.
        // when RELEASETWIN_API_URL is explicitly configured (or a test supplies a handler).
        var hostedConfigReachable = evidenceConfigHandlerForTesting is not null || Get("RELEASETWIN_API_URL") is { Length: > 0 };
        if (ingestClient is not null)
        {
            if (envEvidenceToggle is { } explicitChoice)
            {
                captureEvidence = explicitChoice;
            }
            else if (hostedConfigReachable)
            {
                using var evidenceConfigClient = new EvidenceConfigClient(apiUrl, apiToken!, evidenceConfigHandlerForTesting);
                try
                {
                    captureEvidence = (await evidenceConfigClient.FetchAsync(cancellationToken)).CaptureDefault;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or EvidenceConfigFetchException)
                {
                    output.WriteLine($"WARN: failed to fetch hosted evidence config: {ex.Message}");
                }
            }
        }

        var present = AzureDevOpsEnvironmentVariables.Where(key => !string.IsNullOrWhiteSpace(Get(key))).ToList();
        var missing = AzureDevOpsEnvironmentVariables.Except(present).ToList();

        if (present.Count > 0 && missing.Count > 0)
        {
            output.WriteLine($"Azure DevOps is partially configured; missing: {string.Join(", ", missing)}");
            return 1;
        }

        // config-driven-adapter-selection: with a `releasetwin.yaml` `adapters:` list, only listed
        // adapters are considered; the partial-config check above still fires regardless.
        AzureDevOpsAdapter? azureDevOpsAdapter = null;
        if (config.Considers("azure-devops"))
        {
            if (missing.Count == 0)
            {
                var options = new AzureDevOpsOptions(Get("AZDO_ORG")!, Get("AZDO_PROJECT")!, Get("AZDO_PAT")!);
                var variableGroupId = int.Parse(Get("AZDO_VARIABLE_GROUP_ID")!);
                azureDevOpsAdapter = new AzureDevOpsAdapter(options, Get("AZDO_AREA_PATH")!, variableGroupId, handler: azureDevOpsHandlerForTesting);
            }
            else if (apiToken is { Length: > 0 })
            {
                // hosted-adapter-credentials: environment vars are entirely absent for this adapter —
                // fall back to a hosted fetch before deciding it's not installed. Env vars, when fully
                // present, always win (handled above, before this branch is ever reached).
                var fields = await TryFetchAdapterCredentialAsync("azure-devops", apiToken, apiUrl, adapterCredentialsHandlerForTesting, output, cancellationToken);
                if (fields is not null)
                {
                    try
                    {
                        var options = new AzureDevOpsOptions(fields["org"], fields["project"], fields["pat"]);
                        var variableGroupId = int.Parse(fields["variableGroupId"]);
                        azureDevOpsAdapter = new AzureDevOpsAdapter(options, fields["areaPath"], variableGroupId, handler: azureDevOpsHandlerForTesting);
                    }
                    catch (Exception ex) when (ex is KeyNotFoundException or FormatException)
                    {
                        output.WriteLine($"WARN: hosted 'azure-devops' credentials are missing or malformed: {ex.Message}");
                    }
                }
            }
        }

        if (config.Requires("azure-devops") && azureDevOpsAdapter is null)
        {
            output.WriteLine("releasetwin.yaml lists 'azure-devops' but its credentials are set in neither the environment (AZDO_ORG / AZDO_PROJECT / AZDO_PAT / AZDO_AREA_PATH / AZDO_VARIABLE_GROUP_ID) nor a hosted adapter credential.");
            return 1;
        }

        var presentLd = LaunchDarklyEnvironmentVariables.Where(key => !string.IsNullOrWhiteSpace(Get(key))).ToList();
        var missingLd = LaunchDarklyEnvironmentVariables.Except(presentLd).ToList();

        if (presentLd.Count > 0 && missingLd.Count > 0)
        {
            output.WriteLine($"LaunchDarkly is partially configured; missing: {string.Join(", ", missingLd)}");
            return 1;
        }

        // Optional: which flag `ld.readFeatureFlag` reads back. Defaults to the adapter's own
        // built-in demo flag name so existing zero-config example cases are unaffected; a real
        // flag-proof run against a customer's own flag overrides this to match that case's
        // `flag_proof.feature_key` (the name actually being toggled).
        var ldFlagKey = Get("LAUNCHDARKLY_FLAG_KEY") is { Length: > 0 } flagKeyOverride ? flagKeyOverride : "release-proof-feature";

        LaunchDarklyAdapter? launchDarklyAdapter = null;
        if (config.Considers("launchdarkly"))
        {
            if (missingLd.Count == 0)
            {
                var ldOptions = new LaunchDarklyOptions(Get("LAUNCHDARKLY_API_TOKEN")!, Get("LAUNCHDARKLY_PROJECT_KEY")!, Get("LAUNCHDARKLY_ENVIRONMENT_KEY")!);
                launchDarklyAdapter = new LaunchDarklyAdapter(ldOptions, ldFlagKey, launchDarklyHandlerForTesting);
            }
            else if (apiToken is { Length: > 0 })
            {
                var fields = await TryFetchAdapterCredentialAsync("launchdarkly", apiToken, apiUrl, adapterCredentialsHandlerForTesting, output, cancellationToken);
                if (fields is not null)
                {
                    try
                    {
                        var ldOptions = new LaunchDarklyOptions(fields["apiToken"], fields["projectKey"], fields["environmentKey"]);
                        launchDarklyAdapter = new LaunchDarklyAdapter(ldOptions, ldFlagKey, launchDarklyHandlerForTesting);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        output.WriteLine($"WARN: hosted 'launchdarkly' credentials are missing or malformed: {ex.Message}");
                    }
                }
            }
        }

        if (config.Requires("launchdarkly") && launchDarklyAdapter is null)
        {
            output.WriteLine("releasetwin.yaml lists 'launchdarkly' but its credentials are set in neither the environment (LAUNCHDARKLY_API_TOKEN / LAUNCHDARKLY_PROJECT_KEY / LAUNCHDARKLY_ENVIRONMENT_KEY) nor a hosted adapter credential.");
            return 1;
        }

        // Unlike the credential-gated adapters above, the UI adapter needs no credentials — but
        // launching a real browser process is expensive and requires browser binaries to be
        // installed, so it's opt-in rather than unconditional like the HTTP adapter.
        var uiEnabled = (Get("RELEASETWIN_UI_ENABLED") is { Length: > 0 } uiFlag && (uiFlag == "1" || string.Equals(uiFlag, "true", StringComparison.OrdinalIgnoreCase)))
            || config.Requires("ui");
        // ui-session-video: opt-in browser-session recording, same shape as RELEASETWIN_UI_ENABLED.
        var uiVideoDir = Get("RELEASETWIN_UI_VIDEO_DIR") is { Length: > 0 } dir ? dir : null;
        UiAdapter? uiAdapter = null;
        if (uiEnabled)
        {
            try
            {
                uiAdapter = await UiAdapter.CreateAsync(recordVideoDir: uiVideoDir, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                output.WriteLine($"Failed to start the UI adapter (browser launch failed): {ex.Message}");
                return 1;
            }
        }

        using var httpAdapter = new HttpAdapter(httpAdapterHandlerForTesting);

        try
        {
            var root = new CompositionRoot();
            if (azureDevOpsAdapter is not null)
            {
                root.Install(azureDevOpsAdapter);
            }

            if (launchDarklyAdapter is not null)
            {
                root.Install(launchDarklyAdapter);
            }

            if (uiAdapter is not null)
            {
                root.Install(uiAdapter);
            }

            root.Install(httpAdapter);
            var catalog = root.Catalog;
            var executor = root.BuildExecutor();

            // Whichever installed adapter exposes a feature-state controller, not any specific one by
            // name — cli-runner's own requirement text never named Azure DevOps specifically.
            var featureStateController = new IFeatureStateControllerSource?[] { azureDevOpsAdapter, launchDarklyAdapter }
                .OfType<IFeatureStateControllerSource>()
                .Select(source => source.FeatureStateController)
                .FirstOrDefault(controller => controller is not null);

            // hosted-project-secrets: fetch this project's stored secrets once per run (only when a
            // project token is configured — no token, no fetch, no behavior change) and fall back to
            // them for any `${VAR_NAME}` the local environment doesn't have. Local environment values
            // always win and never trigger this fetch's result to be consulted for that name.
            var projectSecrets = new Dictionary<string, string>();
            if (apiToken is { Length: > 0 })
            {
                using var secretsClient = new ProjectSecretsClient(apiUrl, apiToken, projectSecretsHandlerForTesting);
                try
                {
                    projectSecrets = new Dictionary<string, string>(await secretsClient.FetchAllAsync(cancellationToken));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ProjectSecretsFetchException)
                {
                    output.WriteLine($"WARN: failed to fetch hosted project secrets: {ex.Message}");
                }
            }

            string? ResolveEnvironmentVariable(string name) =>
                Get(name) ?? (projectSecrets.TryGetValue(name, out var secretValue) ? secretValue : null);

            // evidence-capture: values the redactor must mask wherever they surface in captured
            // evidence — every hosted project secret, plus the credential-bearing environment
            // variables this run may have used.
            var secretsToMask = projectSecrets.Values
                .Concat(new[] { "AZDO_PAT", "LAUNCHDARKLY_API_TOKEN", "RELEASETWIN_API_TOKEN" }.Select(Get))
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .ToList();
            var redactor = new EvidenceRedactor(secretsToMask);
            var executionOptions = new ExecutionOptions { CaptureEvidence = captureEvidence && ingestClient is not null };

            var (cases, loadError) = await loadCasesAsync(ResolveEnvironmentVariable);
            if (cases is null)
            {
                output.WriteLine(loadError);
                return 1;
            }

            // ci-pr-integration: build the JSON run summary from the same per-case results printed
            // below, when --summary-json / RELEASETWIN_SUMMARY_JSON is set (design.md D-A).
            var summaryPath = Get("RELEASETWIN_SUMMARY_JSON") is { Length: > 0 } sp ? sp : null;
            var summary = summaryPath is null ? null : new RunSummaryBuilder();
            // pr-annotation-evidence-link: the project-dashboard URL, taken from the first successful
            // upload's response. Stays null when nothing was uploaded.
            string? runUrl = null;

            var passed = 0;
            var failed = 0;
            foreach (var loadedCase in cases)
            {
                var testCase = WithEffectiveCapabilities(loadedCase.Case);

                if (loadedCase.FlagProof is { } flagProof)
                {
                    // http-flag-control: a per-case `flag_proof.control` block wins over any
                    // adapter-vended controller — it toggles a flag system nothing installed knows.
                    var controller = flagProof.Control is { } control
                        ? new HttpFeatureStateController(
                            httpAdapter.HttpClient,
                            flagProof.FeatureKey,
                            control.Method,
                            control.Url,
                            control.Headers,
                            control.Body,
                            knownBadWhenDisabled: control.Polarity == FlagProofPolarity.KnownBadWhenDisabled,
                            verify: control.Verify is { } v
                                ? new HttpFlagVerify(v.Method, v.Url, v.Headers, v.Body, v.JsonPath, v.Expected)
                                : null,
                            auth: control.Auth is { } a
                                ? new HttpFlagAuth(a.TokenUrl, a.ClientId, a.ClientSecret, a.Scope)
                                : null)
                        : featureStateController;

                    if (controller is null)
                    {
                        failed++;
                        summary?.AddCase(testCase.CaseId, passed: false, classification: null, flagProofOutcome: "Ineligible", release: testCase.Release);
                        output.WriteLine($"FLAGPROOF {testCase.CaseId} (Ineligible): no installed adapter exposes feature-state control and the case declares no flag_proof.control");
                        continue;
                    }

                    var flagProofRunner = new FlagProofRunner(executor, catalog, controller);
                    var flagProofExecution = await flagProofRunner.RunAsync(testCase, flagProof.FeatureKey, flagProof.BuildIdentity, executionOptions, cancellationToken: cancellationToken);
                    var result = flagProofExecution.Result;

                    if (result.Outcome == FlagProofOutcome.Passed)
                    {
                        passed++;
                    }
                    else
                    {
                        failed++;
                    }

                    output.WriteLine(result.Message is { Length: > 0 } flagProofMessage
                        ? $"FLAGPROOF {result.CaseId} ({result.Outcome}): {flagProofMessage}"
                        : $"FLAGPROOF {result.CaseId} ({result.Outcome})");

                    string? flagProofEvidenceUrl = null;
                    if (ingestClient is not null)
                    {
                        try
                        {
                            RedactionResult? evidence = null;
                            if (flagProofExecution.KnownBadEvidence is not null || flagProofExecution.KnownGoodEvidence is not null)
                            {
                                var seed = flagProofExecution.KnownBadEvidence ?? flagProofExecution.KnownGoodEvidence!;
                                evidence = redactor.Redact(seed, flagProofExecution.KnownBadEvidence, flagProofExecution.KnownGoodEvidence, loadedCase.Evidence);
                            }

                            var upload = await ingestClient.UploadFlagProofReportAsync(result, evidence, cancellationToken, testCase.Release);
                            runUrl ??= upload.RunUrl;
                            if (evidence is not null && upload.EvidenceAccepted)
                            {
                                flagProofEvidenceUrl = upload.ReportUrl;
                            }
                            else if (evidence is not null && !upload.EvidenceAccepted)
                            {
                                output.WriteLine($"WARN evidence not accepted for {result.CaseId} (report uploaded; check your plan tier)");
                            }
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                        {
                            // Upload failure never changes the case's own outcome or the exit code
                            // (cli-runner spec: "Upload failure is a warning, not a case failure").
                            output.WriteLine($"WARN upload failed for {result.CaseId}: {ex.Message}");
                        }
                    }

                    summary?.AddCase(result.CaseId, result.Outcome == FlagProofOutcome.Passed, classification: null, flagProofOutcome: result.Outcome.ToString(), release: testCase.Release, evidenceUrl: flagProofEvidenceUrl);

                    continue;
                }

                var execution = await executor.ExecuteAsync(testCase, executionOptions, cancellationToken);
                var report = execution.Report;
                if (report.Passed)
                {
                    passed++;
                    output.WriteLine($"PASS {report.CaseId}");
                }
                else
                {
                    failed++;
                    output.WriteLine($"FAIL {report.CaseId} ({report.Classification}): {report.FailureDetail}");
                }

                string? caseEvidenceUrl = null;
                if (ingestClient is not null)
                {
                    try
                    {
                        var evidence = execution.Evidence is null
                            ? null
                            : redactor.Redact(execution.Evidence, null, null, loadedCase.Evidence);

                        var upload = await ingestClient.UploadCaseReportAsync(report, evidence, cancellationToken, testCase.Release);
                        runUrl ??= upload.RunUrl;
                        if (evidence is not null && upload.EvidenceAccepted)
                        {
                            caseEvidenceUrl = upload.ReportUrl;
                        }
                        else if (evidence is not null && !upload.EvidenceAccepted)
                        {
                            output.WriteLine($"WARN evidence not accepted for {report.CaseId} (report uploaded; check your plan tier)");
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        // Upload failure never changes the case's own outcome or the exit code
                        // (cli-runner spec: "Upload failure is a warning, not a case failure").
                        output.WriteLine($"WARN upload failed for {report.CaseId}: {ex.Message}");
                    }
                }

                summary?.AddCase(report.CaseId, report.Passed, report.Classification?.ToString(), flagProofOutcome: null, release: testCase.Release, evidenceUrl: caseEvidenceUrl);
            }

            output.WriteLine($"{passed} passed, {failed} failed");

            if (summaryPath is not null && summary is not null)
            {
                // Written on pass or fail (design.md D-A). The destination directory was validated
                // up front in CliEntrypoint, so this only fails on a genuine I/O fault.
                try
                {
                    RunSummaryWriter.Write(summaryPath, summary.Build(runUrl));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    output.WriteLine($"WARN: failed to write run summary to {summaryPath}: {ex.Message}");
                }
            }

            return failed == 0 ? 0 : 1;
        }
        finally
        {
            azureDevOpsAdapter?.Dispose();
            launchDarklyAdapter?.Dispose();
            uiAdapter?.Dispose();
            ingestClient?.Dispose();
        }
    }

    /// <summary>Parses an on/off-style toggle env var. Null when unset or unrecognized (defer to the hosted default).</summary>
    private static bool? ParseToggle(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "on" or "1" or "true" or "yes" => true,
        "off" or "0" or "false" or "no" => false,
        _ => null,
    };

    /// <summary>
    /// hosted-adapter-credentials: attempts a hosted fetch for one adapter's credentials, returning
    /// null (not installing that adapter) on "not configured" or any fetch failure — this whole path
    /// is optional, exactly like an entirely-absent set of environment variables already is.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>?> TryFetchAdapterCredentialAsync(
        string adapter, string apiToken, string apiUrl, HttpMessageHandler? handler, TextWriter output, CancellationToken cancellationToken)
    {
        using var client = new AdapterCredentialsClient(apiUrl, apiToken, handler);
        try
        {
            return await client.FetchAsync(adapter, cancellationToken);
        }
        catch (AdapterCredentialNotConfiguredException)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AdapterCredentialFetchException)
        {
            output.WriteLine($"WARN: failed to fetch hosted '{adapter}' credentials: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// graceful-capability-gating: unions a case's declared `requires:` with whatever
    /// <see cref="AzureDevOpsAdapter.KnownOperationCapabilities"/> / <see cref="LaunchDarklyAdapter.KnownOperationCapabilities"/>
    /// implies from the operation, prerequisite, and cleanup names actually referenced — so a case
    /// that forgets to declare `requires:` for a known-gated operation is still protected from crashing.
    /// </summary>
    private static TestCase WithEffectiveCapabilities(TestCase testCase)
    {
        var referencedNames = testCase.Prerequisites.Select(p => p.CheckName)
            .Concat(testCase.Pipeline.Select(p => p.OperationName))
            .Concat(testCase.Cleanup.Select(c => c.OperationName))
            .ToList();

        var knownOperationCapabilities = AzureDevOpsAdapter.KnownOperationCapabilities
            .Concat(LaunchDarklyAdapter.KnownOperationCapabilities)
            .Concat(UiAdapter.KnownOperationCapabilities)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var inferredCapabilities = referencedNames
            .Where(knownOperationCapabilities.ContainsKey)
            .Select(name => knownOperationCapabilities[name]);

        var effectiveCapabilities = testCase.RequiredCapabilities.Select(c => c.Name)
            .Concat(inferredCapabilities)
            .Distinct()
            .Select(name => new CapabilityRequirement(name))
            .ToList();

        return testCase with { RequiredCapabilities = effectiveCapabilities };
    }
}
