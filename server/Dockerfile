FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY server/BenchmarkServer.csproj server/
RUN dotnet restore server/BenchmarkServer.csproj

COPY server/ server/
RUN dotnet publish server/BenchmarkServer.csproj \
	--configuration ${BUILD_CONFIGURATION} \
	--output /app/publish \
	--no-restore \
	/p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
	RESULTS_DIR=/app/results
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BenchmarkServer.dll"]

