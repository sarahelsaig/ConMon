using Hangfire.Server;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
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


        private int Id { get; set; } = -1;
        public string Label { get; private set; }
        public string Cron { get; set; } = "*/10 * * * *";

        public string Program { get; set; }
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; }

        private Process process = null;
        public bool Running => process?.IsRunning() ?? false;
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
            var a = new ApplicationService(request.Label, null);

            a.Program = request.Program;
            a.Arguments = request.Arguments;
            a.WorkingDirectory = request.WorkingDirectory;
            a.Cron = request.Cron;
            a.SaveToDatabase();

            return a;
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


        private void BufferAdd(object sender, DataReceivedEventArgs e) { BufferAdd(e.Data); }
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

        public Task StartAsync() => StartAsync(new CancellationToken(false));
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            BufferAdd($"Starting {Program} {Arguments} ({WorkingDirectory ?? "."})");

            var process = new Process();
            process.StartInfo.FileName = Program;
            process.StartInfo.Arguments = Arguments;
            if (WorkingDirectory != null) process.StartInfo.WorkingDirectory = WorkingDirectory;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += BufferAdd;

            if (!process.StartInfo.FileName.Contains("\\") && !process.StartInfo.FileName.Contains("/"))
                process.StartInfo.FileName = System.IO.Path.Combine(
                    WorkingDirectory ?? Environment.CurrentDirectory, process.StartInfo.FileName);

            if (!cancellationToken.IsCancellationRequested)
            {
                process.Start();
                process.BeginOutputReadLine();
                await Task.Run(() => { process.WaitForExit(); }, cancellationToken);
                process.Close();
            }

            process = null;
        }
    }
}
