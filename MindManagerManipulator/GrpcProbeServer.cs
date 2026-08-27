using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MindManagerManipulator;

internal sealed class GrpcProbeServer
: IDisposable
{
  private const int Port = 50051;

  private readonly WebApplication application_;

  private GrpcProbeServer(WebApplication application) {
    application_ = application;
  }

  public static GrpcProbeServer Start(Form1 form) {
    WebApplicationBuilder builder = WebApplication.CreateBuilder();

    builder.WebHost.ConfigureKestrel(options => {
      options.Listen(
        IPAddress.Loopback,
        Port,
        listenOptions => {
          listenOptions.Protocols =
            HttpProtocols.Http2;
        });
    });

    builder.Services.AddSingleton(form);
    builder.Services.AddGrpc();
    WebApplication application = builder.Build();
    application.MapGrpcService<ProbeGrpcService>();

    application
      .StartAsync()
      .GetAwaiter()
      .GetResult();

    return new GrpcProbeServer(application);
  }

  public void Dispose()
  {
    application_
      .StopAsync()
      .GetAwaiter()
      .GetResult();

    application_
      .DisposeAsync()
      .AsTask()
      .GetAwaiter()
      .GetResult();
  }
}
