using Renci.SshNet;
using Rugal.ShellRunner.Model;
using System.Management.Automation;
using System.Runtime.Intrinsics.X86;

namespace Rugal.ShellRunner.Core
{
    public class ShellRunner
    {
        #region Shell Property
        private PowerShell Shell { get; set; }
        private string StartPath { get; set; }
        public VariableModel Variable { get; set; }
        public RunnerMode RunMode { get; set; }
        public bool IsPrintMode { get; set; }
        public string NowLocation { get; set; }
        #endregion

        #region Ssh Property
        private SshConnectInfo SshInfo { get; set; }
        private SshClient Ssh { get; set; }
        private ShellStream SshStream { get; set; }
        private string SshLastLocation { get; set; }
        #endregion

        public bool IsInSsh => Ssh?.IsConnected ?? false;
        public ShellRunner()
        {
            Shell = PowerShell.Create();
            Variable = new VariableModel();
            GetCurrentLocation();
        }

        #region Common Method
        public CommandResult Run(CommandLine Model, FileRunnerModel FileRunner = null)
        {
            if (Model.IsComment)
                return CommandResult.Next();

            if (string.IsNullOrWhiteSpace(Model.FullCommand))
                return CommandResult.Next();

            var GetVariable = FileRunner?.Variable ?? Variable;

            Model.WithVariable(GetVariable);

            var VariabledCommand = Model.VariabledCommand;
            Model = new CommandLine(VariabledCommand)
                .WithVariable(GetVariable);

            switch (Model.CommandType)
            {
                case CommandType.PrintMode:
                    ChangeRunMode(Model);
                    break;

                case CommandType.Run:
                    ImportFiles(Model);
                    break;
            }

            if (IsPrintMode)
                return CommandResult.Next();

            switch (Model.CommandType)
            {
                case CommandType.Ssh:
                    Console.WriteLine("Create new ssh connection\n");
                    var NewSshNext = NewSsh(Model);
                    return CommandResult.Next(NewSshNext);

                case CommandType.EndSsh:
                    EndSsh();
                    break;

                case CommandType.Scp:
                    ScpSend(Model);
                    break;

                case CommandType.Open:
                    OpenFolder(Model);
                    break;

                case CommandType.Back:
                    BackToStart();
                    break;

                case CommandType.Var:
                    var IsDeclareVar = DeclareVariable(Model, GetVariable);
                    return CommandResult.Next(IsDeclareVar);

                case CommandType.RmVar:
                    RemoveVariable(Model, GetVariable);
                    break;

                case CommandType.VarReq:
                    if (RunMode == RunnerMode.UserInput)
                    {
                        Console.WriteLine("Only file runner mode can register/remove required variables");
                        return CommandResult.Next(false);
                    }
                    else
                        RegistRequiredVariable(Model, FileRunner.RequiredVariable);
                    break;

                case CommandType.RmVarReq:
                    if (RunMode == RunnerMode.UserInput)
                    {
                        Console.WriteLine("Only file run mode can register/remove required variables");
                        return CommandResult.Next(false);
                    }
                    else
                        RemoveRequiredArgs(Model, FileRunner.RequiredVariable);
                    break;

                case CommandType.IfExist:
                    return If_FileExist(Model);

                case CommandType.EndIf:
                    return CommandResult.EndIf();

                case CommandType.None:
                default:
                    CommandSend(Model);
                    break;
            }
            return CommandResult.Next();
        }
        public void ChangeRunMode(CommandLine Model)
        {
            if (Model.Args.Count > 1)
            {
                var GetArg = Model.Args[1].ToLower();
                switch (GetArg)
                {
                    case "on":
                        IsPrintMode = true;
                        Console.WriteLine($"Print mode set「on」");
                        break;
                    case "off":
                        IsPrintMode = false;
                        Console.WriteLine($"Print mode set「off」");
                        break;
                    default:
                        var StatusText = IsPrintMode ? "on" : "off";
                        Console.WriteLine($"Print mode not set, and status is「{StatusText}」");
                        break;
                }
            }
            else
            {
                var StatusText = IsPrintMode ? "on" : "off";
                Console.WriteLine($"Print mode is「{StatusText}」\n");
            }
        }
        public void CommandSend(CommandLine Model)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region Shell
        public string GetCurrentLocation()
        {
            if (IsInSsh)
            {
                var SshRet = SshSend(new CommandLine("pwd"));
                var SshLocation = $"{SshInfo.UserName}@{SshInfo.Host}:{SshRet}";
                SshLastLocation = SshLocation;

                return SshLastLocation;
            }
            else
            {
                var ShellRet = ShellInvoke(new CommandLine("Get-Location"), false);
                var ShellLocation = ShellRet.Result.First().BaseObject.ToString();
                NowLocation = ShellLocation;
                StartPath ??= ShellLocation;
                return ShellLocation;
            }
        }
        private ShellResult ShellInvoke(CommandLine Model, bool IsOutString = true)
        {
            try
            {
                var Script = Model.FullCommand;
                if (IsOutString)
                    Script += " | Out-String";

                Shell.AddScript(Script);
                var GetStreams = Shell.Streams;
                var RunResult = Shell.Invoke();
                var ErrorMessage = string.Join("\n", GetStreams.Error
                    .Select(Item => Item.Exception.ToString()));
                var Result = new ShellResult()
                {
                    Command = Model,
                    Errors = GetStreams.Error.ToList(),
                    ErrorMessage = ErrorMessage,
                    Result = RunResult.ToList(),
                };

                GetStreams.Error.Clear();
                Shell.Commands.Clear();

                return Result;
            }
            catch (Exception ex)
            {
                Shell.Commands.Clear();
                return new ShellResult()
                {
                    Command = Model,
                    ErrorMessage = ex.ToString(),
                    Result = null,
                };
            }
        }
        private static void ShellPrint(ShellResult Model)
        {
            if (Model.IsHasErrors)
            {
                foreach (var Item in Model.Errors)
                    Console.WriteLine(Item);
            }
            else if (Model.IsHasErrorMessage)
                Console.WriteLine(Model.ErrorMessage);
            else
            {
                foreach (var Item in Model.Result)
                {
                    var PrintResult = Item.BaseObject.ToString();
                    Console.WriteLine(PrintResult);
                }
            }
            Console.WriteLine();
        }
        private void OpenFolder(CommandLine Model)
        {
            var GetArg = Model.PartValues.FirstOrDefault();
            var OpenPath = ".";
            if (GetArg is not null)
                OpenPath = GetArg;

            CommandSend(new CommandLine($"start {OpenPath}"));
        }
        private void BackToStart()
        {
            CommandSend(new CommandLine($"cd {StartPath}"));
        }
        private void ImportFiles(CommandLine Command)
        {
            var AllFile = Command.GetArgValues("-f");

            if (Command.OtherArgs.Any())
                AllFile.AddRange(Command.OtherArgs);

            RunMode = RunnerMode.FileRun;
            foreach (var FileName in AllFile)
            {
                var FileModel = new FileRunnerModel(Command)
                {
                    Id = Guid.NewGuid(),
                    FileName = FileName,
                };
                Console.WriteLine($"\nStart run file {FileModel.FileName}\n");
                RunFile(FileModel);
                Console.WriteLine($"\nFinish run file {FileModel.FileName}\n");
            }
        }
        private void RunFile(FileRunnerModel Model)
        {
            var FullFileName = Path.Combine(NowLocation, Model.FileName);
            if (!File.Exists(FullFileName))
            {
                Console.WriteLine($"File「{FullFileName}」is not found\n");
                return;
            }
            var AllLines = File.ReadAllLines(FullFileName);
            var DontRunUntilEndIf = false;
            foreach (var Line in AllLines)
            {
                if (string.IsNullOrWhiteSpace(Line))
                {
                    if (IsPrintMode)
                        Console.WriteLine();
                    continue;
                }

                var Command = new CommandLine(Line)
                    .WithVariable(Model.Variable)
                    .WithVariable(Model.CommandVariable);

                if (!Command.IsCanNext || !Command.CheckRequired(Model.RequiredVariable))
                    return;

                if (DontRunUntilEndIf && !IsPrintMode)
                {
                    if (Command.CommandType == CommandType.EndIf)
                        DontRunUntilEndIf = false;
                    else
                        continue;
                }

                #region Print Command
                if (!Command.IsComment)
                    Console.WriteLine($"> {Command.VariabledCommand}");
                #endregion
                var Result = Run(Command, Model);

                if (!Result.IsCanNext)
                    return;

                if (Command.CommandType == CommandType.IfExist && !Result.IsIfTrue)
                    DontRunUntilEndIf = true;
            }
        }
        private static bool DeclareVariable(CommandLine Model, VariableModel SetVariable)
        {
            if (Model.CommandVariable.HasVariable)
            {
                foreach (var Item in Model.CommandVariable.Variable)
                {
                    SetVariable.WithVariable(Item.Key, Item.Value);
                    Console.WriteLine($"Declare variable {Item.Key} = {Item.Value}");
                }
                Console.WriteLine();
                return true;
            }
            else if (Model.RunnerVariableArgs.Any())
            {
                var VariableKey = Model.RunnerVariableArgs.First();
                if (!Model.Variable.TryGetVariable(VariableKey, out var Value))
                {
                    Console.WriteLine($"Variable {VariableKey} not found\n");
                    return false;
                }
                Console.WriteLine($"Variable {VariableKey} = {Value}\n");
            }
            return true;
        }
        private static void RemoveVariable(CommandLine Model, VariableModel SetVariable)
        {
            if (!Model.VariableKeys.Any())
            {
                Console.WriteLine($"no variables are removed");
                return;
            }
            foreach (var Item in Model.VariableKeys)
            {
                if (SetVariable.TryRemoveVariable(Item))
                    Console.WriteLine($"Variable {Item} has been removed");
            }
        }
        private static void RegistRequiredVariable(CommandLine Model, List<string> Required)
        {
            foreach (var Item in Model.VariableArgs)
            {
                if (Required.Contains(Item))
                    Console.WriteLine($"Variable {Item} is already regist required list");
                else
                {
                    Required.Add(Item);
                    Console.WriteLine($"Regist required variable: {Item}");
                }
            }
        }
        private static void RemoveRequiredArgs(CommandLine Model, List<string> Required)
        {
            if (!Model.VariableKeys.Any())
            {
                Console.WriteLine($"No variables are removed from required list");
                return;
            }
            foreach (var Item in Model.VariableKeys)
            {
                if (Required.Contains(Item))
                {
                    Required.Remove(Item);
                    Console.WriteLine($"Variable {Item} has been removed from required list");
                }
            }
        }
        private CommandResult If_FileExist(CommandLine Model)
        {
            var AllFileName = Model.Args.Skip(1);

            var IsAllFileExist = true;
            foreach (var Item in AllFileName)
            {
                var FullFileName = Path.Combine(NowLocation, Item);
                var IsExist = File.Exists(FullFileName);
                IsAllFileExist = IsAllFileExist && IsExist;
                var IsExistText = IsExist ? "exist" : "not exist";
                Console.WriteLine($"File「{Item}」is {IsExistText} : {IsExist}");
            }

            Console.WriteLine();
            if (RunMode == RunnerMode.FileRun && IsAllFileExist)
                return CommandResult.IfTrue();

            return CommandResult.EndIf();
        }
        #endregion

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

                        SshStream = Ssh.CreateShellStream("", 0, 0, 0, 0, 0);
                        var ConnectResult = GetSshResult();
                        Console.WriteLine(ConnectResult);
                        Console.WriteLine();
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
        private string SshSend(CommandLine Model, double TimeOutSec = 0.5)
        {
            var NoReadCmd = new[]
            {
                "cd",
                SshInfo.Password,
            };

            if (Model.FullCommand.Contains("sudo"))
            {
                SshStream.WriteLine(Model.FullCommand);
                SshStream.Expect("password", TimeSpan.FromSeconds(0.5));
                SshStream.WriteLine(SshInfo.Password);
            }
            else
            {
                SshStream.WriteLine(Model.FullCommand);
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
        private static void SshPrint(string SshResult, CommandLine Model)
        {
            Console.WriteLine($"{SshResult}");
            Console.WriteLine();
        }
        #endregion

        #region Scp
        public static void ScpSend(CommandLine Model)
        {
            var ScpInfo = new ScpConnectInfo(Model);

            if (!ScpInfo.IsRequiredCheck)
                return;

            var ScpClient = new SftpClient(ScpInfo.Host, ScpInfo.Port, ScpInfo.UserName, ScpInfo.Password);
            try
            {
                ScpClient.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
                return;
            }

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

                Console.WriteLine($"scp upload file {Info.Name} finish\n");
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

                Console.WriteLine($"scp download file {Info.Name} finish\n");
            }

            ScpClient.Disconnect();
            ScpClient.Dispose();
        }

        #endregion
    }
}
