﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace ERPFramework.ModulesHelper
{
    [Flags]
    public enum SerialType : ushort
    {
        ONLY_NAME = 0x0000,
        MAC_ADDRESS = 0x0001,
        LICENSE_NAME = 0x0002,
        EXPIRATION_DATE = 0x0004,
        PEN_DRIVE = 0x0008,
        TRIAL = 0x0010
    }

    public enum ActivationState
    {
        Activate,
        NotActivate,
        Trial
    }

    [Serializable()]
    public class SerialModule
    {
        public bool Enable { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public SerialType SerialType;

        public DateTime Expiration { get; set; }

        public string SerialNo { get; set; }

        public List<string> Functionality { get; set; } = new List<string>();
    }

    [Serializable()]
    public class ActivationData
    {
        public string License { get; set; }

        public string PenDrive { get; set; }

        public List<SerialModule> Modules = new List<SerialModule>();
    }

    public static class ActivationManager
    {
        public static ActivationData activationData = new ActivationData();
        private static string macAddres = ReadMacAddress();

        public static void Clear()
        {
            activationData.Modules.Clear();
        }

        public static void LoadActivations()
        {
            var applPath = Path.GetDirectoryName(Application.ExecutablePath);
            var dirs = Directory.GetDirectories(applPath);

            foreach (var dir in dirs)
            {
                var menuDir = Path.Combine(dir, "Menu");
                if (Directory.Exists(menuDir))
                {
                    LoadActivatioModule(menuDir);
                    continue;
                }
            }
        }

        private static void LoadActivatioModule(string menuDir)
        {
#if DEBUG
            var activationName = "activation.xml";
#else
            var activationName = "activation.cml";
#endif
            var activationFile = new XmlDocument();
            activationFile.Load(Path.Combine(menuDir, activationName));
            var module = activationFile.SelectSingleNode("module");

            var serial = new SerialModule
            {
                Name = module.Attributes["name"].Value,
                Code = module.Attributes["code"].Value,
                SerialType = (SerialType)Enum.Parse(typeof(SerialType), module.Attributes["serialType"].Value),
                Enable = false,
                SerialNo = ""
            };
            if (serial.SerialType.HasFlag(SerialType.EXPIRATION_DATE))
            {
                if (DateTime.TryParse(module.Attributes["expirationDate"].Value, out DateTime expirationDate))
                    serial.Expiration = expirationDate;
            }
            var functionality = activationFile.SelectNodes("module/functionality");
            foreach (XmlNode node in functionality)
                serial.Functionality.Add(node.InnerText);
        }

        public static void AddModule(bool enable, string module, SerialType sType, DateTime expiration, string serial)
        {
            var sd = new SerialModule
            {
                SerialType = sType,
                SerialNo = serial
            };

            activationData.Modules.Add(sd);
        }

        public static ActivationState IsActivate(string name)
        {
            SerialModule sm = activationData.Modules.Find(p => (p.Name == name));
            if (sm == null || sm.Enable)
                return ActivationState.NotActivate;

            if (!SerialFormatIsOk(sm.SerialNo, sm.Name))
                return ActivationState.NotActivate;

            if (!CheckSerialType(sm))
                return ActivationState.NotActivate;

            return sm.SerialType.HasFlag(SerialType.TRIAL)
                ? ActivationState.Trial
                : ActivationState.Activate;
        }

        private static string ConvertTo64(string text)
        {
            var textchar = text.ToCharArray();
            var textbyte = new byte[textchar.Length];

            for (int t = 0; t < textchar.Length; t++)
                textbyte[t] = (byte)textchar[t];
            return Convert.ToBase64String(textbyte);
        }

        public static string CreateSerial(string license, string macAddress, string application, string module, SerialType sType, DateTime expiration, string pendrive)
        {
            string serial = string.Empty;

            concat(ref serial, ConvertString(application) + ConvertString(module));
            if (sType.HasFlag(SerialType.LICENSE_NAME))
                concat(ref serial, ConvertString(license));
            if (sType.HasFlag(SerialType.MAC_ADDRESS))
                concat(ref serial, ConvertMacAddress(macAddress));

            if (sType.HasFlag(SerialType.EXPIRATION_DATE))
                concat(ref serial, ConvertToString((UInt64)(expiration.Year * 365 + expiration.Month * 31 + expiration.Day)));

            if (sType.HasFlag(SerialType.PEN_DRIVE))
            {
                var letter = USBSerialNumber.GetDriveLetterFromName(pendrive);
                concat(ref serial, ConvertSerialNumber(USBSerialNumber.getSerialNumberFromDriveLetter(letter)));
            }

            if (sType.HasFlag(SerialType.TRIAL))
                concat(ref serial, ConvertString("TRIAL VERSION"));

            // Checksum
            concat(ref serial, ConvertString(serial));
            return serial;
        }

        private static bool SerialFormatIsOk(string serial, string module)
        {
            var serialCheck = string.Empty;
            var parts = serial.Split(new char[] { '-' });
            for (int t = 0; t < parts.Length - 1; t++)
                concat(ref serialCheck, parts[t]);

            if (ConvertString(serialCheck) != parts[parts.Length - 1])
                return false;

            if (ConvertString(module) != parts[0])
                return false;

            return true;
        }

        private static bool CheckSerialType(SerialModule sm)
        {
            var pos = 1;
            var parts = sm.SerialNo.Split(new char[] { '-' });

            if (sm.SerialType.HasFlag(SerialType.LICENSE_NAME))
                if (parts[pos++] != ConvertString(activationData.License))
                    return false;

            if (sm.SerialType.HasFlag(SerialType.MAC_ADDRESS))
                if (parts[pos++] != ConvertMacAddress(macAddres))
                    return false;

            if (sm.SerialType.HasFlag(SerialType.EXPIRATION_DATE))
                if (ConvertFromString(parts[pos++]) < (UInt64)(GlobalInfo.CurrentDate.Year * 365 + GlobalInfo.CurrentDate.Month * 31 + GlobalInfo.CurrentDate.Day))
                    return false;

            if (sm.SerialType.HasFlag(SerialType.PEN_DRIVE))
            {
                var letter = USBSerialNumber.GetDriveLetterFromName(activationData.PenDrive);
                if (letter == string.Empty || parts[pos++] != ConvertSerialNumber(USBSerialNumber.getSerialNumberFromDriveLetter(letter)))
                    return false;
            }

            return true;
        }

        private static void concat(ref string a, string b)
        {
            a = (a == string.Empty)
                ? b
                : string.Concat(a, "-", b);
        }

        public static string ReadMacAddress()
        {
            using (var searcher = new ManagementObjectSearcher
            ("Select MACAddress,PNPDeviceID FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL AND PNPDeviceID IS NOT NULL"))
            {
                var mObject = searcher.Get();

                foreach (ManagementObject obj in mObject)
                {
                    var pnp = obj["PNPDeviceID"].ToString();
                    if (pnp.Contains("PCI\\"))
                        return obj["MACAddress"].ToString(); ;
                }
                return "";
            }
        }

        private static string ConvertMacAddress(string mac)
        {
            UInt64 value = 0;

            var exa = mac.Split(new char[] { ':' });

            for (int t = 0; t < exa.Length; t++)
                value = value * (UInt64)4 + ConvertFromEx(exa[t]);

            return ConvertToString(value);
        }

        private static UInt64 ConvertFromEx(string exa)
        {
            var exad = "0123456789ABCDEF";
            var ex = exa.ToCharArray();
            UInt64 value = 0;

            for (int t = 0; t < ex.Length; t++)
                value = value * 16 + (UInt64)exad.IndexOf(ex[t]);

            return value;
        }

        private static string ConvertSerialNumber(string exa)
        {
            return ConvertToString(ConvertFromEx(exa));
        }

        private static string ConvertString(string text)
        {
            UInt64 value = 0;
            var testo = text.Normalize().ToCharArray();
            for (int c = 0; c < testo.Length; c++)
                value = value * 2 + text[c];

            return ConvertToString(value);
        }

        private static string ConvertToString(UInt64 value)
        {
            var convert = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var converted = "";

            var bbase = (UInt64)convert.Length;

            while (value > bbase)
            {
                var rest = value % bbase;
                value = value / bbase;
                converted = convert.Substring((int)rest, 1) + converted;
            }
            converted = convert.Substring((int)value, 1) + converted;

            return converted;
        }

        private static UInt64 ConvertFromString(string exa)
        {
            var exad = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var ex = exa.ToCharArray();
            UInt64 value = 0;

            for (int t = 0; t < ex.Length; t++)
                value = value * (UInt64)exad.Length + (UInt64)exad.IndexOf(ex[t]);

            return value;
        }

        private static string filekey()
        {
            var directory = GlobalInfo.DBaseInfo.dbManager.GetApplicationName();
            if (string.IsNullOrEmpty(directory))
            {
                if (Directory.GetParent(Directory.GetCurrentDirectory()).FullName.EndsWith("bin", StringComparison.CurrentCultureIgnoreCase))
                    directory = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.Parent.Name;
                else
                    directory = new DirectoryInfo(Environment.CurrentDirectory).Name;
            }
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), directory);
            return Path.Combine(path, "key.bin");
        }

        public static bool Load()
        {
            LoadActivations();

            if (File.Exists(filekey()))
            {
                var memStr = new MemoryStream();
                using (FileStream myFS = new FileStream(filekey(), FileMode.Open))
                using (GZipStream gZip = new GZipStream(myFS, CompressionMode.Decompress))
                    gZip.CopyTo(memStr);
                memStr.Position = 0;
                var myBF = new BinaryFormatter();
                activationData = (ActivationData)myBF.Deserialize(memStr);
            }
            else
                return false;

            activationData.License = ConvertFrom64(activationData.License);
            activationData.PenDrive = ConvertFrom64(activationData.PenDrive);

            foreach (SerialModule Module in activationData.Modules)
            {
                Module.Name = ConvertFrom64(Module.Name);
                //Module.Expiration = ConvertFrom64(Module.Expiration);
                Module.SerialNo = ConvertFrom64(Module.SerialNo);
                Module.SerialType = (SerialType)Enum.Parse(typeof(SerialType), ConvertFrom64(Module.SerialType.ToString()));
            }
            return true;
        }

        public static void Save()
        {
            var directory = Path.GetDirectoryName(filekey());
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var memStr = new MemoryStream();
            var myBF = new BinaryFormatter();
            activationData.License = ConvertTo64(activationData.License);
            activationData.PenDrive = ConvertTo64(activationData.PenDrive);

            foreach (SerialModule Module in activationData.Modules)
            {
                Module.Name = ConvertTo64(Module.Name);
                //Module.Expiration = ConvertTo64(Module.Expiration);
                Module.SerialNo = ConvertTo64(Module.SerialNo);
            }
            myBF.Serialize(memStr, activationData);

            using (FileStream myFS = new FileStream(filekey(), FileMode.Create))
            using (GZipStream gzip = new GZipStream(myFS, CompressionMode.Compress, false))
                gzip.Write(memStr.ToArray(), 0, (int)memStr.Length);
        }

        private static string ConvertFrom64(string text)
        {
            if (text.Length == 0)
                return "";

            var textchar = text.ToCharArray();
            var serialByte = Convert.FromBase64CharArray(textchar, 0, textchar.Length);

            var ascii = new ASCIIEncoding();
            return ascii.GetString(serialByte);
        }
    }
}