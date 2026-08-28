namespace MindManagerManipulator;

static class Program
{
  [STAThread]
  static void Main()
  {
    ApplicationConfiguration.Initialize();

    using Form1 form = new();
    using GrpcProbeServer grpcServer = GrpcProbeServer.Start(form);
    using HttpApiServer httpApiServer = HttpApiServer.Start(form);
    Application.Run(form);
  }
}
