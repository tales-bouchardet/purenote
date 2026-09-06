using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace PureNote
{
    public partial class App
    {
        private static readonly string PipeName = "PureNote_Pipe_" + Environment.UserName;
        private static readonly string MutexName = "PureNote_SingleInstance_" + Environment.UserName;

        private Mutex _instanceMutex;

        private bool AcquireInstanceLock()
        {
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
            return createdNew;
        }

        private void StartPipeServer()
        {
            new Thread(PipeServerLoop) { IsBackground = true }.Start();
        }

        private void PipeServerLoop()
        {
            while (true)
            {
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.In))
                    {
                        server.WaitForConnection();

                        using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                        {
                            string path;
                            while ((path = reader.ReadLine()) != null)
                            {
                                if (!File.Exists(path)) continue;

                                string opened = path;
                                Dispatcher.Invoke(() => OpenInRunningInstance(opened));
                            }
                        }
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private void OpenInRunningInstance(string path)
        {
            (MainWindow as MainWindow)?.OpenFromExternal(path);
        }

        private static void TrySendToRunningInstance(string[] paths)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(500);

                    using (StreamWriter writer = new StreamWriter(client, Encoding.UTF8))
                    {
                        foreach (string path in paths) writer.WriteLine(path);
                        writer.Flush();
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
