using ConMon.Models;
using ConMon.Models.Scheduler;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ConMon.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly SchedulerContext _db;
        private readonly IRunAs _runas;

        public ApplicationService(SchedulerContext db, IRunAs runas)
        {
            _db = db;
            _runas = runas;
        }

        #region database manipulation
        public Application FindByLabel(string label) => _db.Applications.SingleOrDefault(x => x.Label == label);
        int FindIdByLabel(string label) => _db.Applications.Where(x => x.Label == label).Select(x => x.Id).SingleOrDefault();
        IQueryable<ApplicationLine> GetLines(int id, int after = -1) => _db.ApplicationLines.Where(x => x.ApplicationId == id && x.Id > after).OrderBy(x => x.Id).Take(1000);
        void AddLine(int id, string line) => _db.ApplicationLines.Add(new ApplicationLine { ApplicationId = id, Line = line });
        void ClearLines(int id) => _db.ApplicationLines.RemoveRange(_db.ApplicationLines.Where(x => x.ApplicationId == id));
        #endregion

        private void ValidString(string text, string name)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(name);
        }

        #region public API
        public void BufferClear(string label)
        {
            ValidString(label, nameof(label));
            ClearLines(FindIdByLabel(label));
            _db.SaveChanges();
        }

        public (int, IEnumerable<string>) BufferGet(string label, int after = -1)
        {
            ValidString(label, nameof(label));
            var ret = GetLines(FindIdByLabel(label), after).Select(x => new { x.Id, x.Line }).ToList();
            return ret.Count > 0 ? (ret.Max(x => x.Id), ret.Select(x => x.Line)) : (after, Array.Empty<string>());
        }

        public async Task CreateAsync(ScheduleAddRequest request, CancellationToken? optionalCancellationToken)
        {
            ValidString(request.Label, "request.Label");
            var cancellationToken = optionalCancellationToken ?? new CancellationToken(false);

            var application = FindByLabel(request.Label);
            if (application is null)
            {
                application = new Application
                {
                    Label = request.Label,
                    Program = request.Program,
                    Arguments = request.Arguments ?? "",
                    WorkingDirectory = request.WorkingDirectory,
                };
                if (!string.IsNullOrWhiteSpace(request.Cron)) application.Cron = request.Cron;
                _db.Applications.Add(application);
            }
            else
            {
                application.Label = request.Label;
                application.Program = request.Program;
                application.Arguments = request.Arguments ?? "";
                application.WorkingDirectory = request.WorkingDirectory;
                if (!string.IsNullOrWhiteSpace(request.Cron)) application.Cron = request.Cron;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task StartAsync(string label, CancellationToken? optionalCancellationToken)
        {
            ValidString(label, nameof(label));
            var cancellationToken = optionalCancellationToken ?? new CancellationToken(false);
            
            var application = FindByLabel(label);
            var id = application.Id;

            // Log start
            bool hasDirectory = !string.IsNullOrWhiteSpace(application.WorkingDirectory);
            AddLine(id, $"Starting {application.Program} {application.Arguments} " +
                $"({_runas?.User ?? Environment.UserName} @ {(hasDirectory ? application.WorkingDirectory : ".")})");


            var process = CreateProcess(application, hasDirectory ? application.WorkingDirectory : Environment.CurrentDirectory);

            if (!string.IsNullOrWhiteSpace(_runas?.User))
            {
                //var password = new System.Security.SecureString();
                //foreach (var c in RunAsPass) password.AppendChar(c);

                process.StartInfo.Domain = _runas.Domain;
                process.StartInfo.UserName = _runas.User;
                process.StartInfo.PasswordInClearText = _runas.Pass;
                process.StartInfo.Verb = "runas";
            }

            process.OutputDataReceived += (o, e) => BufferAdd(id, e);
            process.ErrorDataReceived += (o, e) => BufferAdd(id, e);

            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    await Task.Run(() => { process.WaitForExit(); }, cancellationToken);

                    while (!process.HasExited)
                    {
                        string line = process.StandardOutput.ReadLine() ?? process.StandardError.ReadLine();
                        if (line != null) AddLine(id, line ?? ""); else break;
                        await _db.SaveChangesAsync(cancellationToken);
                    }

                    process.Close();
                }
            }
            catch (Exception e)
            {
                AddLine(id, e.ToString());
            }

            await _db.SaveChangesAsync(cancellationToken);
            process = null;
        }
        #endregion

        #region Auxiliary functions
        private static string ResolveToken(string txt)
        {
            if (txt?.Contains("::last::") == true)
                txt = ResolveTokenLast(txt);

            return txt;
        }

        private static string ResolveTokenLast(string input)
        {
            const string pattern = @"<([^<]*::last::[^<]*)>";
            var parts = Regex.Match(input, pattern).Groups[1].Value
                .Split(new[] { "::last::" }, StringSplitOptions.None);
            while (string.IsNullOrWhiteSpace(parts.Last()))
                parts = parts.Take(parts.Length - 1).ToArray();

            var targets = Directory.GetDirectories(parts[0]);
            if (parts.Length == 2 && !parts[1].Contains("\\"))
                targets = targets.Concat(Directory.GetFiles(parts[0])).ToArray();
            var selected = targets.Last();

            var result = parts.Length == 1 ? selected :
                parts.Length == 2 ? selected + parts[1] :
                ResolveTokenLast(selected + string.Join("::last::", parts.Skip(1)));

            return Regex.Replace(input, pattern, result);
        }

        private Process CreateProcess(Application application, string actualWorkingDirectory) =>
            new Process()
            {
                StartInfo = new ProcessStartInfo(ResolveToken(application.Program), ResolveToken(application.Arguments))
                {
                    WorkingDirectory = ResolveToken(actualWorkingDirectory),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

        private void BufferAdd(int id, DataReceivedEventArgs e)
        {
            try
            {
                AddLine(id, string.IsNullOrWhiteSpace(e.Data) ? "" : e.Data);
            }
            catch (Exception ex)
            {
                AddLine(id, ex.ToString());
            }
        }
        #endregion
    }
}
