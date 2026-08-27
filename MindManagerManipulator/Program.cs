namespace MindManagerManipulator;

static class Program
{
  [STAThread]
  static void Main()
  {
    ApplicationConfiguration.Initialize();

    using Form1 form = new();
    using GrpcProbeServer grpcServer = GrpcProbeServer.Start(form);
    Application.Run(form);
  }
}
