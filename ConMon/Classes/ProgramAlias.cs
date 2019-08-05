using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConMon.Classes
{
    public class ProgramAlias
    {
        public string Program { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }

        public ProgramAlias() { }
        private ProgramAlias(IConfigurationSection section)
        {
            Program = section.GetValue<string>(nameof(Program), null);
            Arguments = section.GetValue<string>(nameof(Arguments), null);
            WorkingDirectory = section.GetValue<string>(nameof(WorkingDirectory), null);
        }

        public static Dictionary<string, ProgramAlias> Config(IConfiguration configuration) =>
            configuration.GetSection("ProgramAlias").GetChildren().ToDictionary(x => x.Key, x => new ProgramAlias(x));
    }
}
