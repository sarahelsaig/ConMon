using Hangfire.Storage;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;

namespace System
{
    public static class ProcessExtensions
    {
        public static bool IsRunning(this Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            try
            {
                Process.GetProcessById(process.Id);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }
    }

    public static class JsonExtensions
    {
        public static JsonSerializerSettings JsonSerializerSettings { get; private set; } = new JsonSerializerSettings();
        public static T JsonParse<T>(this string self) => JsonConvert.DeserializeObject<T>(self, JsonSerializerSettings);
    }

    public static class HangfireExtensions
    {
        public static string GetRecurringJobName(this IStorageConnection connection, string id) =>
            JsonConvert.DeserializeObject<string>(connection.GetJobParameter(id, "RecurringJobId") ?? "null");
    }

    public static class SqlExtensions
    {
        public static SqlCommand CreateCommand(this SqlConnection connection, string commandText, Dictionary<string, object> parameters = null)
        {
             if (connection.State != Data.ConnectionState.Open) connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = commandText;
            if (parameters != null)
                foreach(var x in parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = x.Key;
                    param.Value = x.Value;
                    cmd.Parameters.Add(param);
                }
            return cmd;
        }

        public static string[] ReadLineAndClose(this SqlCommand command)
        {
            using (command)
            {
                using (var reader = command.ExecuteReader())
                    if (reader.Read())
                    {
                        var ret = new object[reader.FieldCount];
                        reader.GetValues(ret);
                        return ret.Select(x => x.ToString()).ToArray();
                    }
                    else
                        return null;
            }
        }

        public static List<string> ReadScalarsAndClose(this SqlCommand command)
        {
            var ret = new List<string>();

            using (command)
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        ret.Add(reader.GetValue(0).ToString());

            return ret;
        }

        public static List<(T1, T2)> ReadTupleAndClose<T1, T2>(this SqlCommand command)
        {
            var ret = new List<(T1, T2)>();

            using (command)
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        ret.Add((
                            (T1)reader.GetValue(0),
                            (T2)reader.GetValue(1)
                            ));

            return ret;
        }


        public static void RunAndClose(this SqlCommand command)
        {
            using (command) command.ExecuteNonQuery();
        }
    }
}
