using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Metrics;

[Flags]
public enum CountersTypeFlags : byte
{
      None                      = 0b_0000_0000

    , ProcessDurationCounter    = 0b_0000_0001

    , ProcessCounter            = 0b_0000_0010
    , ProcessingCounter         = 0b_0000_0100
    , ProcessedCounter          = 0b_0000_1000
    
    , CountCounters             = 0b_0000_1110

    , ProcessRateCounter        = 0b_0001_0000
    , ProcessedRateCounter      = 0b_0010_0000

    , RateCounters              = 0b_0011_0000

    , Common                    = ProcessDurationCounter | ProcessedCounter | ProcessedRateCounter

    , ALL                       = 0b_0011_1111
}

public abstract class AbstractCommonMetricsEventSource : EventSource
{
    private class CounterContainer
    {
        public DiagnosticCounter? Counter;
        public long Count;
    }

    private Dictionary
                    <
                        string
                        ,
                        (
                              EventCounter      ProcessDurationCounter
                            , CounterContainer  ProcessCounter
                            , CounterContainer  ProcessingCounter
                            , CounterContainer  ProcessedCounter
                            , CounterContainer  ProcessRateCounter
                            , CounterContainer  ProcessedRateCounter
                        )
                    > _dynamicEventCounters = new (StringComparer.InvariantCultureIgnoreCase);
                        

    //public static readonly TimingMetricsEventSource Logger = new ();

    private object _locker = new object ();

    [NonEvent]
    public bool AddCounters(string countersNamePrefix, CountersTypeFlags countersTypeFlags = CountersTypeFlags.Common)
    {
        EventCounter processDurationCounter = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessDurationCounter))
        {
            var counterName = $"{countersNamePrefix}-Duration";
            processDurationCounter = new EventCounter(counterName, this)
            {
                  DisplayName = counterName
                , DisplayUnits = "ms/op"
            };
        }

        CounterContainer processCounterContainer = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessCounter))
        {
            var counterName = $"{countersNamePrefix}-Process-Count";
            var counter = new PollingCounter
                                        (
                                            counterName
                                            , this
                                            , () =>
                                            {
                                                return
                                                    _dynamicEventCounters
                                                        [countersNamePrefix]
                                                                    .ProcessCounter
                                                                    .Count;
                                            }
                                        )
            { 
                  DisplayName = counterName
                , DisplayUnits = "op"
            };
            processCounterContainer = new CounterContainer()
            {
                  Counter = counter
                , Count = 0
            };
        }

        CounterContainer processingCounterContainer = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessingCounter))
        {
            var counterName = $"{countersNamePrefix}-Processing-Count";
            var counter = new PollingCounter
                                        (
                                            counterName
                                            , this
                                            , () =>
                                            {
                                                return
                                                    _dynamicEventCounters
                                                        [countersNamePrefix]
                                                                    .ProcessingCounter
                                                                    .Count;
                                            }
                                        )
            {
                  DisplayName = counterName
                , DisplayUnits = "op"
            };
            processingCounterContainer = new CounterContainer()
            {
                  Counter = counter
                , Count = 0
            };
        }

        CounterContainer processedCounterContainer = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessedCounter))
        {
            var counterName = $"{countersNamePrefix}-Processed-Count";
            var counter = new PollingCounter
                                        (
                                            counterName
                                            , this
                                            , () =>
                                            {
                                                return
                                                    _dynamicEventCounters
                                                        [countersNamePrefix]
                                                                    .ProcessedCounter
                                                                    .Count;
                                            }
                                        )
            {
                  DisplayName = counterName
                , DisplayUnits = "op"
            };
            processedCounterContainer = new CounterContainer()
            {
                  Counter = counter
                , Count = 0
            };
        }


        CounterContainer processRateCounterContainer = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessRateCounter))
        {
            var counterName = $"{countersNamePrefix}-Process-Rate";
            var counter = new IncrementingPollingCounter
                                        (
                                            counterName
                                            , this
                                            , () =>
                                            {
                                                return
                                                    _dynamicEventCounters
                                                        [countersNamePrefix]
                                                                    .ProcessRateCounter
                                                                    .Count;
                                            }
                                        )
            {
                  DisplayName = counterName
                , DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                , DisplayUnits = "ops/sec"
            };
            processRateCounterContainer = new CounterContainer()
            {
                  Counter = counter
                , Count = 0
            };
        }

        CounterContainer processedRateCounterContainer = null!;
        if (countersTypeFlags.HasFlag(CountersTypeFlags.ProcessedRateCounter))
        {
            var counterName = $"{countersNamePrefix}-Processed-Rate";
            var counter = new IncrementingPollingCounter
                                        (
                                            counterName
                                            , this
                                            , () =>
                                            {
                                                return
                                                    _dynamicEventCounters
                                                        [countersNamePrefix]
                                                                    .ProcessedRateCounter
                                                                    .Count;
                                            }
                                        )
            {
                  DisplayName = counterName
                , DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                , DisplayUnits = "ops/sec"
            };
            processedRateCounterContainer = new CounterContainer()
            {
                  Counter = counter
                , Count = 0
            };
        }

        var r = _dynamicEventCounters
                                .TryAdd
                                        (
                                            countersNamePrefix
                                            ,
                                            (
                                                  processDurationCounter
                                                , processCounterContainer
                                                , processingCounterContainer
                                                , processedCounterContainer
                                                , processRateCounterContainer
                                                , processedRateCounterContainer
                                            )
                                        );
        return r;
    }


    [NonEvent]
    public void Counting(string countersNamePrefix, Action action, CountersTypeFlags countersTypeFlags = CountersTypeFlags.Common)
    {
        var startTimestamp = StartCounting(countersNamePrefix, countersTypeFlags);
        try
        {
            action();
        }
        finally
        {
            StopCounting(countersNamePrefix, startTimestamp, countersTypeFlags);        
        }
    }

    [NonEvent]
    public async Task CountingAsync(string countersNamePrefix, Func<Task> action, CountersTypeFlags countersTypeFlags = CountersTypeFlags.Common)
    {
        var startTimestamp = StartCounting(countersNamePrefix, countersTypeFlags);
        try
        {
            await action();
        }
        finally
        {
            StopCounting(countersNamePrefix, startTimestamp, countersTypeFlags);
        }
    }


    [NonEvent]
    public long StartCounting(string countersNamePrefix, CountersTypeFlags countersTypeFlags = CountersTypeFlags.Common)
    {
        var startTimestamp = -1L;
        if (IsEnabled())
        {
            Console.WriteLine("enabled");
            
            if (!_dynamicEventCounters.TryGetValue(countersNamePrefix, out var counters))
            {
                lock (_locker)
                {
                    AddCounters(countersNamePrefix, countersTypeFlags);
                }
                counters = _dynamicEventCounters[countersNamePrefix];
            }
            
            if (counters.ProcessDurationCounter is not null)
            {
                startTimestamp = Stopwatch.GetTimestamp();
            }

            if (counters.ProcessCounter is not null)
            {
                Interlocked.Increment(ref counters.ProcessCounter.Count);
            }

            if (counters.ProcessingCounter is not null)
            {
                Interlocked.Increment(ref counters.ProcessingCounter.Count);
            }

            if (counters.ProcessRateCounter is not null)
            {
                Interlocked.Increment(ref counters.ProcessRateCounter.Count);
            }
            
            return startTimestamp;
        }
        Console.WriteLine("not enabled");
        return 0;
    }

    [NonEvent]
    public void StopCounting(string countersNamePrefix, long startTimestamp, CountersTypeFlags countersTypeFlags = CountersTypeFlags.Common)
    {
        if (IsEnabled())
        {
            if (!_dynamicEventCounters.TryGetValue(countersNamePrefix, out var counters))
            {
                lock (_locker)
                {
                    AddCounters(countersNamePrefix, countersTypeFlags);
                }
                counters = _dynamicEventCounters[countersNamePrefix];
            }

            if (counters.ProcessDurationCounter is not null)
            {
                var endTimestamp = Stopwatch.GetTimestamp();
                var duration = new TimeSpan(endTimestamp - startTimestamp).TotalMilliseconds;
                counters.ProcessDurationCounter.WriteMetric(duration);
            }

            if (counters.ProcessedCounter is not null)
            {
                Interlocked.Increment(ref counters.ProcessedCounter.Count);
            }

            if (counters.ProcessingCounter is not null)
            {
                var l = Interlocked.Decrement(ref counters.ProcessingCounter.Count);
                if (l < 0)
                {
                    Interlocked.Exchange(ref counters.ProcessingCounter.Count, 0);
                }
            }

            if (counters.ProcessedRateCounter is not null)
            {
                Interlocked.Increment(ref counters.ProcessedRateCounter.Count);
            }
        }
    }
    protected override void Dispose(bool disposing)
    {
        foreach (var key in _dynamicEventCounters.Keys)
        {
            if 
                (
                    _dynamicEventCounters.Remove(key, out var removed)
                )
            {
                DiagnosticCounter counter = removed.ProcessDurationCounter;
                counter?.Dispose();
                                        
                counter = removed.ProcessCounter?.Counter!;
                counter?.Dispose();

                counter = removed.ProcessingCounter?.Counter!;
                counter?.Dispose();

                counter = removed.ProcessedCounter?.Counter!;
                counter?.Dispose();

                counter = removed.ProcessRateCounter?.Counter!;
                counter?.Dispose();

                counter = removed.ProcessedRateCounter?.Counter!;
                counter?.Dispose();
            }
        }
        _dynamicEventCounters.Clear();
        _dynamicEventCounters = null!;
        base.Dispose(disposing);
    }
}