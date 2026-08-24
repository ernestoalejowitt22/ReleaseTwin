using ReleaseTwin.AdapterSdk;
using ReleaseTwin.Adapters.AzureDevOps;
using ReleaseTwin.Adapters.Http;
using ReleaseTwin.Cli.CaseLoading;
using ReleaseTwin.Cli.Upload;
using ReleaseTwin.Core;

namespace ReleaseTwin.Cli;

/// <summary>
/// design.md D5: composes the credential-free HTTP adapter unconditionally, and the Azure DevOps
/// adapter only when all of its environment variables are present. Partial Azure DevOps config is
/// treated as a mistake (clear startup error), not a silent skip.
/// </summary>
public sealed class CliRunner
{
    private static readonly string[] AzureDevOpsEnvironmentVariables =
    {
        "AZDO_ORG", "AZDO_PROJECT", "AZDO_PAT", "AZDO_AREA_PATH", "AZDO_VARIABLE_GROUP_ID",
    };

    public async Task<int> RunAsync(
        string casesDirectory,
        IReadOnlyDictionary<string, string?> environment,
        TextWriter output,
        CancellationToken cancellationToken = default,
        HttpMessageHandler? azureDevOpsHandlerForTesting = null,
        HttpMessageHandler? httpAdapterHandlerForTesting = null,
        HttpMessageHandler? uploadHandlerForTesting = null)
    {
        string? Get(string key) => environment.TryGetValue(key, out var value) ? value : null;

        // cli-runner (hosted-self-serve-platform delta): upload is entirely optional. No token, no
        // upload attempt, no error — the CLI behaves exactly as it did before this capability existed.
        var apiToken = Get("RELEASETWIN_API_TOKEN");
        IngestClient? ingestClient = apiToken is { Length: > 0 }
            ? new IngestClient(Get("RELEASETWIN_API_URL") is { Length: > 0 } url ? url : "https://api.releasetwin.example", apiToken, uploadHandlerForTesting)
            : null;

        var present = AzureDevOpsEnvironmentVariables.Where(key => !string.IsNullOrWhiteSpace(Get(key))).ToList();
        var missing = AzureDevOpsEnvironmentVariables.Except(present).ToList();

        if (present.Count > 0 && missing.Count > 0)
        {
            output.WriteLine($"Azure DevOps is partially configured; missing: {string.Join(", ", missing)}");
            return 1;
        }

        var installAzureDevOps = missing.Count == 0;

        AzureDevOpsAdapter? azureDevOpsAdapter = null;
        if (installAzureDevOps)
        {
            var options = new AzureDevOpsOptions(Get("AZDO_ORG")!, Get("AZDO_PROJECT")!, Get("AZDO_PAT")!);
            var variableGroupId = int.Parse(Get("AZDO_VARIABLE_GROUP_ID")!);
            azureDevOpsAdapter = new AzureDevOpsAdapter(options, Get("AZDO_AREA_PATH")!, variableGroupId, handler: azureDevOpsHandlerForTesting);
        }

        using var httpAdapter = new HttpAdapter(httpAdapterHandlerForTesting);

        try
        {
            var root = new CompositionRoot();
            if (azureDevOpsAdapter is not null)
            {
                root.Install(azureDevOpsAdapter);
            }

            root.Install(httpAdapter);
            var catalog = root.Catalog;
            var executor = root.BuildExecutor();

            IReadOnlyList<LoadedCase> cases;
            try
            {
                cases = new CaseFileLoader(casesDirectory).LoadAll();
            }
            catch (CaseFileException ex)
            {
                output.WriteLine($"Failed to load cases: {ex.Message}");
                return 1;
            }

            var passed = 0;
            var failed = 0;
            foreach (var loadedCase in cases)
            {
                var testCase = WithEffectiveCapabilities(loadedCase.Case);

                if (loadedCase.FlagProof is { } flagProof)
                {
                    if (azureDevOpsAdapter is null)
                    {
                        failed++;
                        output.WriteLine($"FLAGPROOF {testCase.CaseId} (Ineligible): no installed adapter exposes feature-state control");
                        continue;
                    }

                    var flagProofRunner = new FlagProofRunner(executor, catalog, azureDevOpsAdapter.FeatureStateController);
                    var result = await flagProofRunner.RunAsync(testCase, flagProof.FeatureKey, flagProof.BuildIdentity, cancellationToken: cancellationToken);

                    if (result.Outcome == FlagProofOutcome.Passed)
                    {
                        passed++;
                    }
                    else
                    {
                        failed++;
                    }

                    output.WriteLine($"FLAGPROOF {result.CaseId} ({result.Outcome})");

                    if (ingestClient is not null)
                    {
                        try
                        {
                            await ingestClient.UploadFlagProofReportAsync(result, cancellationToken);
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                        {
                            // Upload failure never changes the case's own outcome or the exit code
                            // (cli-runner spec: "Upload failure is a warning, not a case failure").
                            output.WriteLine($"WARN upload failed for {result.CaseId}: {ex.Message}");
                        }
                    }

                    continue;
                }

                var report = await executor.ExecuteAsync(testCase, cancellationToken);
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

                if (ingestClient is not null)
                {
                    try
                    {
                        await ingestClient.UploadCaseReportAsync(report, cancellationToken);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        // Upload failure never changes the case's own outcome or the exit code
                        // (cli-runner spec: "Upload failure is a warning, not a case failure").
                        output.WriteLine($"WARN upload failed for {report.CaseId}: {ex.Message}");
                    }
                }
            }

            output.WriteLine($"{passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }
        finally
        {
            azureDevOpsAdapter?.Dispose();
            ingestClient?.Dispose();
        }
    }

    /// <summary>
    /// graceful-capability-gating: unions a case's declared `requires:` with whatever
    /// <see cref="AzureDevOpsAdapter.KnownOperationCapabilities"/> implies from the operation,
    /// prerequisite, and cleanup names actually referenced — so a case that forgets to declare
    /// `requires:` for a known-gated operation is still protected from crashing.
    /// </summary>
    private static TestCase WithEffectiveCapabilities(TestCase testCase)
    {
        var referencedNames = testCase.Prerequisites.Select(p => p.CheckName)
            .Concat(testCase.Pipeline.Select(p => p.OperationName))
            .Concat(testCase.Cleanup.Select(c => c.OperationName));

        var inferredCapabilities = referencedNames
            .Where(AzureDevOpsAdapter.KnownOperationCapabilities.ContainsKey)
            .Select(name => AzureDevOpsAdapter.KnownOperationCapabilities[name]);

        var effectiveCapabilities = testCase.RequiredCapabilities.Select(c => c.Name)
            .Concat(inferredCapabilities)
            .Distinct()
            .Select(name => new CapabilityRequirement(name))
            .ToList();

        return testCase with { RequiredCapabilities = effectiveCapabilities };
    }
}
