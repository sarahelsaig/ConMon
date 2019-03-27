using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConMon.Models
{
    public class ScheduleAddRequest
    {
        public string Program { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; }

        public string Label { get; set; }
        public string Cron { get; set; } = "*/10 * * * *";
    }
}
