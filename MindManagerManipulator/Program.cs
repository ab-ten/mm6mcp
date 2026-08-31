using System.Net;

namespace MindManagerManipulator;


static class Program
{
  [STAThread]
  static void Main(string[] args)
  {
    ApplicationConfiguration.Initialize();
    ProgramOptions options;

    try {
      options = ParseOptions(args);
    }catch (ArgumentException ex) {
      MessageBox.Show(
        ex.Message,
        "MindManager Manipulator",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
      return;
    }

    using Form1 form = new();
    using HttpApiServer httpApiServer =
                          HttpApiServer.Start(form, options.ListenAddress, options.Port);
    Application.Run(form);
  }

  private static ProgramOptions ParseOptions(string[] args) {
    string host = "127.0.0.1";
    int port = 50052;

    for (int i=0; i<args.Length; ++i) {
      switch (args[i]) {
      case "--host":
        host = RequireValue(args, ref i, "--host");
        break;

      case "--port":
        string portText = RequireValue(args, ref i, "--port");
        if (! int.TryParse(portText, out port)
            || port < 1
            || port > 65535) {
          throw new ArgumentException("--port must be an integer from 1 through 65535.");
        }
        break;

      default:
        throw new ArgumentException($"Unknown command-line argument: {args[i]}");
      }
    }

    IPAddress listenAddress;
    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) {
      listenAddress = IPAddress.Loopback;
    }else if (!IPAddress.TryParse(host, out listenAddress!)) {
      throw new ArgumentException("--host must be an IP address or localhost.");
    }

    return new ProgramOptions(listenAddress, port);
  }

  private static string RequireValue(string[] args, ref int index, string option) {
    if (++index >= args.Length) {
      throw new ArgumentException($"{option} requires a value.");
    }
    return args[index];
  }

  private sealed record ProgramOptions(
    IPAddress ListenAddress,
    int Port);
}
