namespace MindManagerManipulator;

static class Program
{
  [STAThread]
  static void Main()
  {
    ApplicationConfiguration.Initialize();
    using GrpcProbeServer grpcServer = GrpcProbeServer.Start();
    Application.Run(new Form1());
  }
}
