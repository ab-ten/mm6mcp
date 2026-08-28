namespace MindManagerManipulator;

internal sealed record OutlineDocumentInfo(
  string CentralTopicText,
  int NodeCount,
  int LeafCount,
  int MaxDepth,
  double AverageLeafDepth);

internal static class OutlineDocumentInfoCalculator
{
  public static OutlineDocumentInfo Calculate(OutlineNode root, int nodeCount) {
    int leafCount = 0;
    int maxDepth = 0;
    long totalLeafDepth = 0;

    Accumulate(root, 0, ref leafCount, ref maxDepth, ref totalLeafDepth);
    double averageLeafDepth =
      leafCount == 0 ? 0.0 : (double)totalLeafDepth/leafCount;

    return new OutlineDocumentInfo(root.Text, nodeCount, leafCount, maxDepth, averageLeafDepth);
  }

  private static void Accumulate(
    OutlineNode node,
    int depth,
    ref int leafCount,
    ref int maxDepth,
    ref long totalLeafDepth)
  {
    if (depth > maxDepth) {
      maxDepth = depth;
    }

    if (node.Children.Count == 0) {
      ++leafCount;
      totalLeafDepth += depth;
      return;
    }

    foreach (OutlineNode child in node.Children) {
      Accumulate(child, depth+1, ref leafCount, ref maxDepth, ref totalLeafDepth);
    }
  }
}
