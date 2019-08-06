using Hangfire.Server;
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
    public class ApplicationService
    {
        public const string SqlFindLabels = "select Label from Application";
        public const string SqlFindByLabel = "select Id,Cron,Program,Arguments,WorkingDirectory from Application where Label = @Label";
        public const string SqlCreateFromLabel = "insert into Application(Label,Cron,Program,Arguments,WorkingDirectory) values(@Label, '*/10 * * * *', NULL, '', NULL)";
        public const string SqlUpdate = "update Application set Cron=@Cron,Program=@Program,Arguments=@Arguments,WorkingDirectory=@WorkingDirectory where Id = @Id";
        public const string SqlLines = "select top 1000 Id, Line from ApplicationLine where ApplicationId = @Id and Id > @after order by Id desc";
        public const string SqlUpdateLines = "insert into ApplicationLine(ApplicationId, Line) values(@Id, @line)";
        public const string SqlDeleteLines = "delete from ApplicationLine where ApplicationId = @Id";


        public static string ConnectionString { get; set; }
        private string InstanceConnectionString = null;

        public static string RunAsDomain = "";
        public static string RunAsUser = null;
        public static string RunAsPass = null;

        private int Id { get; set; } = -1;
        public string Label { get; private set; }
        public string Cron { get; set; } = "*/10 * * * *";

        string _Program, _Arguments = "", _WorkingDirectory;
        public string Program
        {
            get => _Program;
            set { _Program = value; if (_Program.Contains("::last::")) _Program = ResolveTokenLast(_Program); }
        }
        public string Arguments
        {
            get => _Arguments;
            set { _Arguments = value; if (_Arguments.Contains("::last::")) _Arguments = ResolveTokenLast(_Arguments); }
        }
        public string WorkingDirectory
        {
            get => _WorkingDirectory;
            set { _WorkingDirectory = value; if (_WorkingDirectory.Contains("::last::")) _WorkingDirectory = ResolveTokenLast(_WorkingDirectory); }
        }

        private bool Initialized = false;


        private Dictionary<string, object> LabelAsParams => new Dictionary<string, object> { { nameof(Label), Label } };
        private Dictionary<string, object> Params(params object[] data)
        {
            var ret = new Dictionary<string, object>();
            for (int i = 0; i < data.Length; i += 2)
                ret[data[i].ToString()] = data[i + 1];
            return ret;
        }


        public static List<ApplicationService> GetServices(string connectionString = null)
        {
            using (var connection = new SqlConnection(connectionString ?? ConnectionString))
                return connection.CreateCommand(SqlFindLabels)
                    .ReadScalarsAndClose()
                    .Select(label => new ApplicationService(label, ConnectionString))
                    .ToList();
        }

        public static ApplicationService FromRequest(Models.ScheduleAddRequest request)
        {
            var service = new ApplicationService(request.Label, null)
            {
                Program = request.Program,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                Cron = request.Cron
            };

            service.SaveToDatabase();
            return service;
        }

        public ApplicationService() { InstanceConnectionString = ConnectionString; }
        public ApplicationService(string label, string connectionString = null) : this() { Initialize(label, connectionString); }

        public void Initialize(string label, string connectionString = null)
        {
            Label = label;
            InstanceConnectionString = connectionString ?? ConnectionString;
            using (var connection = new SqlConnection(InstanceConnectionString))
                while (Id < 0)
                {
                    var row = connection.CreateCommand(SqlFindByLabel, LabelAsParams).ReadLineAndClose();
                    if (row is null)
                        using (var cmd = connection.CreateCommand(SqlCreateFromLabel, LabelAsParams)) cmd.ExecuteNonQuery();
                    else
                    {
                        Id = int.Parse(row[0]);
                        Cron = row[1];
                        Program = row[2];
                        Arguments = row[3];
                        WorkingDirectory = row[4];
                    }
                }
            Initialized = true;
        }

        public void SaveToDatabase()
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
                connection.CreateCommand(SqlUpdate, Params(
                    nameof(Id), Id,
                    nameof(Cron), Cron,
                    nameof(Program), Program,
                    nameof(Arguments), Arguments,
                    nameof(WorkingDirectory), WorkingDirectory))
                    .RunAndClose();
        }


        private void BufferAdd(object sender, DataReceivedEventArgs e)
        {
            try
            {
                BufferAdd(e.Data);
            }
            catch (Exception ex)
            {
                BufferAdd(ex.ToString());
            }
        }

        private void BufferAdd(string line)
        {
            if (line is null) line = string.Empty;
            using (var connection = new SqlConnection(InstanceConnectionString))
                connection.CreateCommand(SqlUpdateLines, Params(nameof(Id), Id, nameof(line), line)).RunAndClose();
        }

        public (int, IEnumerable<string>) BufferGet(int after)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                var ret = connection.CreateCommand(SqlLines, Params(nameof(Id), Id, nameof(after), after)).ReadTupleAndClose<int, string>();
                return ret.Count > 0 ? (ret.Max(x => x.Item1), ret.OrderBy(x => x.Item1).Select(x => x.Item2)) : (after, new string[0]);
            }
        }

        public void BufferClear()
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
                connection.CreateCommand(SqlDeleteLines, Params(nameof(Id), Id)).RunAndClose();
        }

        public void Start(PerformContext context)
        {
            if (!Initialized)
                Initialize(context.Connection.GetRecurringJobName(context.BackgroundJob.Id));
            StartAsync(new CancellationToken(false)).Wait();
        }

        public static string ResolveTokenLast(string input)
        {
            const string pattern = @"<([^<]*::last::[^<]*)>";
            var parts = Regex.Match(input, pattern).Groups[1].Value
                .Split(new [] { "::last::" }, StringSplitOptions.None);
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

        public Task StartAsync() => StartAsync(new CancellationToken(false));
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            bool hasDirectory = !string.IsNullOrWhiteSpace(WorkingDirectory);
            string user = string.IsNullOrWhiteSpace(RunAsUser) ? Environment.UserName : RunAsUser;
            BufferAdd($"Starting {Program} {Arguments} ({user} @ {(hasDirectory ? WorkingDirectory : ".")})");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo(Program, Arguments)
                {
                    WorkingDirectory = hasDirectory ? WorkingDirectory : "",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            if (RunAsUser != null && RunAsPass != null)
            {
                //var password = new System.Security.SecureString();
                //foreach (var c in RunAsPass) password.AppendChar(c);

                process.StartInfo.Domain = RunAsDomain;
                process.StartInfo.UserName = RunAsUser;
                process.StartInfo.PasswordInClearText = RunAsPass;
                process.StartInfo.Verb = "runas";
            }

            process.OutputDataReceived += BufferAdd;
            process.ErrorDataReceived += BufferAdd;

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
                        if (line != null) BufferAdd(line); else break;
                    }

                    process.Close();
                }
            }
            catch (Exception e)
            {
                BufferAdd(e.ToString());
            }

            process = null;
        }
    }
}
