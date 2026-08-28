using Grpc.Core;
using MindManagerManipulator.Grpc;
using GrpcOutlineNode = MindManagerManipulator.Grpc.OutlineNode;

namespace MindManagerManipulator;

internal sealed class ProbeGrpcService
: Probe.ProbeBase
{
  private readonly Form1 form_;

  public ProbeGrpcService(Form1 form) {
    form_ = form;
  }

  public override Task<PingReply> Ping(PingRequest request, ServerCallContext context) {
    return Task.FromResult(new PingReply { Message = "pong" });
  }

  public override async Task<ReadSelectedOutlineReply>
  ReadSelectedOutline(ReadSelectedOutlineRequest request, ServerCallContext context) {
    int handlerThreadId = Environment.CurrentManagedThreadId;

    UiOutlineProbeResult uiRead =
      await form_.ReadSelectedOutlineForGrpcProbeAsync(context.CancellationToken);

    return new ReadSelectedOutlineReply {
      Root = ToGrpcOutlineNode(uiRead.Result.Root),
      NodeCount = uiRead.Result.NodeCount,
      HandlerThreadId = handlerThreadId,
      CallbackThreadId = uiRead.CallbackThreadId,
    };
  }

  public override async Task<UpdateTopicTextReply>
  UpdateTopicText(UpdateTopicTextRequest request, ServerCallContext context) {
    if (string.IsNullOrEmpty(request.Id)) {
      throw new RpcException(new Status(StatusCode.InvalidArgument, "id is required."));
    }
    if (! request.HasExpectedText) {
      throw new RpcException(
          new Status(StatusCode.InvalidArgument, "expected_text is required."));
    }
    if (! request.HasNewText) {
      throw new RpcException(
          new Status(StatusCode.InvalidArgument, "new_text is required."));
    }

    try {
      TopicTextUpdateResult result =
        await form_.UpdateTopicTextForGrpcAsync(
          request.Id,
          request.ExpectedText,
          request.NewText,
          context.CancellationToken);

      return new UpdateTopicTextReply {
        Id = result.Id,
        OldText = result.OldText,
        NewText = result.NewText,
        Changed = result.Changed,
      };
    }
    catch (TopicTextConflictException) {
      throw new RpcException(
          new Status(StatusCode.Aborted, "Topic text changed since it was read."));
    }
  }

  private static GrpcOutlineNode
  ToGrpcOutlineNode(OutlineNode node) {
    GrpcOutlineNode result = new() {
      Id = node.Id,
      Text = node.Text,
    };

    foreach (string iconMarker in node.IconMarkers) {
      result.IconMarkers.Add(iconMarker);
    }

    foreach (OutlineNode child in node.Children) {
      result.Children.Add(ToGrpcOutlineNode(child));
    }

    return result;
  }
}
