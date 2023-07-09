using Renci.SshNet;
using Rugal.ShellRunner.Model;
using System.Management.Automation;
using System.Text;

namespace Rugal.ShellRunner.Core
{
    public partial class ShellRunner
    {
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
