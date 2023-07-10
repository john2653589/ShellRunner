using Rugal.ShellRunner.Core;
using Rugal.ShellRunner.Model;
using System.Text;

const string Version = "1.0.7";

Console.WriteLine($"Shell Runner v{Version} From Rugal");
Console.OutputEncoding = Encoding.UTF8;

var Args = Environment.GetCommandLineArgs().Skip(1).ToArray();
var Runner = new ShellRunner();

if (Args.Length > 0)
{
    var CommandLine = string.Join(' ', Args);
    var Command = new CommandLine($"#run {CommandLine}");
    Console.WriteLine("CommandLine args mode");
    Runner.Run(Command);
}
else
{
    Console.WriteLine("User input mode");
    UserLoop();
}
void UserLoop()
{
    while (true)
    {
        Runner.PrintLocation();

        var Input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(Input))
        {
            if (Runner.IsInSsh)
            {
                Runner.LastSshCommandText = "";
                Runner.SshSendText("");
                Runner.WaitSshResult();
            }
            continue;
        }

        Runner.RunMode = RunnerMode.UserInput;
        if (Input.ToLower() == "exit")
            break;

        var Command = new CommandLine(Input);
        Runner.Run(Command);

        Task.Delay(10).Wait();
    }
}