using Renci.SshNet;
using Rugal.ShellRunner.Model;
using System.Management.Automation;
using System.Text;

namespace Rugal.ShellRunner.Core
{
    public partial class ShellRunner
    {
        #region Ssh Property
        private SshConnectInfo SshInfo { get; set; }
        private SshClient Ssh { get; set; }
        private ShellStream SshStream { get; set; }
        private CommandLine LastSshCommand { get; set; }
        public string LastSshCommandText { get; set; }
        private string SshLastLocation { get; set; }
        private List<string> SshResultQueue { get; set; }
        #endregion
        public bool IsInSsh => Ssh?.IsConnected ?? false;
        #region Ssh
        private bool NewSsh(CommandLine Model)
        {
            if (IsInSsh)
            {
                Console.WriteLine("Cannot create new ssh connections in ssh mode\n");
                EndSsh();
                return false;
            }

            SshInfo = new SshConnectInfo(Model);
            if (!SshInfo.IsRequiredCheck)
            {
                EndSsh();
                return false;
            }

            var TryCount = 0;
            while (TryCount < 3)
            {
                try
                {
                    Ssh = new SshClient(SshInfo.Host, SshInfo.Port, SshInfo.UserName, SshInfo.Password);
                    Ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                    Ssh.Connect();
                    if (Ssh.IsConnected)
                    {
                        Console.WriteLine("Ssh connection success\n");

                        SshStream = Ssh.CreateShellStream("", 0, 0, 0, 0, 10240);
                        SshStream.ErrorOccurred += (s, e) =>
                        {
                            Console.WriteLine(e.Exception.ToString());
                        };
                        SshStream.DataReceived += (s, e) =>
                        {
                            var Result = Encoding.UTF8.GetString(e.Data);
                            ProcessSshResult(Result);
                        };

                        WaitSshResult();
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Ssh connection error");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                TryCount++;
                Console.WriteLine($"\nRetry {TryCount}...\n");
            }
            Console.WriteLine("Retry more than 3 times");
            return false;
        }
        private void EndSsh()
        {
            if (Ssh is not null)
                Console.WriteLine("Ssh connection has been closed");

            SshStream?.Flush();
            SshStream?.Close();
            SshStream = null;
            Ssh?.Disconnect();
            Ssh?.Dispose();
            Ssh = null;
        }
        private void SshSend(CommandLine Model)
        {
            LastSshCommandText = Model.FullCommand;
            if (!Model.FullCommand.Contains("sudo"))
            {
                LastSshCommand = Model;
                SshStream.Write(Model.FullCommand + "\r");
                Task.Delay(10).Wait();
            }
            else
            {
                SshStream.WriteLine(Model.FullCommand);
                SshStream.Expect("password", TimeSpan.FromSeconds(0.5));
                SshStream.WriteLine(SshInfo.Password);
            }

            WaitSshResult();
        }
        public void SshSendText(string CommandText)
        {
            SshStream.Write(CommandText + "\r");
            Task.Delay(10).Wait();
        }
        private void ProcessSshResult(string Result)
        {
            SshResultQueue ??= new List<string> { };

            foreach (var Item in Result.Split('\r', '\n'))
            {
                if (string.IsNullOrWhiteSpace(Item))
                    continue;

                if (LastSshCommand is not null && LastSshCommand.FullCommand == Item)
                    continue;

                if (IsLastResult(Item))
                {
                    SshLastLocation = Item;
                    var NextLine = LastSshCommandText == "" ? "" : "\n";
                    Console.Write($"{NextLine}{Item.TrimEnd(' ', '#')}> ");
                }
                else
                    Console.WriteLine(Item);

                SshResultQueue.Add(Item);
            }
        }
        private bool IsLastResult(string Result)
        {
            if (Result is null)
                return false;

            var IsLast = Result.Contains(SshInfo.UserName) && Result.Contains('@') && Result.Contains(':');
            return IsLast;
        }
        public void WaitSshResult()
        {
            while (!IsLastResult(SshResultQueue?.LastOrDefault()))
            {
                Task.Delay(10).Wait();
            }
            SshResultQueue = null;
        }
        #endregion
    }
}
