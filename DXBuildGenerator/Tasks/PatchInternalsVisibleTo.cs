using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using System.Globalization;
using Microsoft.Build.Utilities;

namespace ConvertionTasks
{
    public class PatchInternalsVisibleTo : Task
    {

        [Required]
        public ITaskItem[] FileNames { get; set; }

        [Required]
        public string SNExeFileName { get; set; }

        [Required]
        public string KeyFileName { get; set; }

        private bool CheckFileExists(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Log.LogError("The file {0} not found", fileName);
                return false;
            }
            else
                return true;
        }
        public override bool Execute()
        {

            if (!CheckFileExists(SNExeFileName) | !CheckFileExists(KeyFileName))
                return false;

            foreach (ITaskItem taskItem in FileNames)
            {
                PerformPatch(taskItem.ItemSpec,
                    Path.GetFullPath(KeyFileName));
            }
            return true;
        }

        private void PerformPatch(string fileName, string keyFileName)
        {
            string publicKeyToken;
            string publicKey = RetrievePublicKeyAndToken(keyFileName, out publicKeyToken);
            PatchFile(fileName, publicKey, publicKeyToken);
        }

        private static void PatchFile(string fileName, string publicKey, string publicKeyToken)
        {
            string content;
            string originalContent;
            using (StreamReader reader = new StreamReader(fileName))
            {
                originalContent = content = reader.ReadToEnd();
                reader.Close();
            }

            Regex regex = new Regex(@""",\s*PublicKey=[0123456789abcdefABCDEF]*""");
            content = regex.Replace(content, String.Format("\", PublicKey={0}\"", publicKey));

            regex = new Regex(@"(?<=public const string PublicKeyToken = \"")[0123456789abcdefABCDEF]{16}(?=\"")");
            content = regex.Replace(content, publicKeyToken);

            if (content != originalContent)
            {
                string tmpFileName = Path.GetRandomFileName();
                File.WriteAllText(tmpFileName, content);
                File.Delete(fileName);
                File.Move(tmpFileName, fileName);
            }
        }

        private string RetrievePublicKeyAndToken(string keyFileName, out string token)
        {
            string publicKeyFileName = ExtractPublicKey(keyFileName);
            string publicKey = GetFullPublicKey(publicKeyFileName);
            string snOutput = ExecuteSn("-q -t {0}", publicKeyFileName).Trim();
            if (string.IsNullOrWhiteSpace(snOutput))
                throw new InvalidOperationException("The output of sn.exe is empty.");

            token = snOutput.Substring(snOutput.Length - 16);
            File.Delete(publicKeyFileName);
            return publicKey;
        }


        private static string GetPublicKeyFileName()
        {
            return Path.Combine(Path.GetTempPath(), "dx_public_key.tmp");
        }
        private string ExtractPublicKey(string keyFileName)
        {
            string fileName = GetPublicKeyFileName();
            ExecuteSn("-p \"{0}\" \"{1}\"", keyFileName, fileName);
            return fileName;
        }

        private string ExecuteSn(string argumentsFormat, params object[] arguments)
        {


            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = SNExeFileName;
            psi.Arguments = String.Format(CultureInfo.InvariantCulture, argumentsFormat, arguments);
            psi.UseShellExecute = false;
            Log.LogMessage("Executing {0} {1}", SNExeFileName, psi.Arguments);
            psi.RedirectStandardOutput = true;
            using (Process proc = Process.Start(psi))
            {
                proc.WaitForExit();
                return proc.StandardOutput.ReadToEnd();
            }

        }


        private string GetFullPublicKey(string publicKeyName)
        {
            string csvFileName = Path.Combine(Path.GetTempPath(), "dx_public_key.csv");
            ExecuteSn("-o \"{0}\" \"{1}\"", publicKeyName, csvFileName);

            string csvContent = File.ReadAllText(csvFileName);
            File.Delete(csvFileName);

            string[] bytes = csvContent.Split(',');
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                int byteValue = Int32.Parse(bytes[i]);
                result.AppendFormat("{0:x2}", byteValue);
            }
            return result.ToString();
        }

    }
}
