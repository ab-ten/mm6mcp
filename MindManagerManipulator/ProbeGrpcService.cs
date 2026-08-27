using Grpc.Core;
using MindManagerManipulator.Grpc;

namespace MindManagerManipulator;

internal sealed class ProbeGrpcService
: Probe.ProbeBase
{
  public override Task<PingReply> Ping(PingRequest request, ServerCallContext context) {
    return Task.FromResult(new PingReply { Message = "pong" });
  }
}
