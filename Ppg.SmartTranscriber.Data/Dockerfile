FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /src

# Copy project files
COPY ["Ppg.SmartTranscriber.Data/Ppg.SmartTranscriber.Data.csproj", "Ppg.SmartTranscriber.Data/"]
COPY ["Ppg.SmartTranscriber.Services/Ppg.SmartTranscriber.Services.csproj", "Ppg.SmartTranscriber.Services/"]

# Restore packages
RUN dotnet restore "Ppg.SmartTranscriber.Data/Ppg.SmartTranscriber.Data.csproj"

# Copy the rest of the code
COPY . .

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Set the working directory
WORKDIR "/src/Ppg.SmartTranscriber.Data"

# The command to run migrations
ENTRYPOINT ["dotnet", "ef", "database", "update", "--connection", "Host=postgres;Port=5432;Database=db_transcription;Username=postgres;Password=postgres"]