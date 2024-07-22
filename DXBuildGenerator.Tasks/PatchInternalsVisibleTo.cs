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
using System.Reflection;
using System.Security.Cryptography;

namespace ConvertionTasks
{
    public class PatchInternalsVisibleTo : Task
    {

        [Required]
        public ITaskItem[] FileNames { get; set; }


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

            if (!CheckFileExists(KeyFileName))
                return false;

            foreach (ITaskItem taskItem in FileNames)
            {
                PerformPatch(taskItem.ItemSpec,
                    Path.GetFullPath(KeyFileName));
            }
            return true;
        }

        public static byte[] GetPublicKey(byte[] snk) => new StrongNameKeyPair(snk).PublicKey;

        private static string ToHex(byte[] snk)
        {
            return string.Join(string.Empty, snk.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
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

            var regex = new Regex(@"(?<=public const string PublicKeyToken = \"")[0123456789abcdefABCDEF]{16}(?=\"")");
            content = regex.Replace(content, publicKeyToken);

            regex = new Regex(@"(?<=public const string PublicKey = \"")[0123456789abcdefABCDEF]*(?=\"")");
            content = regex.Replace(content, publicKey);

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
            var publicKey = GetPublicKey(File.ReadAllBytes(keyFileName));
            token = ToHex(GetPublicKeyToken(publicKey));
            return ToHex(publicKey);
        }


        public static byte[] GetPublicKeyToken(byte[] publicKey)
        {
            using (var csp = new SHA1CryptoServiceProvider())
            {
                byte[] hash = csp.ComputeHash(publicKey);
                byte[] token = new byte[8];

                for (int i = 0; i < 8; i++)
                {
                    token[i] = hash[hash.Length - i - 1];
                }

                return token;
            }
        }
    }
}
