using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace ConvertionTasks
{
    public class PatchInternalsVisibleTo : Microsoft.Build.Utilities.Task
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
            
            CheckFileExists(SNExeFileName);
            CheckFileExists(KeyFileName);
            foreach (ITaskItem taskItem in FileNames)
            {
                PerformPatch(taskItem.ItemSpec, Path.GetFullPath(SNExeFileName),
                    Path.GetFullPath(KeyFileName));
            }
            return true;
        }

        static void PerformPatch(string fileName, string snExe, string keyFileName)
        {
            string publicKeyToken = CreatePublicKeyToken(snExe, keyFileName);
            PatchFile(fileName, publicKeyToken);
        }

        static void PatchFile(string fileName, string publicKeyToken)
        {
            string content = String.Empty;
            using (StreamReader reader = new StreamReader(fileName))
            {
                content = reader.ReadToEnd();
                reader.Close();
            }

            Regex regex = new Regex(@""",\s*PublicKey=[0123456789abcdefABCDEF]*""");
            content = regex.Replace(content, String.Format("\", PublicKey={0}\"", publicKeyToken));

            string tmpFileName = Path.GetRandomFileName();
            using (StreamWriter writer = new StreamWriter(tmpFileName))
            {
                writer.Write(content);
                writer.Close();
            }
            File.Delete(fileName);
            File.Move(tmpFileName, fileName);
        }

        static string CreatePublicKeyToken(string snExe, string keyFileName)
        {
            string publicKeyName = CreatePublicKey(snExe, keyFileName);
            string publicKeyToken = ObtainPublicKeyToken(snExe, publicKeyName);
            File.Delete(publicKeyName);
            return publicKeyToken;
        }


        private static string GetPublicKeyFileName()
        {
            return Path.Combine(Path.GetTempPath(), "dx_public_key.tmp");
        }
        static string CreatePublicKey(string snExe, string keyFileName)
        {

            string fileName = GetPublicKeyFileName();
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = snExe;
            psi.Arguments = String.Format("-p {0} {1}", keyFileName, fileName);
            psi.UseShellExecute = false;
            using (Process proc = Process.Start(psi))
            {
                proc.WaitForExit();
            }
            return fileName;
        }

        private static string ObtainPublicKeyToken(string snExe, string publicKeyName)
        {
            string fileName = GetPublicKeyFileName();
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = snExe;
            psi.Arguments = String.Format("-o {0} {1}", publicKeyName, fileName);
            psi.UseShellExecute = false;

            using (Process proc = Process.Start(psi))
            {
                proc.WaitForExit();
            }

            string csvContent = String.Empty;
            using (StreamReader sr = new StreamReader(fileName))
            {
                csvContent = sr.ReadToEnd();
                sr.Close();
            }
            File.Delete(fileName);

            string[] bytes = csvContent.Split(',');
            StringBuilder result = new StringBuilder();
            int count = bytes.Length;
            for (int i = 0; i < count; i++)
            {
                int byteValue = Int32.Parse(bytes[i]);
                result.AppendFormat("{0:x2}", byteValue);
            }
            return result.ToString();
        }

    }
}
