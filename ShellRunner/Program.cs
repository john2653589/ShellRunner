using Rugal.ShellRunner.Core;
using Rugal.ShellRunner.Model;

const string Version = "1.0.5";

Console.WriteLine($"Shell Runner v{Version} From Rugal");

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
        var Location = Runner.GetCurrentLocation();
        Console.Write($"{Location}> ");

        var Input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(Input))
            continue;

        Runner.RunMode = RunnerMode.UserInput;
        if (Input.ToLower() == "exit")
            break;

        var Command = new CommandLine(Input);
        Runner.Run(Command);
    }
}