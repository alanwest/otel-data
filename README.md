# OpenTelemetry Data

This is a simple application that generates data and sends it to New Relic over
OTLP.

To run the application:

1. Modify the `NEW_RELIC_API_KEY` and `NEW_RELIC_OTLP_ENDPOINT` environment
   variables in the [`.env`](./.env) file as necessary.

2. Then run:

```shell
docker compose up --build
```

The application simulates web requests and messaging operations. It
demonstrates a number of scenarios where either the telemetry conforms or does
not conform to the OpenTelemetry semantic conventions.

## Background

New Relic’s APM experience is centered around the notion of a [transaction](https://docs.newrelic.com/docs/apm/transactions/intro-transactions/transactions-new-relic-apm/).
This is true even for services that are instrumented with OpenTelemetry despite
OpenTelemetry not having a direct analog to New Relic’s notion of a
transaction. New Relic leverages OpenTelemetry’s [semantic conventions](https://opentelemetry.io/docs/concepts/semantic-conventions/)
to drive its transaction-centric APM experience.

Each transaction has a name. For example, when using New Relic’s APM agents to
instrument a web application, a transaction is created for each request
received by the application. The agent usually names the transaction by the
route invoked by the request or other identifying characteristics of the
request - e.g., HTTP method or response code.

Although OpenTelemetry instrumentation does not create transactions, it does
emit telemetry with similar identifying characteristics. For an HTTP service,
OpenTelemetry instrumentation emits the [`http.server.request.duration`](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/#metric-httpserverrequestduration)
metric for measuring the duration of web requests. The HTTP semantic
conventions require a number of attributes be present on the
`http.server.request.duration` metric. We leverage the `http.request.method`
and `http.route` attributes to derive a transaction name for the purpose of
driving the New Relic APM experience.

OpenTelemetry HTTP instrumentation also emits span data following the same HTTP
semantic conventions. Notably, the span generated for a request uses the same
`http.request.method` and `http.route` attributes found on the
`http.server.request.duration` metric. This symmetry between metric and span
data enables us to correlate the transactions we derive from the
`http.server.request.duration` metric to transaction traces we derive from span
data. We add a `transaction.name` attribute to the root span of a request to
facilitate this correlation.

In short, we drive our APM experience by deriving transaction names based on
metric data described by OpenTelemetry’s semantic conventions, and
additionally, correlating this to span data.

There are situations where telemetry does not follow OpenTelemetry's semantic
conventions. These are described below.

### The "unknown" transaction name

There are times when "unknown" is rendered as the name of a transaction. This
occurs when the telemetry received does not contain the requisite attributes
defined in the semantic conventions.

For example, emitting the `http.server.request.duration` metric without either
the `http.request.method` or `http.route` attributes. This may be an indication
of a bug in the instrumentation. That is, at least one of these attributes
should be present if the instrumentation has correctly implemented the HTTP
semantic conventions.

When you see "unknown" in the UI, this is a signal to users to study the
telemetry they are sending to New Relic and ensure that it adheres to
OpenTelemetry conventions.

On span data the `transaction.name` attribute will contain `unknown`.

### Transaction name can differ from span name

One characteristic unique to span data is that spans have a name whereas a
metric is distinguished only by its attributes. In the context of HTTP
services, note that the [HTTP span semantic conventions](https://opentelemetry.io/docs/specs/semconv/http/http-spans/#name)
declare that the span representing an HTTP request should be named by
concatenating the HTTP method and route. However, some customers have
circumstances where they choose to set the span name differently.

For the purposes of correlating transaction traces in the APM UI we’re required
to leverage the symmetry between the attributes present on both metric and span
data. However, in the Distributed Tracing UI, there is a desire to display both
the transaction name and span name when they differ. Currently, the UI displays
the transaction name when it is present instead of the span's name. This can
lead to confusion when users do not see the span name as they set it.
