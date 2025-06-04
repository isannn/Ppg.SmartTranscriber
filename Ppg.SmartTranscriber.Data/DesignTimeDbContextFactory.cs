using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ppg.SmartTranscriber.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartTranscriberDbContext>
{
    public SmartTranscriberDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SmartTranscriberDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=db_transcription;Username=postgres;Password=postgres");

        return new SmartTranscriberDbContext(optionsBuilder.Options);
    }
} 