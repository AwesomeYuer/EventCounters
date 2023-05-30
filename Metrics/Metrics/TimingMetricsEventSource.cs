using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Metrics;

[EventSource(Name = "Simple-Timing-Metrics-EventSource")]
public sealed class TimingMetricsEventSource : EventSource
{
    private IDictionary<string, EventCounter> _dynamicEventCounters =
                        new Dictionary<string, EventCounter>(StringComparer.OrdinalIgnoreCase);

    public static readonly TimingMetricsEventSource Logger = new ();

    private static object _locker = new object ();

    [NonEvent]
    public void AddEventCounters(params EventCounter[] eventCounters)
    { 

        foreach (var counter in eventCounters) 
        {
            lock (_locker)
            {
                _ = _dynamicEventCounters.TryAdd(counter.Name, counter);
            }
        }
    }

    [NonEvent]
    public void AddEventCounters(params string[] eventCountersNames)
    {
        foreach (var counterName in eventCountersNames)
        {
            _ = _dynamicEventCounters
                        .TryAdd
                                (
                                    counterName
                                    , new EventCounter(counterName, this)
                                    {
                                           DisplayName = counterName
                                         , DisplayUnits = "ms/op"
                                    }
                                );
        }
    }

    [NonEvent]
    public void Timing(string eventCounterName, Action action)
    {
        if (IsEnabled())
        {
            var start = Stopwatch.GetTimestamp();
            action();
            StopTiming(eventCounterName, start);
        }
    }

    [NonEvent]
    public long StartTiming()
    {
        if (IsEnabled())
        {
            Console.WriteLine("enabled");
            return Stopwatch.GetTimestamp();
        }
        Console.WriteLine("not enabled");
        return 0;
    }

    [NonEvent]
    public void StopTiming(string eventCounterName, long startTimestamp)
    {
        if (IsEnabled())
        {
            var end = Stopwatch.GetTimestamp();
            var metric = new TimeSpan(end - startTimestamp).TotalMilliseconds;
            EventCounter eventCounter;
            if (!_dynamicEventCounters.TryGetValue(eventCounterName, out eventCounter!))
            {
                eventCounter = new EventCounter(eventCounterName, this)
                {
                    DisplayName = eventCounterName
                    , DisplayUnits = "ms/op"
                };
                lock (_locker)
                {
                    _ = _dynamicEventCounters.TryAdd(eventCounterName, eventCounter);
                }
            }
            eventCounter.WriteMetric(metric);
        }
    }
}