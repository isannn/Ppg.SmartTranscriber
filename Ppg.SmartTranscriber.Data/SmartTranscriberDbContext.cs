using Microsoft.EntityFrameworkCore;

namespace Ppg.SmartTranscriber.Data
{
    public class SmartTranscriberDbContext : DbContext
    {
        public SmartTranscriberDbContext(DbContextOptions<SmartTranscriberDbContext> options)
            : base(options)
        {
        }

        public DbSet<TranscriptionJob> TranscriptionJobs { get; set; } = default!;
    }
}