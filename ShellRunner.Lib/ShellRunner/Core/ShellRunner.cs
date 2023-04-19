using Renci.SshNet;
using Rugal.ShellRunner.Model;
using System.Management.Automation;

namespace Rugal.ShellRunner.Core
{
    public class ShellRunner
    {
        private readonly PowerShell Shell;

        private SshConnectInfo SshInfo;
        private SshClient Ssh;
        private ShellStream SshStream;
        private string SshLastLocation;
        private string StartPath;
        public bool IsInSsh => Ssh?.IsConnected ?? false;
        public ShellRunner()
        {
            Shell = PowerShell.Create();
        }
        public bool Run(CommandLineModel Model)
        {
            if (string.IsNullOrWhiteSpace(Model.CommandLine))
                return true;

            switch (Model.CommandName.ToLower())
            {
                case "#ssh":
                    Console.WriteLine("Create new ssh connection");
                    return NewSsh(Model);

                case "#endssh":
                    EndSsh();
                    break;

                case "#scp":
                    ScpSend(Model);
                    break;

                case "#run":
                    ImportFiles(Model);
                    break;

                case "#open":
                    OpenFolder(Model);
                    break;

                case "#back":
                    BackToStart();
                    break;

                default:
                    CommandSend(Model);
                    break;
            }
            return true;
        }

        public void CommandSend(CommandLineModel Model)
        {
            if (IsInSsh)
            {
                var SshResult = SshSend(Model);
                if (!string.IsNullOrWhiteSpace(SshResult))
                    SshPrint(SshResult, Model);
            }
            else
            {
                var ShellResult = ShellInvoke(Model);
                ShellPrint(ShellResult);
            }
        }

        #region Shell
        public string GetCurrentLocation()
        {
            if (IsInSsh)
            {
                var SshRet = SshSend(new CommandLineModel("pwd"));
                var SshLocation = $"{SshInfo.UserName}@{SshInfo.Host}:{SshRet}";
                SshLastLocation = SshLocation;

                return SshLastLocation;
            }
            else
            {
                var ShellRet = ShellInvoke(new CommandLineModel("Get-Location"), false);
                var ShellLocation = ShellRet.Result.First().BaseObject.ToString();
                StartPath ??= ShellLocation;
                return ShellLocation;
            }
        }
        private ShellResult ShellInvoke(CommandLineModel Model, bool IsOutString = true)
        {
            var Script = Model.CommandLine;
            if (IsOutString)
                Script += " | Out-String";

            Shell.AddScript(Script);
            var RunResult = Shell.Invoke();
            var GetStreams = Shell.Streams;

            var Result = new ShellResult()
            {
                CommandLineInfo = Model,
                Errors = GetStreams.Error.ToList(),
                Result = RunResult.ToList(),
            };

            GetStreams.Error.Clear();
            Shell.Commands.Clear();

            return Result;
        }
        private void ShellPrint(ShellResult Model)
        {
            if (Model.IsHasError)
            {
                foreach (var Item in Model.Errors)
                    Console.WriteLine(Item);
            }
            else
            {
                foreach (var Item in Model.Result)
                {
                    var PrintResult = Item.BaseObject.ToString();
                    Console.WriteLine(PrintResult);
                }
            }
        }
        private void OpenFolder(CommandLineModel Model)
        {
            var GetArg = Model.AllArgs.FirstOrDefault();
            var OpenPath = ".";
            if (GetArg is not null)
                OpenPath = GetArg;

            CommandSend(new CommandLineModel($"start {OpenPath}"));
        }
        private void BackToStart()
        {
            CommandSend(new CommandLineModel($"cd {StartPath}"));
        }
        private void ImportFiles(CommandLineModel Model)
        {
            var AllFile = Model.GetArgs("-f");

            if (Model.OtherArgs.Any())
                AllFile.AddRange(Model.OtherArgs);

            foreach (var FileName in AllFile)
            {
                RunFile(FileName, Model);
            }
        }

        private void RunFile(string FileName, CommandLineModel Model)
        {
            var AllLines = File.ReadAllLines(FileName);
            foreach (var Line in AllLines)
            {
                if (string.IsNullOrWhiteSpace(Line))
                    continue;

                var SendLine = Line;

                var CommandInfo = new CommandLineModel(SendLine);
                if (CommandInfo.ParamArgs.Any())
                {
                    foreach (var Item in CommandInfo.ParamArgs)
                    {
                        if (!Model.TryGetParamValue(Item, out var ParamArg))
                        {
                            Console.WriteLine($"「{Item}」Param Arg is required");
                            return;
                        }
                        SendLine = SendLine.Replace(Item, ParamArg);
                    }
                }

                var Location = GetCurrentLocation();
                var PrintCommand = $"{Location}> {SendLine}";
                Console.WriteLine(PrintCommand);

                var IsCanNext = Run(new CommandLineModel(SendLine));
                if (!IsCanNext)
                    break;
            }
        }

        #endregion

        #region Ssh
        private bool NewSsh(CommandLineModel Model)
        {
            if (IsInSsh)
            {
                Console.WriteLine("Cannot create new ssh connections in ssh mode");
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
                    Ssh.Connect();
                    if (Ssh.IsConnected)
                    {
                        Console.WriteLine("Ssh connection success");

                        SshStream = Ssh.CreateShellStream("", 0, 0, 0, 0, 0);
                        var ConnectResult = GetSshResult();
                        Console.WriteLine(ConnectResult);
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
            Console.WriteLine("Retry more than 5 times");
            return false;
        }
        private void EndSsh()
        {
            Console.WriteLine("End Ssh Connection");

            SshStream?.Flush();
            SshStream?.Close();
            SshStream = null;
            Ssh?.Disconnect();
            Ssh?.Dispose();
            Ssh = null;
        }
        private string SshSend(CommandLineModel Model, double TimeOutSec = 0.5)
        {
            var NoReadCmd = new[]
            {
                "cd",
                SshInfo.Password,
            };

            if (Model.CommandLine.Contains("sudo"))
            {
                SshStream.WriteLine(Model.CommandLine);
                SshStream.Expect("password", TimeSpan.FromSeconds(0.5));
                SshStream.WriteLine(SshInfo.Password);
            }
            else
            {
                SshStream.WriteLine(Model.CommandLine);
            }

            if (!NoReadCmd.Contains(Model.CommandName.ToLower()))
            {
                var Result = GetSshResult(TimeOutSec);
                return Result;
            }
            else
            {
                SshStream.Flush();
                return null;
            }
        }
        private string GetSshResult(double TimeOutSec = 0.5)
        {
            var Lines = new List<string> { };
            while (true)
            {
                var GetLine = SshStream.ReadLine(TimeSpan.FromSeconds(TimeOutSec));
                if (GetLine is null)
                {
                    break;
                }

                var IsContainUserName = GetLine.Contains(SshInfo.UserName) || GetLine.Contains("root");
                var IsContainAt = IsContainUserName && GetLine.Contains('@');
                var IsContainColon = IsContainAt && GetLine.Contains(':');

                if (IsContainColon)
                    continue;

                var IsContainFor = IsContainUserName && GetLine.Contains("for");
                if (IsContainFor)
                    continue;

                Lines.Add(GetLine);
            }
            var Result = string.Join('\n', Lines);
            return Result;
        }
        private void SshPrint(string SshResult, CommandLineModel Model)
        {
            Console.WriteLine(SshResult);
        }
        #endregion

        #region Scp
        public static void ScpSend(CommandLineModel Model)
        {
            var ScpInfo = new ScpConnectInfo(Model);

            if (!ScpInfo.IsRequiredCheck)
                return;

            var ScpClient = new SftpClient(ScpInfo.Host, ScpInfo.Port, ScpInfo.UserName, ScpInfo.Password);
            ScpClient.Connect();

            if (ScpInfo.IsUpload)
            {
                var Buffer = File.ReadAllBytes(ScpInfo.LocalPath);
                var Info = new FileInfo(ScpInfo.LocalPath);

                var TotalLength = Buffer.Length;
                var Ms = new MemoryStream(Buffer);
                var LastPersent = 0.0;

                Console.WriteLine($"scp upload file {Info.Name} start");

                ScpClient.UploadFile(Ms, ScpInfo.RemotePath, (WriteLength) =>
                {
                    var Persnet = Math.Floor((double)WriteLength / TotalLength * 100);
                    if (Persnet - LastPersent >= 5)
                    {
                        LastPersent = Persnet;
                        Console.WriteLine($"scp upload file {Info.Name}....{Persnet}%");
                    }
                });

                Console.WriteLine($"scp upload file {Info.Name} finish");
            }
            else
            {
                var Info = new FileInfo(ScpInfo.LocalPath);
                if (Info.Exists)
                    Info.Delete();
                using var WriteMs = Info.Create();
                Console.WriteLine($"scp download {Info.Name} start");
                ScpClient.DownloadFile(ScpInfo.RemotePath, WriteMs, (WriteLength) =>
                {

                });

                Console.WriteLine($"scp download file {Info.Name} finish");
            }

            ScpClient.Disconnect();
            ScpClient.Dispose();
        }

        #endregion
    }
}