using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ConMon.Classes
{
    public class ProgramAlias
    {
        public string Program { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }

        private ProgramAlias(IConfigurationSection section = null)
        {
            if (section == null) return;
            
            Program = section.GetValue<string>(nameof(Program), null);
            Arguments = section.GetValue<string>(nameof(Arguments), null);
            WorkingDirectory = section.GetValue<string>(nameof(WorkingDirectory), null);
        }

        public static Dictionary<string, ProgramAlias> DictionaryFromConfiguration(IConfiguration configuration) =>
            configuration.GetSection(nameof(ProgramAlias)).GetChildren().ToDictionary(x => x.Key, x => new ProgramAlias(x));
    }
}
