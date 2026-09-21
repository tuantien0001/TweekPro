using System;
using System.Security.Cryptography;
using System.Text;

namespace TweekPro.AI {
 /// <summary>
 /// Stores the API key in settings.json encrypted with Windows DPAPI (current user), so it is unreadable from another
 /// account or machine. Off Windows (Linux self-test) the value is only base64-obfuscated and clearly marked as such.
 /// </summary>
 public static class SecretStore {
  const string DpapiPrefix="dpapi:", PlainPrefix="plain:";
  static readonly byte[] Entropy=Encoding.UTF8.GetBytes("TweekPro.AI.ApiKey.v1");
  static readonly bool IsWindows=Environment.OSVersion.Platform==PlatformID.Win32NT;

  /// <summary>Encrypts a secret for persistence; empty input yields an empty string.</summary>
  public static string Protect(string secret){
   if(String.IsNullOrEmpty(secret))return "";
   var bytes=Encoding.UTF8.GetBytes(secret);
   if(IsWindows){
    try{return DpapiPrefix+Convert.ToBase64String(ProtectedData.Protect(bytes,Entropy,DataProtectionScope.CurrentUser));}
    catch(CryptographicException){}
   }
   return PlainPrefix+Convert.ToBase64String(bytes);
  }

  /// <summary>Decrypts a value written by Protect; returns an empty string when the value is missing or unreadable.</summary>
  public static string Unprotect(string stored){
   if(String.IsNullOrEmpty(stored))return "";
   try{
    if(stored.StartsWith(DpapiPrefix,StringComparison.Ordinal)){
     if(!IsWindows)return "";
     return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(DpapiPrefix.Length)),Entropy,DataProtectionScope.CurrentUser));
    }
    if(stored.StartsWith(PlainPrefix,StringComparison.Ordinal))return Encoding.UTF8.GetString(Convert.FromBase64String(stored.Substring(PlainPrefix.Length)));
   }catch(Exception){}
   return "";
  }

  /// <summary>True when the stored value is protected by DPAPI rather than merely encoded.</summary>
  public static bool IsEncrypted(string stored){return !String.IsNullOrEmpty(stored)&&stored.StartsWith(DpapiPrefix,StringComparison.Ordinal);}

  /// <summary>Masks a key for display: first 6 and last 4 characters.</summary>
  public static string Mask(string key){
   if(String.IsNullOrEmpty(key))return "";
   if(key.Length<=12)return new string('•',key.Length);
   return key.Substring(0,6)+new string('•',Math.Min(12,key.Length-10))+key.Substring(key.Length-4);
  }
 }
}
