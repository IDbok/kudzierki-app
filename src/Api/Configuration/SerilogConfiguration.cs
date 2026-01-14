using Serilog;
using Serilog.Templates;

namespace Api.Configuration;

public static class SerilogConfiguration
{
    public static void ConfigureSerilog(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Kudzierki.Api")
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:yyyy-MM-dd HH:mm:ss.fff zzz}] [{@l:u3}] [{CorrelationId}] {@m}\n{@x}"))
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
