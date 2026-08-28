namespace MindManagerManipulator;

internal sealed record OutlineReadNode(
  string Id,
  string Text,
  List<string> IconMarkers,
  int DescendantCount,
  List<OutlineReadNode> Children);

internal sealed record OutlineReadProjectionResult(
  OutlineReadNode Root,
  int ReturnedNodeCount,
  bool Truncated);

internal static class OutlineReadProjection
{
  public static OutlineReadProjectionResult Create(OutlineNode root, int? maxDepth) {
    if (maxDepth.HasValue && maxDepth.Value < 0) {
      throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must not be negative.");
    }

    int returnedNodeCount = 0;
    int totalNodeCount =
      Build(
        root,
        0,
        maxDepth,
        ref returnedNodeCount,
        out OutlineReadNode? projectedRoot);

    return new OutlineReadProjectionResult(
      projectedRoot!,
      returnedNodeCount,
      returnedNodeCount < totalNodeCount);
  }

  private static int Build(
    OutlineNode node,
    int depth,
    int? maxDepth,
    ref int returnedNodeCount,
    out OutlineReadNode? projectedNode)
  {
    bool include = maxDepth is null || depth <= maxDepth.Value;
    List<OutlineReadNode>? projectedChildren = include ? [] : null;
    int descendantCount = 0;

    foreach (OutlineNode child in node.Children) {
      int childNodeCount =
        Build(
          child,
          depth+1,
          maxDepth,
          ref returnedNodeCount,
          out OutlineReadNode? projectedChild);
      descendantCount += childNodeCount;
      if (projectedChild is not null) {
        projectedChildren!.Add(projectedChild);
      }
    }

    if (!include) {
      projectedNode = null;
      return descendantCount + 1;
    }

    ++returnedNodeCount;

    projectedNode =
      new OutlineReadNode(
        node.Id,
        node.Text,
        node.IconMarkers,
        descendantCount,
        projectedChildren!);

    return descendantCount + 1;
  }
}
