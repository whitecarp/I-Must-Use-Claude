using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class GlossaryHistoryTests {
    private static int checks;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,Private).GetValue(owner);}
    private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Private).Invoke(owner,args);}
    private static void Render(Form form,string filename){form.TopMost=false;form.ShowInTaskbar=false;form.Opacity=0;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.Show();Application.DoEvents();form.PerformLayout();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,filename));}form.Hide();}
    [STAThread] public static int Main(){
        try{
            string fixture=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
            Check(!String.IsNullOrWhiteSpace(fixture)&&Path.GetFileName(fixture).StartsWith("glossary-history-fixture-",StringComparison.Ordinal),"isolated fixture directory required");
            Check(!Directory.Exists(fixture),"fixture must start empty");
            Check(GlossaryStore.Load().Count==3,"starter glossary available without writing");
            Check(!Directory.Exists(fixture),"reading defaults does not create files");
            GlossaryStore.Save(new[]{new GlossaryEntry{source="Skills",replacement="技能",target="zh-CN"}});
            Check(GlossaryStore.Load()[0].replacement=="技能","UTF-8 glossary survives read-back");
            if(!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAFEIYU_FIXTURE_BACKEND"))){
                using(var worker=new Worker()){
                    Check(worker.Translate("Keep Skills with Claude.","auto:zh-CN",false).GetAwaiter().GetResult()=="Keep 技能 with Claude.","real Worker protocol carries glossary into Google engine with synthetic transport");
                    Check(worker.Translate("Keep Skills with Claude.","en_zh",true).GetAwaiter().GetResult()=="Keep 技能 with Claude.","real Worker protocol carries glossary into offline engine with synthetic transport");
                    GlossaryStore.Save(new[]{new GlossaryEntry{source="Skills",replacement="能力",target="zh-CN"}});
                    Check(worker.Translate("Keep Skills with Claude.","en_zh",true).GetAwaiter().GetResult()=="Keep 能力 with Claude.","next request uses edited dictionary while worker remains running");
                }
            }
            string before=File.ReadAllText(GlossaryStore.FilePath);
            bool refused=false;try{GlossaryStore.Save(new[]{new GlossaryEntry{source="",replacement="x",target="*"}});}catch(InvalidOperationException){refused=true;}
            Check(refused&&File.ReadAllText(GlossaryStore.FilePath)==before,"invalid edit preserves previous dictionary");
            GlossaryStore.Save(new GlossaryEntry[0]);Check(GlossaryStore.Load().Count==0,"empty glossary disables all defaults explicitly");
            var history=new TranslationHistory();Check(!history.CanPrevious&&!history.CanNext,"initial history has no navigation");
            history.Add("one","一","auto:zh-CN",false);history.Add("two","二","auto:zh-CN",true);
            Check(history.Current.Source=="two"&&history.CanPrevious&&!history.CanNext,"latest result is current");
            Check(history.Move(-1).Result=="一"&&history.CanNext,"previous result exact");
            Check(history.Move(1).Offline,"engine retained");
            history.LeaveView();Check(history.Move(-1).Source=="two","pending selection can return to last successful result");
            for(int i=0;i<30;i++)history.Add("source"+i,"result"+i,"auto:en",false);
            Check(history.Count==20,"session entry count bounded");
            for(int i=0;i<20;i++)history.Add(new string('x',32000),new string('y',32000),"auto:en",false);
            Check(history.Count==4,"session character total bounded");history.Clear();Check(history.Count==0,"closing can clear memory");
            Preferences.Offline=false;Preferences.Pin=false;Ui.English=true;
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new CompanionForm(true)){
                var session=Get<TranslationHistory>(form,"history");session.Add("source one","result one","auto:en",false);session.Add("source two","result two","auto:fr",true);
                int beforeEpoch=Get<int>(form,"epoch");Call(form,"NavigateHistory",-1);
                Check(Get<int>(form,"epoch")>beforeEpoch,"history invalidates in-flight result epoch");
                Check(Get<TextBox>(form,"original").Text=="source one"&&Get<TextBox>(form,"translated").Text=="result one","history restores both panes");
                var snapshot=Get<Snapshot>(form,"latest");Check(snapshot.Conversation=="history"&&Get<bool>(form,"needsFreshSelection"),"history cannot be mistaken for new source selection");
                Check(!ResultGate.CanShow(beforeEpoch,Get<int>(form,"epoch"),snapshot,snapshot),"late worker result rejected after history navigation");
                Call(form,"NavigateHistory",1);Check(Get<TextBox>(form,"translated").Text=="result two","next restores forward entry");
                Check(Get<Label>(form,"status").Text.Contains("Offline"),"historical engine clearly labelled");
                Check(!form.Visible,"fixture form never shown");
                form.Size=new Size(660,900);Render(form,"translator-090-en.png");
                Ui.SetEnglish(false);Check(Get<Button>(form,"previousResult").Text=="上一条","bilingual history controls refresh");
                Render(form,"translator-090-zh.png");
                Check(Directory.GetFiles(fixture).Length==1,"history has no disk persistence");
                form.Close();
            }
            GlossaryStore.Save(GlossaryStore.Defaults());
            foreach(bool english in new[]{true,false}){Ui.English=english;using(var glossary=new GlossaryForm()){Check(!glossary.Visible,"glossary editor initially hidden");Render(glossary,english?"glossary-090-en.png":"glossary-090-zh.png");}}
            Console.WriteLine("GlossaryHistoryTests passed: "+checks+" isolated checks; no desktop selection, clipboard or network used.");return 0;
        }catch(Exception error){Console.WriteLine(error.GetBaseException());return 1;}
    }
}
