using Microsoft.EntityFrameworkCore;

namespace Ppg.SmartTranscriber.Data
{
    public class SmartTranscriberDbContext : DbContext
    {
        public DbSet<TranscriptionJob> TranscriptionJobs { get; set; } = default!;
        public DbSet<TranscriptionSegment> TranscriptionSegments => Set<TranscriptionSegment>();


        
        public SmartTranscriberDbContext(DbContextOptions<SmartTranscriberDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TranscriptionSegment>()
                .HasOne(s => s.Job)
                .WithMany(j => j.Segments)
                .HasForeignKey(s => s.JobId);

            modelBuilder.HasPostgresExtension("unaccent"); // optional for better search

            modelBuilder.Entity<TranscriptionSegment>()
                .HasGeneratedTsVectorColumn(
                    p => p.TextVector,
                    "english",   // Language
                    p => new { p.Text }) // Columns to index
                .HasIndex(p => p.TextVector)
                .HasMethod("GIN");
        }
    }
}