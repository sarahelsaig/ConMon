using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConMon.Models
{
    public class RunAs : IRunAs
    {
        public string Domain { get; set; } = "";
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";

        public RunAs(IConfiguration config)
        {
            if (config != null)
            {
                config = config.GetSection(nameof(RunAs));
                Domain = config.GetValue(nameof(Domain), "");
                User = config.GetValue(nameof(User), "");
                Pass = config.GetValue(nameof(Pass), "");
            }
        }
    }
}
