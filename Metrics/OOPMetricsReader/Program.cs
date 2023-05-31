using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace OOPMetricsReader;

class Program
{
    static void Main(string[] args)
    {
        // Find the process containing the target EventSource.
        var targetProcess = DiagnosticsClient
                                        .GetPublishedProcesses()
                                        .Select(Process.GetProcessById)
                                        .FirstOrDefault
                                            (
                                                (x) =>
                                                {
                                                    return
                                                        x.ProcessName.Contains(args[0], StringComparison.OrdinalIgnoreCase);
                                                }
                                            );
                                        
        if (targetProcess == null)
        {
            Console.WriteLine($"No process named '*{args[0]}*' found. Exiting.");
            return;
        }

        // Define what EventSource and events to listen to.
        var providers = new List<EventPipeProvider>()
        {
            new EventPipeProvider
                    (
                        "CommonMetricsEventSource"
                        , EventLevel.Verbose
                        , arguments: new Dictionary<string, string>
                                        {
                                            { "EventCounterIntervalSec", "1" }
                                        }
                    )
        };

        var countersCursorsTops = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "__", Console.CursorTop }
        };

        // Start listening session
        var client = new DiagnosticsClient(targetProcess.Id);
        using var session = client.StartEventPipeSession(providers, false);
        using var source = new EventPipeEventSource(session.EventStream);

        // Set up output writer
        source.Dynamic.All += traceEvent =>
        {
            if (traceEvent.EventName == "EventCounters")
            {
                var payloadValue = (IDictionary<string, object>) traceEvent.PayloadValue(0);
                IDictionary<string, object> payloadKeyValuePairs = (IDictionary<string, object>) payloadValue["Payload"];

                if (payloadKeyValuePairs.TryGetValue("Count", out var o))
                { 
                    var count = (int) o;
                    if (count <= 0)
                    {
                        return;
                    }
                }

                var counterName = (string) payloadKeyValuePairs["Name"];
                var counterDisplayName = (string) payloadKeyValuePairs["DisplayName"];
                var counterType = (string) payloadKeyValuePairs["CounterType"];
                var displayUnits = (string) payloadKeyValuePairs["DisplayUnits"];
                counterDisplayName = $"{counterDisplayName}({counterName})";

                var cursorTop = -1;

                if (!countersCursorsTops.TryGetValue(counterDisplayName, out cursorTop))
                {
                    cursorTop = countersCursorsTops
                                .Max
                                    (
                                        (x) =>
                                        {
                                            return
                                                x.Value;
                                        }
                                    ) + 1;
                    countersCursorsTops.Add(counterDisplayName, cursorTop);
                }

                double @value;
                var counterValue = string.Empty;
                if
                    (
                        counterType == "Mean"
                        &&
                        displayUnits == "ms/op"
                    )
                {
                    @value = (double) payloadKeyValuePairs["Mean"];
                }
                else if
                    (
                        counterType == "Sum"
                        &&
                        displayUnits == "ops/sec"
                    )
                {
                    @value = (double) payloadKeyValuePairs["Increment"];
                }
                else
                {
                    @value = (double) payloadKeyValuePairs["Max"];
                }
                
                //if (!string.IsNullOrEmpty(counterValue))
                {
                    var counterLabel = $"{DateTime.Now: yyyy-MM-dd HH:mm:ss.fffff} @ {counterDisplayName} @ :";
                    var counterValueText = $"{@value: #,##0.000,00} {displayUnits}";

                    Console.SetCursorPosition(0, cursorTop);
                    Console.Write("\r".PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
                    Console.Write(counterLabel);
                    Console.CursorLeft = Console.WindowWidth - counterValueText.Length - 50;
                    Console.WriteLine(counterValueText);
                }
                
            }
            else
            {
                Console.WriteLine($"{traceEvent.ProviderName}: {traceEvent.EventName}");
            }
        };

        try
        {
            source.Process();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error encountered while processing events");
            Console.WriteLine(e.ToString());
        }
        Console.ReadKey();
    }
}
