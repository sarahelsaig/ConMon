using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConMon.Services
{
    public class ApplicationHost : IHostedService, IDisposable
    {
        public string Program { get; set; }
        public string[] Arguments { get; set; } = new string[0];
        private string _WorkingDirectory = null;
        public string WorkingDirectory { get => _WorkingDirectory ?? Environment.CurrentDirectory; set => WorkingDirectory = value; }
        
        public string Label { get; set; }
        public bool Running => process?.IsRunning() ?? false;
        public DateTime? NextRunTime { get; set; } = DateTime.MinValue;
        public int RerunSeconds { get; set; } = 0;

        public List<string> Buffer { get; set; } = new List<string>();

        public event DataReceivedEventHandler DataReceived;

        private Process process = null;
        private readonly DateTime? DateTimeNull = null;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!NextRunTime.HasValue) return;
            NextRunTime = RerunSeconds > 0 ? NextRunTime.Value.AddSeconds(RerunSeconds) : DateTimeNull;
            Buffer.Add($"Starting {this}");

            var process = new Process();
            process.StartInfo.FileName = Program;
            process.StartInfo.Arguments = string.Join(" ", Arguments);
            process.StartInfo.WorkingDirectory = WorkingDirectory;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += (o, e) => DataReceived?.Invoke(o, e);

            if (!process.StartInfo.FileName.Contains("\\") && !process.StartInfo.FileName.Contains("/"))
                process.StartInfo.FileName = System.IO.Path.Combine(WorkingDirectory, process.StartInfo.FileName);

            if (!cancellationToken.IsCancellationRequested)
            {
                process.Start();
                process.BeginOutputReadLine();
                await Task.Run(() => { process.WaitForExit(); }, cancellationToken);
                process.Close();
            }

            if (Buffer.Count > 100000) lock (Buffer) Buffer = Buffer.TakeLast(50000).ToList();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.Run(() => process?.Kill(), cancellationToken);

        public string ToJson() => JsonConvert.SerializeObject(new
        {
            label = Label,
            running = Running,
            enabled = NextRunTime.HasValue,
            next = NextRunTime,
            command = this.ToString(),
        });

        public override string ToString() => $"{Program} {string.Join(" ", Arguments)} ({WorkingDirectory ?? "."})";

        public void Dispose() => process?.Dispose();
    }
}
