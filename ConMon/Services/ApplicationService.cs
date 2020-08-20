using CliWrap;
using CliWrap.EventStream;
using ConMon.Models;
using ConMon.Models.Scheduler;
using Humanizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
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
            if (!Directory.Exists(workingDirectory))
                workingDirectory =
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
                            case StandardOutputCommandEvent output:
                                AddLine(id, output.Text);
                                break;
                            case StandardErrorCommandEvent error:
                                AddLine(id, error.Text);
                                break;
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

        public static string ResolveTokenLast(string input)
        {
            const string token = "::last::";
            if (!input.Contains(token)) return input;

            // Find the replace pattern in the text, split the inside into parts along the ::last:: token.
            var match = Regex.Match(input, @"<([^<]*::last::[^<]*)>");
            var pattern = match.Groups[0].Value;
            var inner = match.Groups[1].Value.Trim();
            if (inner.StartsWith(token)) inner = $".{Path.DirectorySeparatorChar}{inner}";

            string Inner(string prefix, string subValue)
            {
                if (!subValue.Contains(token)) return subValue;
                if (subValue.Trim().EndsWith(token))
                {
                    throw new InvalidOperationException("The '::last::' token must refer to a directory. If you are " +
                                                        "looking for the last directory in the given path, use a " +
                                                        "trailing slash! (eg. '::last::/')");
                }

                var parts = subValue.Split(new[] { token }, StringSplitOptions.None);
                while (parts.Length > 0 && string.IsNullOrWhiteSpace(parts.Last()))
                {
                    parts = parts.Take(parts.Length - 1).ToArray();
                }

                // Select the alphabetically last directory. If there is only one part, return selected.
                if (parts.Length == 0) parts = new[] { "." };
                var selected = Directory.GetDirectories(prefix + parts[0]).OrderByDescending(x => x).First();
                if (parts.Length == 1) return selected;

                // Attach selected to the rest. Recurse if the remainder had more tokens.
                var remainder = (parts.Length == 2 ? parts[1] : Inner(selected, string.Join(token, parts.Skip(1))))
                    .TrimStart('/')
                    .TrimStart('\\');
                return parts.Length == 2 ? Path.Combine(selected, remainder) : remainder;
            }

            var result = Inner(string.Empty, inner);
            return input.Replace(pattern, result);
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