using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MindManagerManipulator;

internal sealed class HttpApiServer
: IDisposable
{
  private const int Port = 50052;
  private readonly WebApplication application_;

  private HttpApiServer(WebApplication application) {
    application_ = application;
  }

  public static HttpApiServer Start(Form1 form) {
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

    builder.Services.ConfigureHttpJsonOptions(options => {
      options.SerializerOptions.MaxDepth = 256;
    });

    WebApplication application = builder.Build();

    application.MapGet(
      "/v1/ping",
      () => new { message = "pong" });

    application.MapPost(
      "/v1/read-outline",
      (CancellationToken cancellationToken) =>
        ReadOutlineAsync(form, cancellationToken));

    application
      .StartAsync()
      .GetAwaiter()
      .GetResult();

    return new HttpApiServer(application);
  }

  private static async Task<IResult>
  ReadOutlineAsync(Form1 form, CancellationToken cancellationToken) {
    XmlOutlineReadResult? result =
      await form.TryReadSelectedOutlineForHttpApiAsync(cancellationToken);

    if (result is null) {
      return Results.Conflict(
        new {
          error = "no_document_selected",
          message = "No MindManager document is selected.",
        });
    }

    return Results.Ok(
      new { root = result.Root, nodeCount = result.NodeCount });
  }

  public void Dispose() {
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
