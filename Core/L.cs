using System;
using System.Collections.Generic;

namespace TweekPro.Core {
 /// <summary>
 /// Minimal two-language string table. Vietnamese source strings are the keys; English comes from LangEn.
 /// Unknown keys fall back to the Vietnamese text, so a missing translation never produces an empty control.
 /// </summary>
 public static class L {
  public const string Vietnamese="vi", EnglishCode="en";
  static string lang=Vietnamese;

  /// <summary>Active language code ("vi" or "en"); anything else is treated as Vietnamese.</summary>
  public static string Lang { get { return lang; } set { lang=Normalize(value); } }
  public static bool English { get { return lang==EnglishCode; } }

  public static string Normalize(string code){return String.Equals(code,EnglishCode,StringComparison.OrdinalIgnoreCase)?EnglishCode:Vietnamese;}

  /// <summary>Translates a Vietnamese UI string for the active language.</summary>
  public static string T(string vi){
   if(vi==null||!English)return vi;
   string en;return LangEn.Table.TryGetValue(vi,out en)?en:vi;
  }

  /// <summary>Translates a Vietnamese format string and applies the arguments.</summary>
  public static string F(string vi,params object[] args){return String.Format(T(vi),args);}

  /// <summary>True when the active language has an explicit translation for the string.</summary>
  public static bool Has(string vi){return vi!=null&&LangEn.Table.ContainsKey(vi);}

  /// <summary>Number of English entries; used by the self-test to guard against an emptied table.</summary>
  public static int Count { get { return LangEn.Table.Count; } }
 }
}
