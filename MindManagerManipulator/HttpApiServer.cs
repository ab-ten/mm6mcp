using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace MindManagerManipulator;

internal sealed class HttpApiServer
: IDisposable
{
  private const int Port = 50052;
  private readonly WebApplication application_;

  private HttpApiServer(WebApplication application) {
    application_ = application;
  }

  public static HttpApiServer Start() {
    WebApplicationBuilder builder = WebApplication.CreateBuilder();

    builder.WebHost.ConfigureKestrel(options => {
      options.Listen(
        IPAddress.Loopback,
        Port,
        listenOptions => {
          listenOptions.Protocols =
            HttpProtocols.Http1;
        });
    });

    WebApplication application = builder.Build();

    application.MapGet(
      "/v1/ping",
      () => new { message = "pong" });

    application
      .StartAsync()
      .GetAwaiter()
      .GetResult();

    return new HttpApiServer(application);
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
