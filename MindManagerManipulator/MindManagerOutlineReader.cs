using System.Diagnostics;
using System.Text;

namespace MindManagerManipulator;

internal sealed record OutlineNode(
  string Id,
  string Text,
  List<OutlineNode> Children);

internal sealed record OutlineReadResult(
  OutlineNode Root,
  int NodeCount,
  TimeSpan Elapsed);

internal static class MindManagerOutlineReader
{
  public static OutlineReadResult Read(object documentObject)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();

    int nodeCount = 0;

    dynamic document = documentObject;
    dynamic centralTopic = document.CentralTopic;

    OutlineNode root =
      ReadTopic(centralTopic, ref nodeCount);

    stopwatch.Stop();

    return new OutlineReadResult(
      root,
      nodeCount,
      stopwatch.Elapsed);
  }

  public static string Format(OutlineNode root)
  {
    StringBuilder result = new();

    AppendTopic(result, root, 0);

    return result.ToString();
  }

  private static OutlineNode ReadTopic(
    dynamic topic,
    ref int nodeCount)
  {
    ++nodeCount;

    string id =
      Convert.ToString(topic.Guid) ?? "";

    string text =
      Convert.ToString(topic.Text) ?? "";

    dynamic subTopics = topic.SubTopics;

    int count = (int)subTopics.Count;

    List<OutlineNode> children =
      new(count);

    for (int i=1; i<=count; ++i) {
      dynamic child = subTopics[i];

      children.Add(
        ReadTopic(child, ref nodeCount));
    }

    return new OutlineNode(
      id,
      text,
      children);
  }

  private static void AppendTopic(
    StringBuilder result,
    OutlineNode topic,
    int depth)
  {
    result.Append(' ', depth*2);
    result.Append("- [");
    result.Append(topic.Id);
    result.Append("] ");
    result.AppendLine(FormatText(topic.Text));

    foreach (OutlineNode child in topic.Children) {
      AppendTopic(result, child, depth+1);
    }
  }

  private static string FormatText(string text)
  {
    return text
      .Replace("\r\n", "\n")
      .Replace('\r', '\n')
      .Replace("\n", "\\n");
  }
}
