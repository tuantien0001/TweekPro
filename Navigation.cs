using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TweekPro {
 /// <summary>Keyboard-accessible vertical navigation backed by the existing tab pages.</summary>
 public sealed class TabStrip:UserControl {
  readonly TabControl tabs;
  readonly FlowLayoutPanel menu=new FlowLayoutPanel();
  readonly Dictionary<TabPage,Button> links=new Dictionary<TabPage,Button>();
  public TabStrip(TabControl target){
   tabs=target;Dock=DockStyle.Left;Width=238;BackColor=Theme.Surface;Padding=new Padding(10,12,8,8);
   menu.Dock=DockStyle.Fill;menu.FlowDirection=FlowDirection.TopDown;menu.WrapContents=false;menu.AutoScroll=true;menu.BackColor=Theme.Surface;
   Controls.Add(menu);menu.SizeChanged+=(s,e)=>FitWidths();tabs.SelectedIndexChanged+=(s,e)=>SelectActive();
   Paint+=(s,e)=>{using(var p=new Pen(Theme.Border))e.Graphics.DrawLine(p,Width-1,0,Width-1,Height);};
  }
  public static void HideNativeHeaders(TabControl t){t.Appearance=TabAppearance.FlatButtons;t.ItemSize=new Size(0,1);t.SizeMode=TabSizeMode.Fixed;t.Multiline=false;t.DrawMode=TabDrawMode.Normal;t.Padding=new Point(0,0);}
  static int Group(TabPage page){
   switch(Branding.GlyphKey(page.Text)){
    case "apps":case "windows":case "puzzle":return 0;
    case "magnifier":case "trash":case "folder":case "duplicate":case "download":case "footprints":case "registry":case "drive":return 1;
    case "chart":case "bolt":case "explorer":case "globe":case "layers":return 2;
    default:return -1;
   }
  }
  /// <summary>Builds links after the canonical page collection has been populated.</summary>
  public void Build(){
   menu.SuspendLayout();
   var caption=new Label{Text=Core.L.T("ĐIỀU HƯỚNG"),ForeColor=Theme.Muted,Font=Theme.Small,Height=28,Margin=new Padding(10,0,0,4)};menu.Controls.Add(caption);
   foreach(TabPage p in tabs.TabPages)if(Branding.GlyphKey(p.Text)=="pulse")AddLink(menu,p);
   for(int i=0;i<3;i++)foreach(TabPage p in tabs.TabPages)if(Group(p)==i)AddLink(menu,p);
   foreach(TabPage p in tabs.TabPages)if(Group(p)<0&&Branding.GlyphKey(p.Text)!="pulse")AddLink(menu,p);
   menu.ResumeLayout(true);SelectActive();FitWidths();
  }
  void AddLink(FlowLayoutPanel parent,TabPage page){
   bool uninstall=Branding.GlyphKey(page.Text)=="apps";string title=uninstall?Core.L.T("Gỡ ứng dụng"):page.Text;
   var button=new Button{Text=title,AccessibleName=title,Height=30,Font=uninstall?Theme.Strong:Theme.Small,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(34,0,0,0),FlatStyle=FlatStyle.Flat,Margin=new Padding(0,1,0,1),BackColor=Theme.Surface,ForeColor=Theme.Muted,Cursor=Cursors.Hand,AutoEllipsis=true};
   button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Theme.SoftButtonHover;
   button.Click+=(s,e)=>tabs.SelectedTab=page;
   button.Paint+=(s,e)=>{float scale=button.Height/30f;Branding.DrawTabGlyph(e.Graphics,page.Text,new Rectangle((int)(10*scale),(button.Height-(int)(16*scale))/2,(int)(16*scale),(int)(16*scale)),button.ForeColor);if(tabs.SelectedTab==page)using(var b=new SolidBrush(Theme.Primary))e.Graphics.FillRectangle(b,0,6,3,button.Height-12);};
   links.Add(page,button);parent.Controls.Add(button);
  }
  void SelectActive(){
   if(tabs.SelectedTab==null)return;
   foreach(var pair in links){bool active=pair.Key==tabs.SelectedTab;pair.Value.BackColor=active?Theme.SoftButton:Theme.Surface;pair.Value.ForeColor=active||Branding.GlyphKey(pair.Key.Text)=="apps"?Theme.Primary:Theme.Muted;pair.Value.Invalidate();}
  }
  void FitWidths(){
   int width=Math.Max(120,menu.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);
   foreach(Control c in menu.Controls)c.Width=width;
  }
 }
}
