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

namespace ConvertionTasks {
    public class PatchInternalsVisibleTo : Microsoft.Build.Utilities.Task {

        [Required]
        public ITaskItem[] FileNames { get; set; }

        [Required]
        public string SNExeFileName { get; set; }

        [Required]
        public string KeyFileName { get; set; }

        private bool CheckFileExists(string fileName) {
            if (!File.Exists(fileName)) {
                Log.LogError("The file {0} not found", fileName);
                return false;
            }
            else
                return true;
        }
        public override bool Execute() {

            if (!CheckFileExists(SNExeFileName) | !CheckFileExists(KeyFileName))
                return false;

            foreach (ITaskItem taskItem in FileNames) {
                PerformPatch(taskItem.ItemSpec,
                    Path.GetFullPath(KeyFileName));
            }
            return true;    
        }

        private void PerformPatch(string fileName, string keyFileName) {
            string publicKeyToken = CreatePublicKeyToken(keyFileName);
            PatchFile(fileName, publicKeyToken);
        }

        static void PatchFile(string fileName, string publicKeyToken) {
            string content = String.Empty;
            using (StreamReader reader = new StreamReader(fileName)) {
                content = reader.ReadToEnd();
                reader.Close();
            }

            Regex regex = new Regex(@""",\s*PublicKey=[0123456789abcdefABCDEF]*""");
            content = regex.Replace(content, String.Format("\", PublicKey={0}\"", publicKeyToken));

            string tmpFileName = Path.GetRandomFileName();
            using (StreamWriter writer = new StreamWriter(tmpFileName)) {
                writer.Write(content);
                writer.Close();
            }
            File.Delete(fileName);
            File.Move(tmpFileName, fileName);
        }

        private string CreatePublicKeyToken(string keyFileName) {
            string publicKeyName = CreatePublicKey(keyFileName);
            string publicKeyToken = ObtainPublicKeyToken(publicKeyName);
            File.Delete(publicKeyName);
            return publicKeyToken;
        }


        private static string GetPublicKeyFileName() {
            return Path.Combine(Path.GetTempPath(), "dx_public_key.tmp");
        }
        private string CreatePublicKey(string keyFileName) {
            string fileName = GetPublicKeyFileName();
            ExecuteSn("-p \"{0}\" \"{1}\"", keyFileName, fileName);
            return fileName;
        }

        private void ExecuteSn(string argumentsFormat, params object[] arguments) {

            
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = SNExeFileName;
            psi.Arguments = String.Format(CultureInfo.InvariantCulture, argumentsFormat, arguments);
            psi.UseShellExecute = false;
            Log.LogMessage("Executing {0} {1}", SNExeFileName, psi.Arguments); 
            using (Process proc = Process.Start(psi)) {
                proc.WaitForExit();
            }

        }


        private string ObtainPublicKeyToken(string publicKeyName) {
            string fileName = GetPublicKeyFileName();
            ExecuteSn("-o \"{0}\" \"{1}\"", publicKeyName, fileName);

            string csvContent = File.ReadAllText(fileName);
            File.Delete(fileName);

            string[] bytes = csvContent.Split(',');
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++) {
                int byteValue = Int32.Parse(bytes[i]);
                result.AppendFormat("{0:x2}", byteValue);
            }
            return result.ToString();
        }

    }
}
