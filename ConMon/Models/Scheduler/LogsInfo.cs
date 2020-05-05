using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConMon.Models.Scheduler
{
    public class LogsInfo
    {
        public DirectoryInfo Directory { get; private set; }
        public Dictionary<string, string[]> Groups { get; private set; } = new Dictionary<string, string[]>();

        public static LogsInfo Create(string path)
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) return new LogsInfo();

            var groups = dir.GetFiles()
                .GroupBy(x => x.Extension.TrimStart('.'))
                .ToDictionary(g => g.Key, g => g.Select(fi => fi.Name).ToArray());
            return new LogsInfo
            {
                Directory = dir,
                Groups = groups
            };
        }
    }
}