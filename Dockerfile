FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ReleaseTwin.Core/ReleaseTwin.Core.csproj src/ReleaseTwin.Core/
COPY src/ReleaseTwin.AdapterSdk/ReleaseTwin.AdapterSdk.csproj src/ReleaseTwin.AdapterSdk/
COPY src/ReleaseTwin.Adapters.AzureDevOps/ReleaseTwin.Adapters.AzureDevOps.csproj src/ReleaseTwin.Adapters.AzureDevOps/
COPY src/ReleaseTwin.Adapters.Http/ReleaseTwin.Adapters.Http.csproj src/ReleaseTwin.Adapters.Http/
COPY src/ReleaseTwin.Adapters.LaunchDarkly/ReleaseTwin.Adapters.LaunchDarkly.csproj src/ReleaseTwin.Adapters.LaunchDarkly/
COPY src/ReleaseTwin.Adapters.Ui/ReleaseTwin.Adapters.Ui.csproj src/ReleaseTwin.Adapters.Ui/
COPY src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj src/ReleaseTwin.Cli/
RUN dotnet restore src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj

COPY src/ReleaseTwin.Core/ src/ReleaseTwin.Core/
COPY src/ReleaseTwin.AdapterSdk/ src/ReleaseTwin.AdapterSdk/
COPY src/ReleaseTwin.Adapters.AzureDevOps/ src/ReleaseTwin.Adapters.AzureDevOps/
COPY src/ReleaseTwin.Adapters.Http/ src/ReleaseTwin.Adapters.Http/
COPY src/ReleaseTwin.Adapters.LaunchDarkly/ src/ReleaseTwin.Adapters.LaunchDarkly/
COPY src/ReleaseTwin.Adapters.Ui/ src/ReleaseTwin.Adapters.Ui/
COPY src/ReleaseTwin.Cli/ src/ReleaseTwin.Cli/

# add-feature-flag-seam: ReleaseTwin.Core embeds the repo-root feature-flag registry
# (<EmbeddedResource Include="..\..\flags.json" />), so it must be in the build context.
COPY flags.json flags.json

RUN dotnet publish src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
COPY --from=build /app /app

# Bundled example cases/fixtures — `releasetwin init --from-examples` copies from here,
# so image users get the full example set without a source checkout. (The UI adapter is
# compiled in but needs a Chromium that isn't in this image; it degrades gracefully.)
COPY examples/ /opt/releasetwin/examples/

# The entrypoint names the DLL by absolute path and the working directory is the mount point,
# so `docker run -v "$PWD:/workspace" <image> init` scaffolds into the mount, a bare `run` finds
# `./cases`, and a user who adds `-w /workspace` no longer breaks the entrypoint (a relative DLL
# path plus `-w` produced a misleading "No .NET SDKs were found").
WORKDIR /workspace
ENTRYPOINT ["dotnet", "/app/ReleaseTwin.Cli.dll"]
CMD ["run", "/workspace/cases"]
