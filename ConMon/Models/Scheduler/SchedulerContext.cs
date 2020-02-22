using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace ConMon.Models.Scheduler
{
    public class SchedulerContext : DbContext
    {
        public DbSet<Application> Applications { get; set; }
        public DbSet<ApplicationLine> ApplicationLines { get; set; }

        public SchedulerContext([NotNullAttribute] DbContextOptions<SchedulerContext> options) : base(options) { }
    }
}
