namespace MindManagerManipulator;

internal sealed record OutlineReadNode(
  string Id,
  string Text,
  List<string> IconMarkers,
  int DescendantCount,
  long SubtreeRevision,
  List<OutlineReadNode> Children);

internal sealed record OutlineReadProjectionResult(
  OutlineReadNode Root,
  List<OutlineAncestorNode> AncestorPath,
  int ReturnedNodeCount,
  bool Truncated,
  string ObservationId,
  long Revision);

internal sealed record OutlineAncestorNode(
  string Id,
  string Text);

internal static class OutlineReadProjection
{
  public static OutlineReadProjectionResult Create(
      OutlineNode root,
      IReadOnlyList<OutlineAncestorNode> ancestorPath,
      int? maxDepth,
      string observationId,
      long revision,
      Func<string, long> getSubtreeRevision) {
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
        getSubtreeRevision,
        out OutlineReadNode? projectedRoot);

    return new OutlineReadProjectionResult(
      projectedRoot!,
      [.. ancestorPath],
      returnedNodeCount,
      returnedNodeCount < totalNodeCount,
      observationId,
      revision);
  }

  private static int Build(
    OutlineNode node,
    int depth,
    int? maxDepth,
    ref int returnedNodeCount,
    Func<string, long> getSubtreeRevision,
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
          getSubtreeRevision,
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
        getSubtreeRevision(node.Id),
        projectedChildren!);

    return descendantCount + 1;
  }
}
