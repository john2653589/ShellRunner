using Rugal.ShellRunner.Model;
using System.Management.Automation;

namespace Rugal.ShellRunner.Core
{
    public partial class ShellRunner
    {
        #region Shell Property
        private PowerShell Shell { get; set; }
        private string StartPath { get; set; }
        public VariableModel Variable { get; set; }
        public RunnerMode RunMode { get; set; }
        public string NowLocation { get; set; }
        public bool IsPrintMode { get; set; }
        public bool IsInvokeMode { get; set; }
        #endregion

        #region Shell
        public void PrintLocation()
        {
            if (IsInSsh)
                return;

            var ShellRet = ShellInvoke(new CommandLine("Get-Location"), false);
            var ShellLocation = ShellRet.Result?.FirstOrDefault()?.BaseObject?.ToString();
            NowLocation = ShellLocation;
            StartPath ??= ShellLocation;
            Console.Write($"{ShellLocation}> ");
        }
        private ShellResult ShellInvoke(CommandLine Model, bool IsOutString = true)
        {
            IAsyncResult AsyncResult = null;
            var GetStreams = Shell.Streams;

            try
            {
                var Script = Model.FullCommand;
                if (!IsInvokeMode && IsOutString)
                    Script += " | Out-String";

                Shell.AddScript(Script);

                if (IsInvokeMode)
                    AsyncResult = ShellInvokeSync(GetStreams);

                var RunResult = !IsInvokeMode ? Shell.Invoke() : null;
                var ErrorMessage = string.Join("\n", GetStreams.Error
                    .Select(Item => Item.Exception.ToString()));

                var Result = new ShellResult()
                {
                    Command = Model,
                    Errors = GetStreams.Error.ToList(),
                    ErrorMessage = ErrorMessage,
                    Result = RunResult?.ToList(),
                };

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
            finally
            {
                GetStreams.ClearStreams();
                Shell.Commands.Clear();

                if (IsInvokeMode)
                {
                    Shell.EndInvoke(AsyncResult);
                }
            }
        }
        private IAsyncResult ShellInvokeSync(PSDataStreams GetStreams)
        {
            GetStreams.Progress.DataAdded += (s, e) =>
            {
                var Result = s as PSDataCollection<ProgressRecord>;
                var PrintResult = Result.First().ToString();
                Console.WriteLine(PrintResult);
            };
            GetStreams.Information.DataAdded += (s, e) =>
            {
                var Result = s as PSDataCollection<InformationRecord>;
                var PrintResult = Result.First().ToString();
                Console.WriteLine(PrintResult);
            };
            var Input = new PSDataCollection<PSObject>();
            var Output = new PSDataCollection<PSObject>();
            Output.DataAdded += (s, e) =>
            {
                var Result = s as PSDataCollection<PSObject>;
                var Text = Result[e.Index].ToString();
                Console.WriteLine(Text);
            };
            var Result = Shell.BeginInvoke(Input, Output);
            return Result;
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
            else if (Model.Result is not null)
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
    }
}
