using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "otel-service";
const string instrumentationScopeName = "otel-data-generator";

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(builder =>
    {
        builder.AddService(serviceName);
    })
    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
    .AddSource(instrumentationScopeName)
    .AddOtlpExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(builder =>
    {
        builder.AddService(serviceName);
    })
    .AddMeter(instrumentationScopeName)
    .AddOtlpExporter()
    .Build();

using var activitySource = new ActivitySource(instrumentationScopeName);
using var meter = new Meter(instrumentationScopeName);
var httpServerRequestDuration = meter.CreateHistogram<double>("http.server.request.duration", "s", "Duration of HTTP server requests.");

for (;;)
{
    /// transaction.name = WebTransaction/server/GET /Users
    /* span name = GET /Users       */ SimulateWebRequest("GET", "/Users", null);
    /* span name = Custom span name */ SimulateWebRequest("GET", "/Users", "Custom span name");

    /// transaction.name = WebTransaction/server/POST /Users
    /* span name = POST /Users      */ SimulateWebRequest("POST", "/Users", null);

    /// transaction.name = WebTransaction/server/GET
    /* span name = Custom span name */ SimulateWebRequest("GET", null, "Custom span name");

    /// transaction.name = WebTransaction/server/unknown
    /* span name = Foo              */ SimulateWebRequest(null, null, "Foo");
    /* span name = Bar              */ SimulateWebRequest(null, null, "Bar");
    /* span name =                  */ SimulateWebRequest(null, null, null);

    /// transaction.name = OtherTransaction/consumer/process//customers/{customerId}
    /* span name = /customers/{customerId} process */ SimulateMessagingOperation("process", "/customers/{customerId}", null, null);

    /// transaction.name = OtherTransaction/consumer/process/MyTopic
    /* span name = MyTopic process                 */ SimulateMessagingOperation("process", null, "MyTopic", null);
    /* span name = Custom span name                */ SimulateMessagingOperation("process", null, "MyTopic", "Custom span name");

    // transaction.name = OtherTransaction/consumer/unknown
    /* span name = /customers/{customerId}         */ SimulateMessagingOperation(null, "/customers/{customerId}", null, null);

    Thread.Sleep(500);
}

void SimulateWebRequest(string? httpMethod, string? httpRoute, string? spanNameOverride)
{
    var activityName = spanNameOverride == null
        ? string.Join(" ", httpMethod, httpRoute)
        : spanNameOverride;

    var tags = new TagList();
    AddAttribute(ref tags, "http.request.method", httpMethod);
    AddAttribute(ref tags, "http.route", httpRoute);
    AddAttribute(ref tags, "http.response.status_code", 200);

    var requestDuration = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 200)).TotalSeconds;

    ActivityContext parentContext = default;
    using var activity = activitySource.StartActivity(activityName, ActivityKind.Server, parentContext, tags);
    activity?.SetEndTime(activity.StartTimeUtc.AddSeconds(requestDuration));
    activity?.Stop();

    httpServerRequestDuration.Record(requestDuration, tags);
}

void SimulateMessagingOperation(string? operation, string? destinationTemplate, string? destinationName, string? spanNameOverride)
{
    var activityName = spanNameOverride == null
        ? string.Join(" ", destinationTemplate ?? destinationName, operation)
        : spanNameOverride;

    var tags = new TagList();
    AddAttribute(ref tags, "messaging.operation", operation);
    AddAttribute(ref tags, "messaging.destination.template", destinationTemplate);
    AddAttribute(ref tags, "messaging.destination.name", destinationName);

    var requestDuration = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 200)).TotalSeconds;

    ActivityContext parentContext = default;
    using var activity = activitySource.StartActivity(activityName, ActivityKind.Consumer, parentContext, tags);
    activity?.SetEndTime(activity.StartTimeUtc.AddSeconds(requestDuration));
    activity?.Stop();
}

void AddAttribute(ref TagList tags, string key, object? value)
{
    if (value != null)
    {
        tags.Add(new KeyValuePair<string, object?>(key, value));
    }
}
