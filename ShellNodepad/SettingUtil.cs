using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellNodepad.util
{
    public class SettingUtil
    {
        private static string KEY = @"Software\quick_nodepad";
        private static Dictionary<string, string?> CacheValue = new Dictionary<string, string?>();
        public static bool SetSetting(string key, string value)
        {
            CacheValue[key] = value;
            Microsoft.Win32.RegistryKey rk2 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KEY);
            try
            {
                rk2.SetValue(key, value);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                rk2.Close();
            }
        }

        public static string GetSetting(string key, string def = "")
        {
            if (CacheValue.ContainsKey(key))
            {
                var v = CacheValue[key];
                if (null == v)
                {
                    return def;
                }
                return v;
            }
            Microsoft.Win32.RegistryKey rk2 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KEY);
            try
            {
                string? val = null;
                var v = rk2.GetValue(key);
                if (v != null)
                {
                    val = v.ToString();
                }
                if (string.IsNullOrEmpty(val))
                {
                    CacheValue[key] = def;
                    return def;
                }
                CacheValue[key] = val;
                return val;
            }
            catch
            {
                CacheValue[key] = def;
                return def;
            }
            finally
            {
                rk2.Close();
            }
        }

        public static string? GetSettingOrDefValueIfNotExists(string key, string def = "")
        {
            if (CacheValue.ContainsKey(key))
            {
                return CacheValue[key];
            }
            Microsoft.Win32.RegistryKey rk2 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KEY);
            try
            {
                var v = rk2.GetValueNames().Contains(key) ? rk2.GetValue(key) : null;
                if (v == null)
                {
                    CacheValue[key] = def;
                    return def;
                }
                else
                {
                    string? val = v.ToString();
                    CacheValue[key] = val;
                    return val;
                }
            }
            catch
            {
                CacheValue[key] = def;
                return def;
            }
            finally
            {
                rk2.Close();
            }
        }
    }
}
