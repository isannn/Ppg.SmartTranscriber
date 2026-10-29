FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8460

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/SmartTranscriber.Api/SmartTranscriber.Api.csproj", "src/SmartTranscriber.Api/"]
COPY ["src/SmartTranscriber.Core/SmartTranscriber.Core.csproj", "src/SmartTranscriber.Core/"]
COPY ["src/SmartTranscriber.Infrastructure/SmartTranscriber.Infrastructure.csproj", "src/SmartTranscriber.Infrastructure/"]
COPY ["db/Migrations/", "db/Migrations/"]
RUN dotnet restore "src/SmartTranscriber.Api/SmartTranscriber.Api.csproj"
COPY . .
WORKDIR "/src/src/SmartTranscriber.Api"
RUN dotnet publish "SmartTranscriber.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartTranscriber.Api.dll"]
