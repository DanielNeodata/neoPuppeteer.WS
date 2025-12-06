using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace neoPuppeteerWS
{
    public class neoTools
    {
        private neoMime _MIME = new neoMime();
        internal static readonly string hashSalt = "20f958501eefbd6780a65e9ae9cde0dc";
        internal static readonly char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
        public string HashSHA1(string _input)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(hashSalt + _input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) { sb.Append(b.ToString("x2")); }
                return sb.ToString();
            }
        }
        public string GetUniqueKey(int size)
        {
            byte[] data = new byte[4 * size];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(size);
            for (int i = 0; i < size; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;
                result.Append(chars[idx]);
            }
            return result.ToString();
        }
        public bool EvaluatePath(string _directoryPath)
        {
            try
            {
                string folder = Path.GetDirectoryName(_directoryPath);
                if (!Directory.Exists(folder)) { DirectoryInfo di = Directory.CreateDirectory(folder); }
            }
            catch (IOException ioex)
            {
                return false;
            }
            return true;
        }
        public string GetDataUrlFromFile(string _file) {
            string _mime = _MIME.GetMimeType(_file);
            string _base64 = "";
            if (File.Exists(_file)) { _base64 = ("data:" + _mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(_file))); }
            return _base64;
        }
        public bool WriteStringToFile(string _content, string _filePath)
        {
            if (!File.Exists(_filePath)) { File.WriteAllText(_filePath, _content); }
            return true;
        }
        public bool IsUrlValid(string webUrl)
        {
            if (webUrl == null) return false;
            return Uri.IsWellFormedUriString(webUrl, UriKind.RelativeOrAbsolute);
        }
    }
}
