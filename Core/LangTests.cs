using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TweekPro.Health;

namespace TweekPro.Core {
 /// <summary>Self-tests for the two-language string table; platform-neutral, runs under Mono.</summary>
 public static class LangTests {
  static void Assert(bool ok,string what){if(!ok)throw new Exception("Lang test failed: "+what);}

  public static void Run(){
   string before=L.Lang;
   try{
    L.Lang="vi";
    Assert(L.T("Ứng dụng")=="Ứng dụng"&&L.T("không có trong bảng")=="không có trong bảng"&&L.T(null)==null,"Vietnamese passes strings through");
    Assert(!L.English&&L.Normalize("EN")=="en"&&L.Normalize("fr")=="vi"&&L.Normalize(null)=="vi","language code normalization");

    L.Lang="en";
    Assert(L.English&&L.T("Ứng dụng")=="Applications"&&L.T("Tổng quan")=="Overview"&&L.T("Dọn rác")=="Junk Cleaner","tab titles translated");
    Assert(L.T("chuỗi chưa dịch")=="chuỗi chưa dịch","unknown key falls back to Vietnamese");
    Assert(L.F("{0} tệp, {1}",12,"3 MB")=="12 files, 3 MB","format translation");
    Assert(L.Count>=200,"table is populated ("+L.Count+")");

    var placeholder=new Regex(@"\{\d+\}");
    foreach(var pair in LangEn.Table){
     Assert(!String.IsNullOrWhiteSpace(pair.Key)&&!String.IsNullOrWhiteSpace(pair.Value),"no empty entries");
     var a=placeholder.Matches(pair.Key).Cast<Match>().Select(m=>m.Value).OrderBy(v=>v).ToArray();
     var b=placeholder.Matches(pair.Value).Cast<Match>().Select(m=>m.Value).OrderBy(v=>v).ToArray();
     Assert(a.SequenceEqual(b),"placeholders preserved in: "+pair.Key);
    }

    // Every tab title and every embedded junk rule group/name has an English translation so the UI never mixes languages.
    foreach(string tab in new[]{"Tổng quan","Ứng dụng","Ứng dụng Windows","Phần còn sót","Dọn rác","Thư mục rỗng","Tệp trùng lặp","Tệp tải về cũ","Dấu vết","Phân tích ổ đĩa","Kho khôi phục","Khởi động","Explorer","Mạng","Công cụ","Nhật ký"})
     Assert(L.Has(tab)&&Branding.GlyphKey(L.T(tab))==Branding.GlyphKey(tab)&&Branding.GlyphKey(tab)!="dot","tab translated with matching glyph: "+tab);
    var rules=Cleaner.JunkRules.LoadEmbedded();
    foreach(var r in rules.Rules)Assert(L.Has(r.Group)&&L.Has(r.Name)&&L.Has(r.Description),"junk rule translated: "+r.Id);
    foreach(string tab in new[]{HealthCheck.TabJunk,HealthCheck.TabLeftovers,HealthCheck.TabEmpty,HealthCheck.TabVault,HealthCheck.TabAutorun,HealthCheck.TabAnalyzer})Assert(L.Has(tab),"health tab hint translated: "+tab);

    // The health engine speaks the active language.
    var report=HealthCheck.Evaluate(new HealthInputs{JunkBytes=3L*HealthCheck.GB,JunkFiles=1000,LeftoverCandidates=2,EmptyFolders=0,VaultBackups=0,AutorunEntries=3,DiskName="Drive C:",DiskFreeBytes=50L*HealthCheck.GB,DiskTotalBytes=100L*HealthCheck.GB});
    Assert(report.Findings[0].Area=="Junk files"&&report.Findings[0].Verdict=="Excessive junk"&&report.Findings[0].Detail.EndsWith(" files, 3.00 GB")&&!report.Findings[0].Detail.Contains("tệp"),"junk finding in English: "+report.Findings[0].Detail);
    Assert(report.Findings[1].Detail=="2 items from earlier uninstalls are still unhandled.","leftover finding in English");
    Assert(report.Findings[3].Verdict=="Vault is empty"&&report.Findings[3].Detail=="No backups yet.","vault finding in English");
    Assert(report.GradeLabel!="Cần xử lý ngay"&&HealthCheck.SeverityLabel(HealthSeverity.High)=="Urgent","grade and severity labels in English");
    Assert(HealthCheck.Summary(report).StartsWith("Tweek Pro – PC health check "),"summary header in English");

    // Settings round-trip keeps the language and normalizes garbage back to Vietnamese.
    string file=Path.Combine(Path.GetTempPath(),"tweekpro-lang-"+Guid.NewGuid().ToString("N")+".json");
    try{
     var s=new Settings{Language="en"};s.Save(file);Assert(Settings.Load(file).Language=="en","language persisted");
     s.Language="klingon";s.Save(file);Assert(Settings.Load(file).Language=="vi","unknown language normalized");
    }finally{try{File.Delete(file);}catch(Exception){}}

    L.Lang="vi";
    Assert(HealthCheck.SeverityLabel(HealthSeverity.High)=="Khẩn"&&L.T("Ứng dụng")=="Ứng dụng","switching back restores Vietnamese");
   }finally{L.Lang=before;}
  }
 }
}
