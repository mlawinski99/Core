using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Observability;

public static class ObservabilityDependencyInstaller
{
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

        // CORE_OTLP_ENDPOINT - Aspire
        // OTEL_EXPORTER_OTLP_ENDPOINT - not local env
        var otlpEndpoint = builder.Configuration["CORE_OTLP_ENDPOINT"]
                        ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        var otlpProtocol = builder.Configuration["CORE_OTLP_PROTOCOL"]
                        ?? builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];

        var protocol = otlpProtocol == "http/protobuf"
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;

        void ConfigureLogs(OtlpExporterOptions o) => Configure(o, "logs");
        void ConfigureTraces(OtlpExporterOptions o) => Configure(o, "traces");
        void ConfigureMetrics(OtlpExporterOptions o) => Configure(o, "metrics");

        void Configure(OtlpExporterOptions o, string signal)
        {
            if (otlpEndpoint is null) return;
            o.Endpoint = new Uri($"{otlpEndpoint.TrimEnd('/')}/v1/{signal}");
            o.Protocol = protocol;
        }

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter(ConfigureLogs);
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ConfigureTraces))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ConfigureMetrics));

        return builder;
    }
}