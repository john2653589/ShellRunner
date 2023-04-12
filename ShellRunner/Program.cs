using Rugal.ShellRunner.Core;
using Rugal.ShellRunner.Model;

var Args = Environment.GetCommandLineArgs().Skip(1).ToArray();

var Runner = new ShellRunner();

if (Args.Length > 0)
{
    var CommandLine = string.Join(' ', Args);
    var CommandModel = new CommandLineModel(CommandLine);
    Console.WriteLine("CommandLine Args Mode");
    RunCommandModel(CommandModel);
}
else
{
    Console.WriteLine("User Input Mode");
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

        if (Input.ToLower() == "exit")
            break;

        var Model = new CommandLineModel(Input);
        RunCommandModel(Model);
    }
}
void RunCommandModel(CommandLineModel Model)
{
    Runner.Run(Model);
}