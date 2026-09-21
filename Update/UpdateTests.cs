using System;

namespace TweekPro.Update {
 /// <summary>Self-tests for version compare, asset picking, release/tag JSON parsing (no network).</summary>
 public static class UpdateTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("UpdateTests: "+message);}

  public static void Run(){
   Assert(UpdateCheck.Normalize("v0.7")=="0.7"&&UpdateCheck.Normalize("0.7.1-beta")=="0.7.1"&&UpdateCheck.Normalize("")=="0","normalize tags");
   Assert(UpdateCheck.IsNewer("0.8","0.7")&&UpdateCheck.IsNewer("1.0","0.9")&&!UpdateCheck.IsNewer("0.7","0.7")&&!UpdateCheck.IsNewer("0.6","0.7"),"version compare");
   Assert(UpdateCheck.IsNewer("0.7.1","0.7"),"patch newer");

   string json="{\"tag_name\":\"v0.8\",\"name\":\"Tweek Pro 0.8\",\"html_url\":\"https://github.com/tuantien0001/TweekPro/releases/tag/v0.8\",\"body\":\"Notes\",\"assets\":[{\"name\":\"notes.txt\",\"browser_download_url\":\"https://example/notes.txt\"},{\"name\":\"TweekPro-Setup.exe\",\"browser_download_url\":\"https://example/setup.exe\"},{\"name\":\"TweekPro-portable.zip\",\"browser_download_url\":\"https://example/portable.zip\"}]}";
   var release=UpdateCheck.Parse(json);Assert(release!=null&&release.TagName=="v0.8"&&release.Assets.Length==3,"parse release");
   Assert(UpdateCheck.PickAsset(release.Assets)=="https://example/setup.exe","prefer installer exe");
   Assert(UpdateCheck.PickAsset(new[]{new GhAsset{Name="TweekPro.zip",BrowserDownloadUrl="https://example/z.zip"}})=="https://example/z.zip","zip fallback");
   Assert(UpdateCheck.PickAsset(null)==""&&UpdateCheck.PickAsset(new GhAsset[0])=="","empty assets");

   string prev=UpdateCheck.TestJson;string prevTags=UpdateCheck.TestTagsJson;bool prevForce=UpdateCheck.TestForceTagsFallback;
   try{
    UpdateCheck.TestJson=json;UpdateCheck.TestForceTagsFallback=false;
    var newer=UpdateCheck.Check("0.7");Assert(newer.UpdateAvailable&&newer.Latest=="0.8"&&newer.InstallerUrl.EndsWith("setup.exe")&&newer.PageUrl.Contains("releases")&&!newer.FromTagOnly,"0.7 sees 0.8: "+newer.Error);
    var same=UpdateCheck.Check("0.8");Assert(!same.UpdateAvailable&&same.Error=="","already latest");
    UpdateCheck.TestJson="{\"tag_name\":\"\",\"html_url\":\"x\"}";
    var bad=UpdateCheck.Check("0.7");Assert(!bad.UpdateAvailable&&bad.Error!="","empty tag errors");

    UpdateCheck.TestJson=null;UpdateCheck.TestForceTagsFallback=true;
    UpdateCheck.TestTagsJson="[{\"name\":\"v0.6\"},{\"name\":\"v0.9\"},{\"name\":\"docs\"},{\"name\":\"v0.8.1\"}]";
    var fromTags=UpdateCheck.Check("0.7");Assert(fromTags.FromTagOnly&&fromTags.UpdateAvailable&&fromTags.Latest=="0.9"&&fromTags.InstallerUrl==""&&fromTags.PageUrl.Contains("releases"),"tags fallback picks newest: "+fromTags.Latest+" err="+fromTags.Error);
    var tagsSame=UpdateCheck.Check("0.9");Assert(tagsSame.FromTagOnly&&!tagsSame.UpdateAvailable,"already on newest tag");
    UpdateCheck.TestTagsJson="[]";
    var noTags=UpdateCheck.Check("0.7");Assert(!noTags.UpdateAvailable&&noTags.Error!="","empty tags errors");
   }finally{UpdateCheck.TestJson=prev;UpdateCheck.TestTagsJson=prevTags;UpdateCheck.TestForceTagsFallback=prevForce;}
  }
 }
}
