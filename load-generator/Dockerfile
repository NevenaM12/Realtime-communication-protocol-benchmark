FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY load-generator/LoadGenerator.csproj load-generator/
RUN dotnet restore load-generator/LoadGenerator.csproj

COPY load-generator/ load-generator/
RUN dotnet publish load-generator/LoadGenerator.csproj \
	--configuration ${BUILD_CONFIGURATION} \
	--output /app/publish \
	--no-restore \
	/p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
ENV SERVER_URL=http://benchmark-server:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LoadGenerator.dll"]

