using ConMon.Classes;

namespace ConMon.Models
{
    public class ScheduleAddRequest
    {
        public string Program { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; }

        public string Label { get; set; }
        public string Cron { get; set; } = "*/10 * * * *";

        public void ApplyAlias(ProgramAlias alias)
        {
            if (!string.IsNullOrWhiteSpace(alias.Program))
                Program = alias.Program;
            if (!string.IsNullOrWhiteSpace(alias.Arguments))
                Arguments = string.IsNullOrWhiteSpace(Arguments) ? alias.Arguments :  $"{alias.Arguments} {Arguments}";
            if (!string.IsNullOrWhiteSpace(alias.WorkingDirectory) && string.IsNullOrWhiteSpace(WorkingDirectory))
                WorkingDirectory = alias.WorkingDirectory;
        }
    }
}
