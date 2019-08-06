using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QueueLoader
{
    public static class StatusWatcher
    {
        private static ManagementClient managementClient;
        private static MessageReceiver receiver;
        private static ConcurrentDictionary<Guid, int> instanceIds = new ConcurrentDictionary<Guid, int>();

        [FunctionName("StatusWatcher")]
        public static async Task Run(
            [TimerTrigger("%watcherCron%")] TimerInfo myTimer,
            [Blob("%statusBlob%", FileAccess.Write, Connection = "storageConnectionString")]
            TextWriter blob,
            ILogger log)
        {
            log.LogInformation(Environment.GetEnvironmentVariable("watcherCron"));
            log.LogInformation(Environment.GetEnvironmentVariable("storageConnectionString"));
            log.LogInformation(Environment.GetEnvironmentVariable("sbConnectionString"));
            log.LogInformation(Environment.GetEnvironmentVariable("workQueue"));
            log.LogInformation(Environment.GetEnvironmentVariable("outputQueue"));

            var sbConnectionString = Environment.GetEnvironmentVariable("sbConnectionString");
            var workQueue = Environment.GetEnvironmentVariable("workQueue");
            var outputQueue = Environment.GetEnvironmentVariable("outputQueue");

            managementClient = managementClient ?? new ManagementClient(sbConnectionString);
            if (receiver == null)
            {
                receiver = new MessageReceiver(sbConnectionString, outputQueue, ReceiveMode.ReceiveAndDelete, RetryPolicy.NoRetry,
                    1000);
                receiver.RegisterMessageHandler(async (message, token) =>
                {
                    var instanceId = Guid.Parse(JObject.Parse(Encoding.UTF8.GetString(message.Body))
                        .Value<string>("instanceId"));
                    instanceIds.TryGetValue(instanceId, out var count);
                    instanceIds[instanceId] = count + 1;
                }, new MessageHandlerOptions(_ => Task.CompletedTask)
                {
                    AutoComplete = true,
                    MaxConcurrentCalls = 1000,
                });
            }

            var workDepth = (await managementClient.GetQueueRuntimeInfoAsync(workQueue))
                .MessageCountDetails.ActiveMessageCount;

            var status =
                new JObject
                {
                    ["workDepth"] = workDepth,
                    ["processors"] = new JArray(instanceIds.Select(kvp => new JObject
                    {
                        ["id"] = kvp.Key,
                        ["count"] = kvp.Value
                    }))
                }.ToString(Formatting.Indented);
            instanceIds.Clear();

            log.LogInformation(status);
            await blob.WriteAsync(status);
        }
    }
}