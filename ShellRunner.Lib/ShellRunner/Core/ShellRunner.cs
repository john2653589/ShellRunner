using Rugal.ShellRunner.Model;
using System.Management.Automation;

namespace Rugal.ShellRunner.Core
{
    public partial class ShellRunner
    {
        public ShellRunner()
        {
            Shell = PowerShell.Create();
            Variable = new VariableModel();
            GetLocation();
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

                case CommandType.Invoke:
                    IsInvokeMode = true;
                    break;
                case CommandType.EndInvoke:
                    IsInvokeMode = false;
                    break;
                case CommandType.Clr:
                    Console.Clear();
                    break;
                case CommandType.Position:
                    IsPositionLock = true;
                    PositionY = Console.CursorTop + 1;
                    MaxPositionY = -1;
                    Console.WriteLine($"Set position lock on y:{PositionY}");
                    break;
                case CommandType.EndPosition:
                    IsPositionLock = false;
                    Console.WriteLine($"Set position lock off");

                    break;
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
                    SshSend(Model);
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
    }
}
