# Development stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Ppg.SmartTranscriber.Web/Ppg.SmartTranscriber.Web.csproj", "Ppg.SmartTranscriber.Web/"]
RUN dotnet restore "Ppg.SmartTranscriber.Web/Ppg.SmartTranscriber.Web.csproj"

# Copy the rest of the code
COPY . .

# Set working directory
WORKDIR "/src/Ppg.SmartTranscriber.Web"

# Create Data Protection key directory and set permissions
RUN mkdir -p /root/.aspnet/DataProtection-Keys && chmod 700 /root/.aspnet/DataProtection-Keys

# Clean and build
RUN dotnet clean
RUN dotnet build "Ppg.SmartTranscriber.Web.csproj" -c Debug -o /app/build

# Expose ports
EXPOSE 80
EXPOSE 5000

# Start the app with hot reload
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://+:80"]

# Production stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Ppg.SmartTranscriber.Web/Ppg.SmartTranscriber.Web.csproj", "Ppg.SmartTranscriber.Web/"]
RUN dotnet restore "Ppg.SmartTranscriber.Web/Ppg.SmartTranscriber.Web.csproj"

# Copy the rest of the code
COPY . .

# Build and publish
WORKDIR "/src/Ppg.SmartTranscriber.Web"
RUN dotnet clean
RUN dotnet build "Ppg.SmartTranscriber.Web.csproj" -c Release -o /app/build
RUN dotnet publish "Ppg.SmartTranscriber.Web.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create Data Protection key directory and set permissions
RUN mkdir -p /root/.aspnet/DataProtection-Keys && chmod 700 /root/.aspnet/DataProtection-Keys

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80

ENTRYPOINT ["dotnet", "Ppg.SmartTranscriber.Web.dll"] 