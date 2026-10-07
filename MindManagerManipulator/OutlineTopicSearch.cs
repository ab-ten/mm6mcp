namespace MindManagerManipulator;

internal sealed record OutlineTopicSearchNode(
  string Id,
  string Text,
  bool IsMatch,
  List<OutlineTopicSearchNode> Children);

internal sealed record OutlineTopicSearchProjection(
  OutlineTopicSearchNode? ContextRoot,
  int TotalHitCount,
  int ReturnedHitCount,
  int ReturnedNodeCount,
  bool Truncated);

internal static class OutlineTopicSearch
{
  /// <summary>
  /// 指定subtree内のTopic Textを検索し、返却対象hitとその祖先だけを含む
  /// Central Topic起点のcontext treeを構築する。
  /// </summary>
  ///
  /// <remarks>
  /// groupsはOR-of-ANDとして解釈する。
  /// 各group内の全termが同一Topic Textにcase-insensitiveなsubstringとして
  /// 含まれる場合、そのgroupは一致する。いずれかのgroupが一致すればhitとなる。
  ///
  /// hitの選択順はMindMapのchild orderに従うdepth-first preorderである。
  /// maxHitsは返却hit数だけを制限し、TotalHitCount算出のため検索範囲全体を走査する。
  /// </remarks>
  public static OutlineTopicSearchProjection Search(
    OutlineNode root,
    OutlineNode searchRoot,
    IReadOnlyList<IReadOnlyList<string>> groups,
    int maxHits)
  {
    if (groups.Count == 0) {
      throw new ArgumentException("At least one search group is required.", nameof(groups));
    }

    for (int groupIndex=0; groupIndex<groups.Count; ++groupIndex) {
      IReadOnlyList<string> group = groups[groupIndex];

      if (group.Count == 0) {
        throw new ArgumentException(
            $"Search group {groupIndex} must not be empty.", nameof(groups));
      }

      for (int termIndex=0; termIndex<group.Count; ++termIndex) {
        if (string.IsNullOrEmpty(group[termIndex])) {
          throw new ArgumentException(
            $"Search term {groupIndex}:{termIndex} must not be empty.", nameof(groups));
        }
      }
    }

    if (maxHits < 1) {
      throw new ArgumentOutOfRangeException(nameof(maxHits), "maxHits must be at least 1.");
    }

    HashSet<string> returnedHitIds = [];
    int totalHitCount = 0;

    CollectMatches(searchRoot, groups, maxHits, returnedHitIds, ref totalHitCount);

    if (returnedHitIds.Count == 0) {
      return new OutlineTopicSearchProjection(null, totalHitCount, 0, 0, totalHitCount > 0);
    }

    int returnedNodeCount = 0;

    OutlineTopicSearchNode? contextRoot =
      BuildContextTree(root, returnedHitIds, ref returnedNodeCount);

    if (contextRoot is null) {
      throw new InvalidOperationException(
        "Search hits could not be projected into the logical outline.");
    }

    return new OutlineTopicSearchProjection(
      contextRoot,
      totalHitCount,
      returnedHitIds.Count,
      returnedNodeCount,
      totalHitCount > returnedHitIds.Count);
  }

  private static void
  CollectMatches(
    OutlineNode node,
    IReadOnlyList<IReadOnlyList<string>> groups,
    int maxHits,
    HashSet<string> returnedHitIds,
    ref int totalHitCount)
  {
    if (Matches(node.Text, groups)) {
      ++totalHitCount;

      if (returnedHitIds.Count < maxHits) {
        returnedHitIds.Add(node.Id);
      }
    }

    foreach (OutlineNode child in node.Children) {
      CollectMatches(child, groups, maxHits, returnedHitIds, ref totalHitCount);
    }
  }

  private static bool
  Matches(string text, IReadOnlyList<IReadOnlyList<string>> groups) {
    foreach (IReadOnlyList<string> group in groups) {
      bool groupMatches = true;

      foreach (string term in group) {
        if (!text.Contains(term, StringComparison.OrdinalIgnoreCase)) {
          groupMatches = false;
          break;
        }
      }

      if (groupMatches) {
        return true;
      }
    }

    return false;
  }

  private static OutlineTopicSearchNode?
  BuildContextTree(OutlineNode node, HashSet<string> returnedHitIds, ref int returnedNodeCount) {
    bool isMatch = returnedHitIds.Contains(node.Id);

    List<OutlineTopicSearchNode> children = [];

    foreach (OutlineNode child in node.Children) {
      OutlineTopicSearchNode? projectedChild =
        BuildContextTree(child, returnedHitIds, ref returnedNodeCount);

      if (projectedChild is not null) {
        children.Add(projectedChild);
      }
    }

    if (!isMatch && children.Count == 0) {
      return null;
    }

    ++returnedNodeCount;

    return new OutlineTopicSearchNode(node.Id, node.Text, isMatch, children);
  }
}
