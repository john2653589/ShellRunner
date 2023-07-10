using Microsoft.CodeAnalysis;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace Rugal.ShellRunner.Model
{
    public class CommandPart
    {
        public int Index { get; set; }
        public List<string> Args { get; set; }
        public List<string> VariableArgs => Args
            .Where(Item => Item.First() == '-')
            .ToList();
        public List<string> RunnerVariableArgs => Args
            .Where(Item => Item[..2] == "-@")
            .ToList();
        public bool HasVariableArgs => VariableArgs.Any();
        public bool HasRunnerVariableArgs => RunnerVariableArgs.Any();

        public string Key { get; set; }
        public string Value { get; set; }
        public bool IsCommandName => Index == 0;
        public bool IsParam => !string.IsNullOrWhiteSpace(Key);
        public bool IsVariableKey => IsParam && Key.First() == '-';
        public bool IsVariableValue => Value.First() == '-';
        public bool IsRunnerVariableKey => IsParam && Key[..2] == "-@"; // -@a 123
        public bool IsRunnerVariableValue => Value.Length >= 2 && Value[..2] == "-@"; // -p -@a
        public bool IsOtherArg => !IsParam && !IsCommandName;
        public CommandPart(int _Index)
        {
            Index = _Index;
        }
        public CommandPart(int _Index, string _Key, string _Value) : this(_Index)
        {
            Key = _Key;
            Value = _Value;
            Args = new List<string> { Key, Value };
        }
        public CommandPart(int _Index, List<string> KeyValue) : this(_Index)
        {
            Value = KeyValue.Last();
            Args = new List<string> { };
            Args.AddRange(KeyValue);

            if (KeyValue.Count > 1)
                Key = KeyValue.First();
        }
    }
    public class CommandLine
    {
        public bool IsComment => Regex.IsMatch(FullCommand, "^--");
        public string FullCommand { get; set; }
        public List<CommandPart> Parts { get; set; }
        public List<string> Args => Parts
            .SelectMany(Item => Item.Args)
            .ToList();
        public List<string> PartValues => Parts
            .Skip(1)
            .Select(Item => Item.Value)
            .ToList();
        public List<CommandPart> CommandParam => Parts
            .Where(Item => Item.IsParam)
            .ToList();
        public List<string> VariableKeys => Parts
            .Where(Item => Item.IsVariableKey)
            .Select(Item => Item.Key)
            .ToList();
        public List<string> OtherArgs => Parts
            .Where(Item => Item.IsOtherArg)
            .Select(Item => Item.Value)
            .ToList();
        public List<string> VariableArgs => Parts
            .Where(Item => Item.HasVariableArgs)
            .SelectMany(Item => Item.VariableArgs)
            .ToList();
        public List<string> RunnerVariableArgs => Parts
            .Where(Item => Item.HasRunnerVariableArgs)
            .SelectMany(Item => Item.RunnerVariableArgs)
            .ToList();
        public string CommandName => Args.First();
        public CommandType CommandType => GetCommandType(CommandName);
        public VariableModel CommandVariable { get; set; }
        public VariableModel Variable { get; set; }
        public string VariabledCommand => GetVariabledCommand();
        public bool IsCanNext { get; set; }
        public CommandLine(string _CommandLine)
        {
            FullCommand = _CommandLine.TrimStart().TrimEnd();
            CommandVariable = new VariableModel();
            Variable = new VariableModel();
            InitVarible();
        }
        private void InitVarible()
        {
            Parts = GetCommandPart(FullCommand);
            var RunnerVarible = Parts
                .Where(Item => Item.IsRunnerVariableKey);

            CommandVariable.WithVariables(RunnerVarible);
            if (CommandVariable.HasVariable)
                Variable.WithVariable(CommandVariable);
            IsCanNext = true;
        }

        #region Private Get Property
        private static List<CommandPart> GetCommandPart(string Command)
        {
            var RetParts = new List<List<string>>();
            var CatchNext = false;
            var CommandArray = Command.Split(' ');
            var CommandType = GetCommandType(CommandArray.First());

            var ComandIndex = 0;
            foreach (var Item in CommandArray)
            {
                var CatchMode = CheckCatch(CommandType, out var SkipCount);
                if (CatchNext)
                {
                    var IsAddLast = false;
                    switch (CatchMode)
                    {
                        case PartCatchMode.AwaylsCatch:
                            IsAddLast = true;
                            break;
                        case PartCatchMode.AwaylsSingle:
                            IsAddLast = false;
                            break;
                        case PartCatchMode.ValueDashStart:
                            IsAddLast = Item.First() == '-';
                            break;
                        case PartCatchMode.ValueNotDashStart:
                            IsAddLast = Item.First() != '-';
                            break;
                        case PartCatchMode.ValueDashStartButNotDashAt:
                            IsAddLast = Item.First() == '-' && !(Item.Length >= 2 && Item[..2] == "-@");
                            break;
                        case PartCatchMode.ValueDashAt:
                            IsAddLast = Item.Length >= 2 && Item[..2] == "-@";
                            break;
                        case PartCatchMode.ValueNotDashAtStart:
                            IsAddLast = !(Item.Length >= 2 && Item[..2] == "-@");
                            break;
                    }

                    CatchNext = false;
                    if (IsAddLast)
                    {
                        var Last = RetParts.Last();
                        Last.Add(Item);
                        continue;
                    }
                }
                else if (Item.First() == '-')
                {
                    CatchNext = true;
                }
                else
                    CatchNext = false;

                if (ComandIndex < SkipCount)
                    CatchNext = false;

                RetParts.Add(new List<string> { Item });
                ComandIndex++;
            }

            var Ret = RetParts
                .Select((Item, Index) => new CommandPart(Index, Item))
                .ToList();

            return Ret;
        }
        private static PartCatchMode CheckCatch(CommandType Type, out int SkipCount)
        {
            SkipCount = 0;

            var Ret = PartCatchMode.ValueNotDashStart;
            switch (Type)
            {
                case CommandType.Var:
                    Ret = PartCatchMode.AwaylsCatch;
                    break;
                case CommandType.IfExist:
                    Ret = PartCatchMode.AwaylsSingle;
                    break;
                case CommandType.Ssh:
                    Ret = PartCatchMode.AwaylsCatch;
                    SkipCount = 2;
                    break;
                case CommandType.Run:
                    Ret = PartCatchMode.AwaylsCatch;
                    SkipCount = 2;
                    break;
            }

            return Ret;
        }
        private static CommandType GetCommandType(string CommandName)
        {
            var ClearCommandName = CommandName
                .Replace("#", "")
                .Replace("-", "");

            if (Enum.TryParse<CommandType>(ClearCommandName, true, out var GetType))
                return GetType;

            return CommandType.None;
        }
        #endregion

        #region Variable Controller
        public CommandLine WithVariable(Dictionary<string, string> SetVariableTable, List<string> RequiredVariable = null)
        {
            Variable.WithVariable(SetVariableTable);
            if (RequiredVariable is not null)
                CheckRequired(RequiredVariable);

            return this;
        }
        public CommandLine WithVariable(VariableModel Variable, List<string> RequiredVariable = null)
        {
            WithVariable(Variable.Variable, RequiredVariable);
            return this;
        }
        public bool CheckRequired(List<string> RequiredVariable)
        {
            if (!Variable.CheckRequired(RequiredVariable, out var lostVariable))
            {
                Console.WriteLine($"\nVariable「{lostVariable}」is required\n");
                IsCanNext = false;
                return false;
            }
            return true;
        }
        public string GetArgValue(string Key)
        {
            var Arg = CommandParam
                .FirstOrDefault(Item => Item.Key.ToLower() == Key.ToLower());
            return Arg?.Value;
        }
        public List<string> GetArgValues(string Key)
        {
            var Args = CommandParam
                .Where(Item => Item.Key.ToLower() == Key.ToLower())
                .Select(Item => Item.Value)
                .ToList();

            return Args;
        }
        public bool TryGetArg(string Key, out string Arg)
        {
            Arg = GetArgValue(Key);
            if (Arg is null)
                return false;
            return true;
        }
        private string GetVariabledCommand()
        {
            var AllPart = Parts.ToArray();
            foreach (var Item in Variable.Variable)
            {
                var ReplaceParts = AllPart
                    .Where(Val =>
                    {
                        var IsRunnerValue = Val.IsRunnerVariableValue;
                        var MatchValue = IsRunnerValue && Val.Value == Item.Key;
                        var MatchAny = IsRunnerValue && (Val.Value == Item.Value || Val.Key == Item.Value);
                        if (CommandType != CommandType.Var)
                        {
                            return MatchValue;
                        }
                        else
                        {
                            if (CommandVariable.HasVariable)
                                return MatchValue;
                            else
                                return MatchAny;
                        }
                    });

                foreach (var Val in ReplaceParts)
                {
                    Val.Value = Item.Value;
                }
            }

            foreach (var Item in AllPart)
            {
                Item.Key = ReplaceVarConvert(Item.Key);
                Item.Value = ReplaceVarConvert(Item.Value);

                Item.Key = ReplaceVarInsertCovnert(Item.Key);
                Item.Value = ReplaceVarInsertCovnert(Item.Value);
            }

            var ConvertPart = AllPart
                .Select(Item =>
                    string.Join(" ", new[] { Item.Key, Item.Value })
                    .TrimStart()
                    .TrimEnd());

            var RetCommand = string.Join(" ", ConvertPart);
            return RetCommand;
        }
        private string ReplaceVarConvert(string Value)
        {
            if (string.IsNullOrWhiteSpace(Value))
                return Value;

            if (!Regex.IsMatch(Value, @"^-#var\("))
                return Value;

            var SplitArray = Regex
                .Split(Value, @"(-#var\(.+?\))")
                .Where(Item => !string.IsNullOrWhiteSpace(Item));

            var Ret = "";
            foreach (var Item in SplitArray)
            {
                if (!Regex.IsMatch(Item, @"^-#var\("))
                {
                    Ret += Item;
                    continue;
                }

                var VariableKey = Regex
                    .Replace(Item, @"-#var\((.+?)\)", "$1")
                    .TrimStart()
                    .TrimEnd();

                if (Variable.TryGetVariable(VariableKey, out var VariableValue))
                {
                    Ret += VariableValue;
                    continue;
                }
                Ret += Item;
            }
            return Ret;
        }
        private string ReplaceVarInsertCovnert(string Value)
        {
            if (string.IsNullOrWhiteSpace(Value))
                return Value;

            var Pattern = @"\{.+?\}";
            var VarInsertChat = "^-#\\$";
            if (!Regex.IsMatch(Value, VarInsertChat))
                return Value;

            Value = Regex.Replace(Value, VarInsertChat, "");
            var SplitArray = Regex
                .Split(Value, $"({Pattern})")
                .Where(Item => !string.IsNullOrWhiteSpace(Item));

            var Ret = "";
            foreach (var Item in SplitArray)
            {
                if (!Regex.IsMatch(Item, @"^\{.+?\}$"))
                {
                    Ret += Item;
                    continue;
                }

                var VariableKey = Regex
                    .Replace(Item, @"[{}]", "")
                    .TrimStart()
                    .TrimEnd();

                if (Variable.TryGetVariable(VariableKey, out var VariableValue))
                {
                    Ret += VariableValue;
                    continue;
                }
                Ret += Item;
            }
            return Ret;
        }
        #endregion
    }
    public class CommandResult
    {
        public bool IsCanNext { get; set; }
        public bool IsIfTrue { get; set; }
        public bool IsEndIf { get; set; }

        public static CommandResult Next(bool IsCanNext = true)
        {
            return new CommandResult()
            {
                IsCanNext = IsCanNext,
            };
        }
        public static CommandResult IfTrue(bool IsCanNext = true)
        {
            return new CommandResult()
            {
                IsCanNext = IsCanNext,
                IsIfTrue = true,
            };
        }
        public static CommandResult EndIf(bool IsCanNext = true)
        {
            return new CommandResult()
            {
                IsCanNext = IsCanNext,
                IsEndIf = true,
            };
        }
    }
    public class ShellResult
    {
        public CommandLine Command { get; set; }
        public string ErrorMessage { get; set; }
        public List<ErrorRecord> Errors { get; set; }
        public List<PSObject> Result { get; set; }
        public bool IsHasErrors => Errors?.Any() ?? false;
        public bool IsHasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
        public bool IsHasResult => Result?.Any() ?? false;
    }
    public class FileRunnerModel
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public CommandLine Command { get; set; }
        public VariableModel Variable => Command.Variable;
        public VariableModel CommandVariable => Command.CommandVariable;
        public List<string> RequiredVariable { get; set; }
        public FileRunnerModel(CommandLine _Command)
        {
            Command = _Command;
            RequiredVariable = new List<string> { };
        }
    }
    public class VariableModel
    {
        public Dictionary<string, string> Variable { get; set; }
        public bool HasVariable => Variable?.Any() ?? false;
        public VariableModel()
        {
            Variable = new Dictionary<string, string> { };
        }

        public VariableModel WithVariable(VariableModel SetVariable)
        {
            WithVariable(SetVariable.Variable);
            return this;
        }
        public VariableModel WithVariable(Dictionary<string, string> SetVariableTable)
        {
            foreach (var Item in SetVariableTable)
            {
                if (Variable.ContainsKey(Item.Key))
                    Variable.Remove(Item.Key);

                Variable.Add(Item.Key, Item.Value);
            }
            return this;
        }
        public VariableModel WithVariable(string Key, string Value)
        {
            if (Variable.ContainsKey(Key))
                Variable.Remove(Key);

            Variable.Add(Key, Value);
            return this;
        }
        public VariableModel WithVariable(CommandPart Part)
        {
            WithVariable(Part.Key, Part.Value);
            return this;
        }
        public VariableModel WithVariables(IEnumerable<CommandPart> Parts)
        {
            foreach (var Part in Parts)
                WithVariable(Part);
            return this;
        }
        public bool CheckRequired(List<string> RequiredVariable, out string LostVariable)
        {
            LostVariable = null;
            foreach (var Item in RequiredVariable)
            {
                if (!RequiredVariable.Contains(Item))
                    continue;

                if (!Variable.ContainsKey(Item))
                {
                    LostVariable = Item;
                    return false;
                }
            }
            return true;
        }
        public bool TryGetVariable(string VariableKey, out string VariableValue)
        {
            VariableValue = VariableKey;
            if (string.IsNullOrWhiteSpace(VariableKey))
                return false;

            if (VariableKey.Length < 2)
                return false;

            if (VariableKey[..2] != "-@")
                VariableKey = $"-@{VariableKey}";

            if (!Variable.TryGetValue(VariableKey, out VariableValue))
            {
                VariableValue = VariableKey;
                return false;
            }

            return true;
        }
        public bool TryRemoveVariable(string VariableKey)
        {
            if (Variable.ContainsKey(VariableKey))
            {
                Variable.Remove(VariableKey);
                return true;
            }
            return false;
        }
    }
    public class SshConnectInfo
    {
        public CommandLine CommandInfo { get; set; }
        public string Host { get; set; }
        public string UserName { get; set; } = "root";
        public string Password { get; set; }
        public int Port { get; set; } = 22;
        public bool IsRequiredCheck { get; set; }
        public SshConnectInfo(CommandLine _CommandInfo)
        {
            CommandInfo = _CommandInfo;
            InitInfo();
        }
        private void InitInfo()
        {
            Host = CommandInfo.PartValues.FirstOrDefault();

            if (Host is null)
            {
                Console.WriteLine("Ssh Host is required");
                return;
            }

            if (Host.Contains('@'))
            {
                var HostArray = Host.Split('@');
                Host = HostArray[1];
                UserName = HostArray[0];
            }

            if (!CommandInfo.TryGetArg("-#p", out var GetPassword))
            {
                Console.WriteLine("Ssh Password is required");
                return;
            }
            else
                Password = GetPassword;

            if (CommandInfo.TryGetArg("-p", out var GetPort))
            {
                if (int.TryParse(GetPort, out var IntPort))
                    Port = IntPort;
            }

            IsRequiredCheck = true;
        }
    }
    public class ScpConnectInfo
    {
        public CommandLine CommandInfo { get; set; }
        public string Host { get; set; }
        public string UserName { get; set; } = "root";
        public string Password { get; set; }
        public int Port { get; set; } = 22;
        public bool IsUpload { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public bool IsRequiredCheck { get; set; }
        public ScpConnectInfo(CommandLine _CommandInfo)
        {
            CommandInfo = _CommandInfo;
            InitInfo();
        }
        private void InitInfo()
        {
            Host = CommandInfo.PartValues
                .FirstOrDefault(Item => Item.Contains(':'));

            if (Host is null)
            {
                Console.WriteLine("Scp Host is required");
                return;
            }

            if (Host.Contains('@'))
            {
                var HostArray = Host.Split('@');
                Host = HostArray[1].Split(':')[0];
                UserName = HostArray[0];
            }

            if (!CommandInfo.TryGetArg("-#p", out var GetPassword))
            {
                Console.WriteLine("Scp Password is required using「-#p」args.");
                return;
            }
            else
                Password = GetPassword;

            if (CommandInfo.TryGetArg("-p", out var GetPort))
            {
                if (int.TryParse(GetPort, out var IntPort))
                    Port = IntPort;
            }

            foreach (var Item in CommandInfo.OtherArgs)
            {
                if (Item.Contains(':'))
                {
                    IsUpload = true;
                    var RemoteBodyArray = Item.Split(':');
                    RemotePath = RemoteBodyArray[1];
                }
                else
                {
                    IsUpload = false;
                    LocalPath = Item;
                }
            }
            IsRequiredCheck = true;
        }
    }

    public enum RunnerMode
    {
        UserInput,
        FileRun,
    }
    public enum CommandType
    {
        None,
        PrintMode,
        Ssh,
        EndSsh,
        Scp,
        Run,
        Open,
        Back,
        Var,
        RmVar,
        VarReq,
        RmVarReq,
        IfExist,
        EndIf,
        Invoke,
        EndInvoke,
        Clr,
        Position,
        EndPosition,
    }
    public enum PartCatchMode
    {
        AwaylsCatch,
        AwaylsSingle,
        ValueNotDashStart,
        ValueNotDashAtStart,
        ValueDashStart,
        ValueDashStartButNotDashAt,
        ValueDashAt,
    }
}