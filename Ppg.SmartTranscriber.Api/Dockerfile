# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Ppg.SmartTranscriber.Api/Ppg.SmartTranscriber.Api.csproj", "Ppg.SmartTranscriber.Api/"]
COPY ["Ppg.SmartTranscriber.Data/Ppg.SmartTranscriber.Data.csproj", "Ppg.SmartTranscriber.Data/"]
COPY ["Ppg.SmartTranscriber.Services/Ppg.SmartTranscriber.Services.csproj", "Ppg.SmartTranscriber.Services/"]
RUN dotnet restore "Ppg.SmartTranscriber.Api/Ppg.SmartTranscriber.Api.csproj"
COPY . .
WORKDIR "/src/Ppg.SmartTranscriber.Api"
RUN dotnet build "Ppg.SmartTranscriber.Api.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/Ppg.SmartTranscriber.Api"
RUN dotnet publish "Ppg.SmartTranscriber.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /https
ENTRYPOINT ["dotnet", "Ppg.SmartTranscriber.Api.dll"]