using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConMon.Models.Scheduler
{
    public class Application
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [StringLength(50)]
        public string Label { get; set; }

        [StringLength(50)]
        public string Cron { get; set; } = "*/10 * * * *";

        [StringLength(500)]
        public string Program { get; set; }

        [StringLength(500)]
        public string Arguments { get; set; } = "";

        [StringLength(500)]
        public string WorkingDirectory { get; set; }


        [InverseProperty(nameof(ApplicationLine.Application))]
        public List<ApplicationLine> Lines { get; set; }
    }
}
