using System;
using System.Threading.Tasks;
using ConMon.Classes;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace System
{
    public static class JsonExtensions
    {
        public static JsonSerializerSettings JsonSerializerSettings { get; } = new JsonSerializerSettings();
        public static T JsonParse<T>(this string self) => JsonConvert.DeserializeObject<T>(self, JsonSerializerSettings);
    }

    public static class HangfireExtensions
    {
        public static string GetRecurringJobName(this IStorageConnection connection, string id) =>
            JsonConvert.DeserializeObject<string>(connection.GetJobParameter(id, "RecurringJobId") ?? "null");
    }
}

namespace ConMon.Controllers
{
    public static class ControllerExtensions
    {
        public static ActionResult<object> Attempt(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                return e;
            }
        }
        public static async Task<ActionResult<object>> AttemptAsync(Func<Task> action)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}

namespace Microsoft.Extensions.Configuration
{
    public static class ConMonConfigurationExtensions
    {
        public static ConnectionType GetConnectionType(this IConfiguration config)
        {
            string type = config.GetValue(nameof(ConnectionType), nameof(ConnectionType.SqlServer));
            return Enum.Parse<ConnectionType>(type, true);
        }
    }
}
