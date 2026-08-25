FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ReleaseTwin.Core/ReleaseTwin.Core.csproj src/ReleaseTwin.Core/
COPY src/ReleaseTwin.AdapterSdk/ReleaseTwin.AdapterSdk.csproj src/ReleaseTwin.AdapterSdk/
COPY src/ReleaseTwin.Adapters.AzureDevOps/ReleaseTwin.Adapters.AzureDevOps.csproj src/ReleaseTwin.Adapters.AzureDevOps/
COPY src/ReleaseTwin.Adapters.Http/ReleaseTwin.Adapters.Http.csproj src/ReleaseTwin.Adapters.Http/
COPY src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj src/ReleaseTwin.Cli/
RUN dotnet restore src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj

COPY src/ReleaseTwin.Core/ src/ReleaseTwin.Core/
COPY src/ReleaseTwin.AdapterSdk/ src/ReleaseTwin.AdapterSdk/
COPY src/ReleaseTwin.Adapters.AzureDevOps/ src/ReleaseTwin.Adapters.AzureDevOps/
COPY src/ReleaseTwin.Adapters.Http/ src/ReleaseTwin.Adapters.Http/
COPY src/ReleaseTwin.Cli/ src/ReleaseTwin.Cli/

RUN dotnet publish src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "ReleaseTwin.Cli.dll"]
CMD ["/workspace/cases"]
