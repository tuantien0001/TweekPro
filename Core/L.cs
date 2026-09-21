using System;
using System.Collections.Generic;

namespace TweekPro.Core {
 /// <summary>
 /// Minimal two-language string table. Vietnamese source strings are the keys; English comes from LangEn plus
 /// feature-local tables (e.g. Pup.PupLang) merged at startup. A key present in two tables throws so translations never diverge.
 /// Unknown keys fall back to the Vietnamese text, so a missing translation never produces an empty control.
 /// </summary>
 public static class L {
  public const string Vietnamese="vi", EnglishCode="en";
  static string lang=Vietnamese;
  static readonly Dictionary<string,string> table=Merge(LangEn.Table,Pup.PupLang.Table,Stale.StaleLang.Table,Update.UpdateLang.Table,Startup.StartupLang.Table,Explorer.ExplorerLang.Table);

  /// <summary>Active language code ("vi" or "en"); anything else is treated as Vietnamese.</summary>
  public static string Lang { get { return lang; } set { lang=Normalize(value); } }
  public static bool English { get { return lang==EnglishCode; } }

  public static string Normalize(string code){return String.Equals(code,EnglishCode,StringComparison.OrdinalIgnoreCase)?EnglishCode:Vietnamese;}

  /// <summary>Translates a Vietnamese UI string for the active language.</summary>
  public static string T(string vi){
   if(vi==null||!English)return vi;
   string en;return table.TryGetValue(vi,out en)?en:vi;
  }

  /// <summary>Translates a Vietnamese format string and applies the arguments.</summary>
  public static string F(string vi,params object[] args){return String.Format(T(vi),args);}

  /// <summary>True when the active language has an explicit translation for the string.</summary>
  public static bool Has(string vi){return vi!=null&&table.ContainsKey(vi);}

  /// <summary>Number of English entries; used by the self-test to guard against an emptied table.</summary>
  public static int Count { get { return table.Count; } }

  /// <summary>Every merged pair, for tests that validate placeholders and emptiness across all feature tables.</summary>
  public static IEnumerable<KeyValuePair<string,string>> Pairs { get { return table; } }

  static Dictionary<string,string> Merge(params Dictionary<string,string>[] tables){
   var merged=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var t in tables)foreach(var pair in t){
    if(merged.ContainsKey(pair.Key))throw new InvalidOperationException("Khóa dịch trùng giữa các bảng: "+pair.Key);
    merged.Add(pair.Key,pair.Value);
   }
   return merged;
  }
 }
}
