using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;

namespace MindManagerManipulator;

internal sealed record XmlOutlineReadResult(
  OutlineNode Root,
  int NodeCount,
  TimeSpan XmlFetchElapsed,
  TimeSpan ParseElapsed);

internal static class MindManagerXmlOutlineReader
{
  private static readonly XNamespace ApplicationNamespace =
    "http://schemas.mindjet.com/MindManager/Application/2003";

  public static XmlOutlineReadResult Read(object documentObject)
  {
    dynamic document = documentObject;

    Stopwatch fetchStopwatch = Stopwatch.StartNew();

    string xml =
      Convert.ToString(document.Xml)
      ?? throw new InvalidOperationException(
        "MindManager returned no document XML.");

    fetchStopwatch.Stop();

    Stopwatch parseStopwatch = Stopwatch.StartNew();

    XDocument xmlDocument = ParseXml(xml);

    XElement map =
      xmlDocument.Root
      ?? throw new InvalidOperationException(
        "MindManager XML has no root element.");

    XElement oneTopic =
      map.Element(ApplicationNamespace+"OneTopic")
      ?? throw new InvalidOperationException(
        "MindManager XML has no OneTopic element.");

    XElement rootTopic =
      oneTopic.Element(ApplicationNamespace+"Topic")
      ?? throw new InvalidOperationException(
        "MindManager XML has no root Topic element.");

    int nodeCount = 0;

    OutlineNode root =
      ReadTopic(rootTopic, ref nodeCount);

    parseStopwatch.Stop();

    return new XmlOutlineReadResult(
      root,
      nodeCount,
      fetchStopwatch.Elapsed,
      parseStopwatch.Elapsed);
  }

  private static XDocument ParseXml(string xml)
  {
    XmlReaderSettings settings = new() {
      DtdProcessing = DtdProcessing.Prohibit,
      XmlResolver = null,
    };

    using StringReader stringReader = new(xml);
    using XmlReader reader =
      XmlReader.Create(stringReader, settings);

    return XDocument.Load(reader);
  }

  private static OutlineNode ReadTopic(
    XElement topic,
    ref int nodeCount)
  {
    ++nodeCount;

    string id =
      (string?)topic.Attribute("OId")
      ?? "";

    string text =
      (string?)topic
        .Element(ApplicationNamespace+"Text")
        ?.Attribute("PlainText")
      ?? "";

    List<OutlineNode> children = [];

    XElement? subTopics =
      topic.Element(
        ApplicationNamespace+"SubTopics");

    if (subTopics is not null) {
      foreach (XElement child in
        subTopics.Elements(
          ApplicationNamespace+"Topic")) {
        children.Add(
          ReadTopic(child, ref nodeCount));
      }
    }

    return new OutlineNode(
      id,
      text,
      children);
  }
}


internal static class OutlineComparer
{
  public static string? Compare(
    OutlineNode expected,
    OutlineNode actual)
  {
    return CompareNode(
      expected,
      actual,
      "root");
  }

  private static string? CompareNode(
    OutlineNode expected,
    OutlineNode actual,
    string path)
  {
    if (expected.Id != actual.Id) {
      return
        $"{path}: ID differs: " +
        $"COM='{expected.Id}', XML='{actual.Id}'";
    }

    if (expected.Text != actual.Text) {
      return
        $"{path}: Text differs: " +
        $"COM='{Escape(expected.Text)}', " +
        $"XML='{Escape(actual.Text)}'";
    }

    if (expected.Children.Count !=
        actual.Children.Count) {
      return
        $"{path}: child count differs: " +
        $"COM={expected.Children.Count}, " +
        $"XML={actual.Children.Count}";
    }

    for (int i=0; i<expected.Children.Count; ++i) {
      OutlineNode expectedChild =
        expected.Children[i];

      OutlineNode actualChild =
        actual.Children[i];

      string? error =
        CompareNode(
          expectedChild,
          actualChild,
          $"{path}/{i}");

      if (error is not null) {
        return error;
      }
    }

    return null;
  }

  private static string Escape(string text)
  {
    return text
      .Replace("\r", "\\r")
      .Replace("\n", "\\n");
  }
}
