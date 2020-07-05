using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ConMon.Models.Scheduler
{
    public class SchedulerContext : DbContext
    {
        public DbSet<Application> Applications { get; set; }
        public DbSet<ApplicationLine> ApplicationLines { get; set; }

        public SchedulerContext([NotNullAttribute] DbContextOptions<SchedulerContext> options) : base(options) { }
    }
}
