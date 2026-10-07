using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MindManagerManipulator;

internal sealed record UpdateTopicTextHttpRequest
{
  public required string? Id { get; init; }
  public required string? ExpectedText { get; init; }
  public required string? NewText { get; init; }
}

internal sealed record ReadOutlineHttpRequest
{
  public string? StartId { get; init; }
  public int? MaxDepth { get; init; }
}

internal sealed record FindTopicHttpRequest
{
  public List<List<string>?>? Groups { get; init; }
  public string? StartId { get; init; }
  public int? MaxHits { get; init; }
}

internal sealed class HttpApiServer
: IDisposable
{
  private readonly WebApplication application_;

  private HttpApiServer(WebApplication application) {
    application_ = application;
  }

  public static HttpApiServer Start(Form1 form, IPAddress listenAddress, int port) {
    WebApplicationBuilder builder = WebApplication.CreateBuilder();

    builder.WebHost.ConfigureKestrel(options => {
      options.Listen(
        listenAddress, port,
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
      (ReadOutlineHttpRequest? request,
       CancellationToken cancellationToken) =>
         ReadOutlineAsync(form, request, cancellationToken));

    application.MapPost(
      "/v1/find-topic",
      (FindTopicHttpRequest? request,
       CancellationToken cancellationToken) =>
         FindTopicAsync(form, request, cancellationToken));

    application.MapPost(
      "/v1/update-topic-text",
      (UpdateTopicTextHttpRequest request,
       CancellationToken cancellationToken) =>
         UpdateTopicTextAsync(form, request, cancellationToken));

    application.MapGet(
      "/v1/document-info",
      (CancellationToken cancellationToken) =>
      GetDocumentInfoAsync(form, cancellationToken));

    application
      .StartAsync()
      .GetAwaiter()
      .GetResult();

    return new HttpApiServer(application);
  }

  private static async Task<IResult>
  ReadOutlineAsync(
    Form1 form,
    ReadOutlineHttpRequest? request,
    CancellationToken cancellationToken)
  {
    string? startId = request?.StartId;
    int? maxDepth = request?.MaxDepth;

    if (startId is not null && startId.Length == 0) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "startId must not be empty.",
        });
    }

    if (maxDepth.HasValue && maxDepth.Value < 0) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "maxDepth must not be negative.",
        });
    }

    try {
      OutlineReadProjectionResult? result =
        await form.TryReadSelectedOutlineForHttpApiAsync(startId, maxDepth, cancellationToken);

      if (result is null) {
        return Results.Conflict(
          new {
            error = "no_document_selected",
            message = "No MindManager document is selected.",
          });
      }

      return Results.Ok(
        new {
          observationId = result.ObservationId,
          revision = result.Revision,
          ancestorPath = result.AncestorPath,
          root = result.Root,
          returnedNodeCount = result.ReturnedNodeCount,
          truncated = result.Truncated,
        });
    }
    catch (TopicNotExposedException) {
      return Results.NotFound(
        new {
          error = "topic_not_found",
          message = "Topic is not part of the exposed outline.",
        });
    }
  }

  private static async Task<IResult>
  FindTopicAsync(Form1 form, FindTopicHttpRequest? request, CancellationToken cancellationToken) {
    if (request?.Groups is null || request.Groups.Count == 0) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "groups must contain at least one search group.",
        });
    }

    for (int groupIndex=0; groupIndex<request.Groups.Count; ++groupIndex) {
      List<string>? group = request.Groups[groupIndex];

      if (group is null || group.Count == 0) {
        return Results.BadRequest(
          new {
            error = "invalid_request",
            message =
              $"groups[{groupIndex}] must contain at least one search term.",
          });
      }

      for (int termIndex=0; termIndex<group.Count; ++termIndex) {
        if (string.IsNullOrEmpty(group[termIndex])) {
          return Results.BadRequest(
            new {
              error = "invalid_request",
              message =
                $"groups[{groupIndex}][{termIndex}] must not be empty.",
            });
        }
      }
    }

    if (request.StartId == "") {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "startId must not be empty.",
        });
    }

    int maxHits = request.MaxHits ?? 100;

    if (maxHits < 1) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "maxHits must be at least 1.",
        });
    }

    List<IReadOnlyList<string>> groups =
      request.Groups
        .Select(group => (IReadOnlyList<string>)group!)
        .ToList();

    try {
      FindTopicResult? result =
        await form.TryFindTopicForHttpApiAsync(
          groups,
          request.StartId,
          maxHits,
          cancellationToken);

      if (result is null) {
        return Results.Conflict(
          new {
            error = "no_document_selected",
            message = "No MindManager document is selected.",
          });
      }

      return Results.Ok(
        new {
          observationId = result.ObservationId,
          revision = result.Revision,
          totalHitCount = result.TotalHitCount,
          returnedHitCount = result.ReturnedHitCount,
          returnedNodeCount = result.ReturnedNodeCount,
          truncated = result.Truncated,
          contextRoot = result.ContextRoot,
        });
    }
    catch (TopicNotExposedException) {
      return Results.NotFound(
        new {
          error = "topic_not_found",
          message = "Topic is not part of the exposed outline.",
        });
    }
  }

  private static async Task<IResult>
  UpdateTopicTextAsync(
    Form1 form,
    UpdateTopicTextHttpRequest request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrEmpty(request.Id)) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "id is required.",
        });
    }

    if (request.ExpectedText is null) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "expectedText is required and must not be null.",
        });
    }

    if (request.NewText is null) {
      return Results.BadRequest(
        new {
          error = "invalid_request",
          message = "newText is required and must not be null.",
        });
    }

    try {
      TopicTextUpdateResult? result =
        await form.TryUpdateTopicTextForHttpApiAsync(
          request.Id,
          request.ExpectedText,
          request.NewText,
          cancellationToken);

      if (result is null) {
        return Results.Conflict(
          new {
            error = "no_document_selected",
            message = "No MindManager document is selected.",
          });
      }

      return Results.Ok(
        new {
          id = result.Id,
          oldText = result.OldText,
          newText = result.NewText,
          changed = result.Changed,
        });
    }
    catch (TopicNotExposedException) {
      return Results.NotFound(
        new {
          error = "topic_not_found",
          message = "Topic is not part of the exposed outline.",
        });
    }
    catch (TopicTextConflictException) {
      return Results.Conflict(
        new {
          error = "text_conflict",
          message = "Topic text changed since it was read.",
        });
    }
  }

  private static async Task<IResult>
  GetDocumentInfoAsync(Form1 form, CancellationToken cancellationToken) {
    OutlineDocumentInfo? result =
      await form.TryReadSelectedDocumentInfoForHttpApiAsync(cancellationToken);

    if (result is null) {
      return Results.Conflict(
        new {
          error = "no_document_selected",
          message = "No MindManager document is selected.",
        });
    }

    return Results.Ok(
      new {
        centralTopicText = result.CentralTopicText,
        nodeCount = result.NodeCount,
        leafCount = result.LeafCount,
        maxDepth = result.MaxDepth,
        averageLeafDepth = result.AverageLeafDepth,
      });
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
