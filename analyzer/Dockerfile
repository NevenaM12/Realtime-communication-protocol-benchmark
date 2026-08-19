FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY analyzer/BenchmarkAnalyzer.csproj analyzer/
RUN dotnet restore analyzer/BenchmarkAnalyzer.csproj

COPY analyzer/ analyzer/
RUN dotnet publish analyzer/BenchmarkAnalyzer.csproj \
	--configuration ${BUILD_CONFIGURATION} \
	--output /app/publish \
	--no-restore \
	/p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BenchmarkAnalyzer.dll"]

