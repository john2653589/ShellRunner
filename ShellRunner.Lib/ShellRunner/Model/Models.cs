using Microsoft.CodeAnalysis;
using System.Management.Automation;

namespace Rugal.ShellRunner.Model
{
    public class CommandLineModel
    {
        public string CommandLine { get; set; }
        public string CommandName { get; set; }
        public List<KeyValuePair<string, string>> CommandArgs { get; set; }
        public List<KeyValuePair<string, string>> ParamValues { get; set; }
        public List<string> ParamArgs { get; set; }
        public List<string> OtherArgs { get; set; }
        public List<string> AllArgs { get; set; }
        private string[] CommandArray { get; set; }

        public CommandLineModel(string _CommandLine)
        {
            CommandLine = _CommandLine.TrimStart().TrimEnd();
            InitCommandLine();
        }
        private void InitCommandLine()
        {
            CommandArray = CommandLine.Split(' ');
            CommandName = CommandArray.FirstOrDefault();
            CommandArgs = new List<KeyValuePair<string, string>> { };
            ParamValues = new List<KeyValuePair<string, string>> { };
            ParamArgs = new List<string> { };
            OtherArgs = new List<string> { };
            AllArgs = new List<string> { };
            var Key = "";
            foreach (var Item in CommandArray.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(Item))
                    continue;

                if (Item.Length >= 2 && Item[..2] == "-@")
                    ParamArgs.Add(Item);

                if (Item.First() == '-')
                    Key = Item;
                else if (Key != "")
                {
                    CommandArgs.Add(new KeyValuePair<string, string>(Key, Item));
                    AllArgs.Add(Item);

                    if (Key[..2] == "-@")
                    {
                        ParamValues.Add(new KeyValuePair<string, string>(Key, Item));
                    }

                    Key = "";
                }
                else
                {
                    OtherArgs.Add(Item);
                    AllArgs.Add(Item);
                }
            }
        }
        public string GetArg(string Key)
        {
            var Arg = CommandArgs.FirstOrDefault(Item => Item.Key.ToLower() == Key.ToLower());
            return Arg.Value;
        }
        public List<string> GetArgs(string Key)
        {
            var Args = CommandArgs
                .Where(Item => Item.Key.ToLower() == Key.ToLower())
                .Select(Item => Item.Value)
                .ToList();

            return Args;
        }

        public bool TryGetArg(string Key, out string Arg)
        {
            Arg = GetArg(Key);
            if (Arg is null)
                return false;
            return true;
        }

        public string GetParamValue(string Key)
        {
            var Arg = ParamValues.FirstOrDefault(Item => Item.Key.ToLower() == Key.ToLower());
            return Arg.Value;
        }

        public bool TryGetParamValue(string Key, out string ParamArg)
        {
            ParamArg = GetParamValue(Key);
            if (ParamArg is null)
                return false;
            return true;
        }
    }
    public class ShellResult
    {
        public CommandLineModel CommandLineInfo { get; set; }
        public List<ErrorRecord> Errors { get; set; }
        public List<PSObject> Result { get; set; }
        public bool IsHasError => Errors.Any();
        public bool IsHasResult => Result.Any();
    }

    public class SshConnectInfo
    {
        public CommandLineModel CommandInfo { get; set; }
        public string Host { get; set; }
        public string UserName { get; set; } = "root";
        public string Password { get; set; }
        public int Port { get; set; } = 22;
        public bool IsRequiredCheck { get; set; }
        public SshConnectInfo(CommandLineModel _CommandInfo)
        {
            CommandInfo = _CommandInfo;
            InitInfo();
        }
        private void InitInfo()
        {
            Host = CommandInfo.AllArgs.FirstOrDefault();

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
        public CommandLineModel CommandInfo { get; set; }
        public string Host { get; set; }
        public string UserName { get; set; } = "root";
        public string Password { get; set; }
        public int Port { get; set; } = 22;
        public bool IsUpload { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public bool IsRequiredCheck { get; set; }
        public ScpConnectInfo(CommandLineModel _CommandInfo)
        {
            CommandInfo = _CommandInfo;
            InitInfo();
        }
        private void InitInfo()
        {
            var AllArgs = CommandInfo.AllArgs;

            Host = AllArgs.FirstOrDefault(Item => Item.Contains(':'));
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
}