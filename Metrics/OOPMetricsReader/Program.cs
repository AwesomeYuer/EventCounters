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
                                            {"EventCounterIntervalSec", "1"}
                                        }
                    )
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
                var payload = (IDictionary<string, object>) traceEvent.PayloadValue(0);

                var line = string.Join
                                        (
                                            ", "
                                            , payload
                                                    .Select
                                                        (
                                                            p =>
                                                            {
                                                                return
                                                                $"{p.Key}: {p.Value}";
                                                            }
                                                        )
                                        );
                if 
                    (
                        !line.Contains("Count:0")
                        //&&
                        //!line.Contains(@"-Rate""")
                    )
                {
                    //line = line.Replace(":", ":");
                    Console.WriteLine (line);
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
