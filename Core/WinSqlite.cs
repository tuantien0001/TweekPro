using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TweekPro.Core {
 /// <summary>
 /// Minimal SQLite access through winsqlite3.dll, which ships with Windows 10+ (System32). No bundled native library, so the
 /// Linux self-test skips SQL paths (Available is false there) and callers fall back to whole-file handling.
 /// </summary>
 public static class WinSqlite {
  const string Lib="winsqlite3.dll";
  const int OpenReadWrite=2|4,OpenReadOnly=1,Row=100,Done=101;
  static readonly IntPtr Transient=new IntPtr(-1);
  static bool? available;

  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] filename,out IntPtr db,int flags,IntPtr vfs);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr db);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int nByte,out IntPtr stmt,IntPtr tail);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt,int col);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr stmt,int col);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr stmt,int index,byte[] text,int n,IntPtr destructor);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_changes(IntPtr db);
  [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);

  /// <summary>True when winsqlite3.dll exists in System32 (Windows 10 and later).</summary>
  public static bool Available {
   get {
    if(available.HasValue)return available.Value;
    try{string sys=Environment.GetFolderPath(Environment.SpecialFolder.System);available=Environment.OSVersion.Platform==PlatformID.Win32NT&&!String.IsNullOrEmpty(sys)&&File.Exists(Path.Combine(sys,Lib));}catch(Exception){available=false;}
    return available.Value;
   }
  }

  static byte[] Utf8(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  static string Message(IntPtr db){try{return Marshal.PtrToStringAnsi(sqlite3_errmsg(db))??"";}catch(Exception){return "";}}
  static string ColumnText(IntPtr stmt,int col){IntPtr p=sqlite3_column_text(stmt,col);if(p==IntPtr.Zero)return null;int n=sqlite3_column_bytes(stmt,col);var bytes=new byte[n];Marshal.Copy(p,bytes,0,n);return Encoding.UTF8.GetString(bytes);}

  static IntPtr Open(string file,bool write){
   if(!Available)throw new IOException("winsqlite3.dll không có trên hệ thống này.");
   IntPtr db;int rc=sqlite3_open_v2(Utf8(file),out db,write?OpenReadWrite:OpenReadOnly,IntPtr.Zero);
   if(rc!=0){string msg=Message(db);sqlite3_close_v2(db);throw new IOException("Không mở được cơ sở dữ liệu ("+rc+"): "+msg);}
   return db;
  }

  static IntPtr Prepare(IntPtr db,string sql,string[] binds){
   IntPtr stmt;int rc=sqlite3_prepare_v2(db,Utf8(sql),-1,out stmt,IntPtr.Zero);
   if(rc!=0)throw new IOException("SQL lỗi ("+rc+"): "+Message(db));
   for(int i=0;i<(binds??new string[0]).Length;i++){var bytes=Encoding.UTF8.GetBytes(binds[i]??"");rc=sqlite3_bind_text(stmt,i+1,bytes,bytes.Length,Transient);if(rc!=0){sqlite3_finalize(stmt);throw new IOException("Không gán được tham số "+(i+1)+": "+Message(db));}}
   return stmt;
  }

  /// <summary>Runs a SELECT and returns every row as text columns (null for SQL NULL).</summary>
  public static List<string[]> Query(string file,string sql,params string[] binds){
   var rows=new List<string[]>();IntPtr db=Open(file,false);
   try{
    IntPtr stmt=Prepare(db,sql,binds);
    try{int cols=sqlite3_column_count(stmt);int rc;while((rc=sqlite3_step(stmt))==Row){var row=new string[cols];for(int c=0;c<cols;c++)row[c]=ColumnText(stmt,c);rows.Add(row);}if(rc!=Done)throw new IOException("Đọc dữ liệu lỗi ("+rc+"): "+Message(db));}
    finally{sqlite3_finalize(stmt);}
   }finally{sqlite3_close_v2(db);}
   return rows;
  }

  /// <summary>Runs one statement that changes data and returns the affected row count.</summary>
  public static int Execute(string file,string sql,params string[] binds){
   IntPtr db=Open(file,true);
   try{
    IntPtr stmt=Prepare(db,sql,binds);
    try{int rc=sqlite3_step(stmt);if(rc!=Done&&rc!=Row)throw new IOException("Thực thi lỗi ("+rc+"): "+Message(db));}
    finally{sqlite3_finalize(stmt);}
    return sqlite3_changes(db);
   }finally{sqlite3_close_v2(db);}
  }

  /// <summary>Row count of a table; -1 when the table is missing or the file is not a database.</summary>
  public static long Count(string file,string table){
   try{var rows=Query(file,"SELECT count(*) FROM \""+table.Replace("\"","\"\"")+"\"");long n;return rows.Count>0&&long.TryParse(rows[0][0],out n)?n:-1;}catch(Exception){return -1;}
  }
 }
}
