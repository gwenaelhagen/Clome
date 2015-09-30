using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clome
{
    class Program
    {
        [STAThread] // give access to the clipboard
        static void Main(string[] args)
        {
            Arguments arguments;

            if (!TryParseArgs(args, out arguments))
            {
                // todo: Usage();
                return; // code error
            }

            // todo: what to do if forkName == upstream ?

            var content = Properties.Resources.clome;

            content = String.Format(content,
                arguments.RemoteName, arguments.DevelopLocalBranchName,
                arguments.LocalRepoFolder,
                arguments.LocalPRRepoFolder, arguments.FeatureName,
                arguments.ForkName, arguments.ForkUrl,
                Path.GetTempFileName(), Path.GetTempFileName(), Path.GetTempFileName(), Path.GetTempFileName());

            var batFilePath = Path.GetTempFileName() + ".bat";

            File.WriteAllText(batFilePath, content);

            RunBatchFile(batFilePath);

            // delete batch file
            File.Delete(batFilePath);
        }

        private class Arguments
        {
            public string LocalRepoFolder { get; set; }
            public string DevelopLocalBranchName { get; set; }
            
            public string RemoteName { get; set; }
             
            public string LocalPRRepoFolder;
             
            public string ForkUrl { get; set; }
            public string ForkName { get; set; }
            
            public string FeatureName { get; set; }

            public bool MissingValues()
            {
                return String.IsNullOrEmpty(LocalRepoFolder)
                    || String.IsNullOrEmpty(DevelopLocalBranchName)
                    || String.IsNullOrEmpty(RemoteName)
                    || String.IsNullOrEmpty(LocalPRRepoFolder)
                    || String.IsNullOrEmpty(ForkUrl)
                    || String.IsNullOrEmpty(ForkName)
                    || String.IsNullOrEmpty(FeatureName);
            }
        }
        
        private static bool TryParseArgs(string[] args, out Arguments arguments)
        {
            arguments = null;
            
            int argsLength = args != null ? args.Length : 0;

            if (argsLength == 0)
                return false;

            int i = 0;
            string flag;
            string value;

            #region get values from command line arguments
            while (i < argsLength)
            {
                if (!TryGetArgValue(ref i, args, i == 0, out flag, out value))
                    return false;

                arguments = arguments ?? new Arguments();

                switch(flag)
                {
                    case "--repoFolder":
                    case "-rf":
                    case "":
                        arguments.LocalRepoFolder = value;
                        PrintArgumentSetFromCmdLine("LocalRepoFolder", arguments.LocalRepoFolder);
                        break;

                    case "--developBranch":
                    case "-db":
                        arguments.DevelopLocalBranchName = value;
                        PrintArgumentSetFromCmdLine("DevelopLocalBranchName", arguments.DevelopLocalBranchName);
                        break;

                    case "--remoteName":
                    case "-rn":
                        arguments.RemoteName = value;
                        PrintArgumentSetFromCmdLine("RemoteName", arguments.RemoteName);
                        break;

                    case "--prFolder":
                    case "-pf":
                        arguments.LocalPRRepoFolder = value;
                        PrintArgumentSetFromCmdLine("LocalPRRepoFolder", arguments.LocalPRRepoFolder);
                        break;

                    case "--forkUrl":
                    case "-fu":
                        arguments.ForkUrl = value;
                        PrintArgumentSetFromCmdLine("ForkUrl", arguments.ForkUrl);
                        break;

                    case "--forkName":
                    case "-fn":
                        arguments.ForkName = value;
                        PrintArgumentSetFromCmdLine("ForkName", arguments.ForkName);
                        break;

                    case "--featureName":
                    case "-f":
                        arguments.FeatureName = value;
                        PrintArgumentSetFromCmdLine("FeatureName", arguments.FeatureName);
                        break;
                }
            }
            #endregion
            
            if (arguments == null || arguments.MissingValues())
            {
                #region get missing values from clipboard
                
                if (!Clipboard.ContainsText())
                    return false;

                string clipboardContent = Clipboard.GetText(TextDataFormat.UnicodeText);
                clipboardContent = clipboardContent.Trim();

                string folderSuffix;
                
                using (var sr = new StringReader(clipboardContent))
                {
                    string line = sr.ReadLine();

                    if (String.IsNullOrEmpty(line))
                        return false;

                    string[] words = line.Split(' ');

                    if (words.Length != 5)
                        return false;

                    folderSuffix = words[3];

                    arguments = arguments ?? new Arguments();

                    if (String.IsNullOrEmpty(arguments.DevelopLocalBranchName))
                    {
                        arguments.DevelopLocalBranchName = words[4];
                        PrintArgumentSetFromClipboard("DevelopLocalBranchName", arguments.DevelopLocalBranchName);
                    }

                    line = sr.ReadLine();

                    if (String.IsNullOrEmpty(line))
                        return false;

                    words = line.Split(' ');

                    if (words.Length != 4)
                        return false;

                    // override
                    arguments.ForkUrl = words[2];
                    PrintArgumentSetFromClipboard("ForkUrl", arguments.ForkUrl);

                    // override
                    arguments.FeatureName = words[3];
                    PrintArgumentSetFromClipboard("FeatureName", arguments.FeatureName);
                }

                if (String.IsNullOrEmpty(arguments.LocalPRRepoFolder))
                {
                    folderSuffix = folderSuffix.Replace('/', '_');

                    arguments.LocalPRRepoFolder = arguments.LocalRepoFolder + "-" + folderSuffix;
                    PrintArgumentSetFromClipboard("LocalPRRepoFolder", arguments.LocalPRRepoFolder);
                }

                // override
                {
                    var uri = new Uri(arguments.ForkUrl);
                    string baseUri = uri.GetLeftPart(System.UriPartial.Authority);

                    Uri forkRelUri = (new Uri(baseUri)).MakeRelativeUri(uri);

                    string[] parts = forkRelUri.ToString().Split('/');

                    arguments.ForkName = parts[0];
                    PrintArgumentSetFromClipboard("ForkName", arguments.ForkName);
                }
                #endregion
            }

            if (arguments == null || arguments.MissingValues())
            {
                #region get missing values from clipboard
                arguments = arguments ?? new Arguments();

                if (string.IsNullOrEmpty(arguments.LocalRepoFolder))
                    arguments.LocalRepoFolder = GetValueFromInput("LocalRepoFolder", Directory.GetCurrentDirectory());

                if (string.IsNullOrEmpty(arguments.DevelopLocalBranchName))
                    arguments.DevelopLocalBranchName = GetValueFromInput("DevelopLocalBranchName", "develop");

                if (string.IsNullOrEmpty(arguments.RemoteName))
                    arguments.RemoteName = GetValueFromInput("RemoteName", "origin");

                if (string.IsNullOrEmpty(arguments.LocalPRRepoFolder))
                    arguments.LocalPRRepoFolder = GetValueFromInput("LocalPRRepoFolder", Path.Combine(arguments.LocalRepoFolder, "-PR"));

                if (string.IsNullOrEmpty(arguments.ForkUrl))
                    arguments.ForkUrl = GetValueFromInput("ForkUrl");

                if (string.IsNullOrEmpty(arguments.ForkName))
                    arguments.ForkName = GetValueFromInput("ForkName");

                if (string.IsNullOrEmpty(arguments.FeatureName))
                    arguments.FeatureName = GetValueFromInput("FeatureName");

                #endregion
            }

            return arguments != null && !arguments.MissingValues();
        }

        private static bool TryGetArgValue(ref int index, string[] args,
            bool flagIsOptional,
            out string flag, out string value)
        {
            flag = "";
            value = null;

            if (index >= args.Length || index < 0)
                return false;

            string argFlag = args[index];

            if (!argFlag.StartsWith("-"))
            {
                if (!flagIsOptional)
                    return false;

                value = argFlag;

                ++index;

                return true;
            }

            ++index;

            if (index >= args.Length)
                return false;

            flag = argFlag;
            value = args[index];

            ++index;

            return true;
        }

        private static void RunBatchFile(string filename)
        {
            System.Diagnostics.ProcessStartInfo psi =
              new System.Diagnostics.ProcessStartInfo(filename);
            psi.RedirectStandardOutput = true;
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            psi.UseShellExecute = false;
            System.Diagnostics.Process listFiles;
            listFiles = System.Diagnostics.Process.Start(psi);
            System.IO.StreamReader myOutput = listFiles.StandardOutput;
            listFiles.WaitForExit(2000);
            if (listFiles.HasExited)
            {
                string output = myOutput.ReadToEnd();
                //this.processResults.Text = output;
            }
        }

        private static void PrintArgumentSetFromCmdLine(string argument, string value)
        {
            Console.WriteLine("'" + argument + "' set from command line: " + value);
        }

        private static void PrintArgumentSetFromClipboard(string argument, string value)
        {
            Console.WriteLine("'" + argument + "' set from clipboard: " + value);
        }

        private static string GetValueFromInput(string valueName, string defaultValue = null)
        {
            Console.WriteLine("Couldn't retrieve value for '" + valueName + "'. Please provide it" + (!String.IsNullOrEmpty(defaultValue) ? " (default: '" + defaultValue + "')" : "") + ":");
            string value = Console.ReadLine();

            if (String.IsNullOrEmpty(value) && !String.IsNullOrEmpty(defaultValue))
                value = defaultValue;

            return value;
        }

        [STAThread] // needed to be able to access to the clipboard
        static void Main2(string[] args)
        {
            //var folder = args[0];
            var folder = @"C:\Users\Gwena\TestMirror";

            Console.WriteLine("here: " + folder);
            
            if (!Clipboard.ContainsText())
            {
                Console.WriteLine("usage");
                return;
            }

            /*
            git fetch origin
            git checkout -b gwenaelhagen-patch-1 origin/gwenaelhagen-patch-1
            git merge master
            */

            var clipboardContent = Clipboard.GetText(TextDataFormat.UnicodeText);
            clipboardContent = clipboardContent.Trim();

            using(var sr = new StringReader(clipboardContent))
            {
                var line = sr.ReadLine();

                if (String.IsNullOrEmpty(line))
                {
                    Console.WriteLine("usage");
                    return;
                }

                line = sr.ReadLine(); // git checkout -b gwenaelhagen-patch-1 origin/gwenaelhagen-patch-1

                if (String.IsNullOrEmpty(line))
                {
                    Console.WriteLine("usage");
                    return;
                }

                var words = line.Split(' ');

                if (words.Length != 5)
                {
                    Console.WriteLine("usage");
                    return;
                }

                var feature = words[3];
                var develop = "master";//words[4];

                // git clone --verbose --progress --reference "C:\Users\Gwena\TestMirror" --single-branch --branch master https://github.com/gwenaelhagen/TestMirror.git PR
                // cd PR
                // git remote set-branches --add origin gwenaelhagen-patch-1
                // git fetch
                // git merge --no-ff origin/gwenaelhagen-patch-1


                var cmd1 = String.Format("git clone --verbose --progress --reference \"{0}\" --single-branch --branch {1} {2} \"{0}-PR\"", folder, develop, "https://github.com/gwenaelhagen/TestMirror.git");
                var cmd2 = String.Format("cd \"{0}-PR\"", folder);
                var cmd3 = String.Format("git remote set-branches --add origin {0}", feature);
                var cmd4 = String.Format("git fetch");
                var cmd5 = String.Format("git merge --no-ff origin/{0}", feature);

                /*var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git.exe",
                        Arguments = cmd1,
                        UseShellExecute = false,
                        //RedirectStandardOutput = true,
                        //CreateNoWindow = true
                    }
                };
                proc.Start();*/
                /*while (!proc.StandardOutput.EndOfStream)
                {
                    line = proc.StandardOutput.ReadLine();
                    // do something with line
                    Console.WriteLine(line);
                }*/

                using (var process = new Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.WorkingDirectory = folder;
                    process.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");

                    // Redirects the standard input so that commands can be sent to the shell.
                    process.StartInfo.RedirectStandardInput = true;

                    process.EnableRaisingEvents = true;

                    process.OutputDataReceived += ProcessOutputDataHandler;
                    process.ErrorDataReceived += ProcessErrorDataHandler;
                    process.Exited += process_Exited;

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Send a directory command and an exit command to the shell
                    process.StandardInput.WriteLine(cmd1);
                    //process.StandardInput.WriteLine(cmd2);
                    //process.StandardInput.WriteLine(cmd3);
                    //process.StandardInput.WriteLine(cmd4);
                    process.StandardInput.WriteLine(cmd5);

                    //WindowsInput.InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.CANCEL);

                    //process.WaitForExit();
                    
                }
            }
        }

        static void process_Exited(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public static void ProcessOutputDataHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Console.WriteLine(outLine.Data);
        }

        public static void ProcessErrorDataHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Console.WriteLine(outLine.Data);
        }

        // copy from github
        // drag local repo folder
        // add upstream
        // add feature
        // git config --global alias.oldest-ancestor '!zsh -c '\''diff --old-line-format='' --new-line-format='' <(git rev-list --first-parent "${1:-master}") <(git rev-list --first-parent "${2:-HEAD}") | head -1'\'' -'
    }
}
