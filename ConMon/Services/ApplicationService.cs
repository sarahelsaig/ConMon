using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using ConMon.Models;
using ConMon.Models.Scheduler;
using Humanizer;

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

        private int FindIdByLabel(string label) =>
            _db.Applications.Where(x => x.Label == label).Select(x => x.Id).SingleOrDefault();

        private IQueryable<ApplicationLine> GetLines(int id, int after = -1) => 
            _db.ApplicationLines.Where(x => x.ApplicationId == id && x.Id > after).OrderBy(x => x.Id).Take(1000);

        private void AddLine(int id, params string[] lines) => 
            _db.ApplicationLines.Add(new ApplicationLine { ApplicationId = id, Line = string.Join(" ", lines) });

        private void ClearLines(int id) => 
            _db.ApplicationLines.RemoveRange(_db.ApplicationLines.Where(x => x.ApplicationId == id));
        #endregion

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
                await _db.Applications.AddAsync(application, cancellationToken);
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
            var startTime = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(_runas?.User))
            {
                AddLine(id, "WARNING: RUNAS IS CURRENTLY NOT SUPPORTED!! Please remove it from your app settings!");
                //var password = new System.Security.SecureString();
                //foreach (var c in RunAsPass) password.AppendChar(c);

                // process.StartInfo.Domain = _runas.Domain;
                // process.StartInfo.UserName = _runas.User;
                // process.StartInfo.PasswordInClearText = _runas.Pass;
                // process.StartInfo.Verb = "runas";
            }

            var program = ResolveToken(application.Program);
            var arguments = ResolveToken(application.Arguments);
            var workingDirectory = ResolveToken(application.WorkingDirectory);
            if (!Directory.Exists(workingDirectory)) workingDirectory = 
                Path.GetDirectoryName(program) ?? Environment.CurrentDirectory;

            try
            {
                await Cli.Wrap(program)
                    .WithArguments(arguments)
                    .WithWorkingDirectory(workingDirectory)
                    .Observe(cancellationToken)
                    .ForEachAsync(commandEvent =>
                    {
                        switch (commandEvent)
                        {
                            case StartedCommandEvent started:
                                AddLine(id, $"Starting {program} {arguments}",
                                    //$"({_runas?.User ?? Environment.UserName} @ {workingDirectory})");
                                    $"(#{started.ProcessId}; {Environment.UserName} @ {workingDirectory})");
                                break;
                            case StandardOutputCommandEvent output: AddLine(id, output.Text); break;
                            case StandardErrorCommandEvent error: AddLine(id, error.Text); break;
                            case ExitedCommandEvent exited:
                                var endTime = DateTime.Now;
                                var endTimeText = endTime.ToString("s").Replace('T', ' '); 
                                var statusText = exited.ExitCode == 0 ? "success" : $"error ({exited.ExitCode})";
                                AddLine(id, $"Application run for {(endTime - startTime).Humanize()}",
                                    $"and was terminated at {endTimeText} with ${statusText}."); 
                                break;
                        }

                        _db.SaveChanges();
                    }, cancellationToken);
            }
            catch (Exception e)
            {
                AddLine(id, e.ToString());
            }

            await _db.SaveChangesAsync(cancellationToken);
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

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private void ValidString(string text, string name)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(name);
        }
        #endregion
    }
}
