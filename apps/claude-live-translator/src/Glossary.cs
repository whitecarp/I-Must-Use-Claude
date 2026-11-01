using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class GlossaryEntry {
        public string source, replacement, target;
    }
    internal static class GlossaryStore {
        internal static string FilePath { get { return Path.Combine(Preferences.DataDirectory,"glossary.json"); } }
        internal static List<GlossaryEntry> Defaults(){return new List<GlossaryEntry>{
            new GlossaryEntry{source="Claude",replacement="Claude",target="*"},
            new GlossaryEntry{source="Codex",replacement="Codex",target="*"},
            new GlossaryEntry{source="Skills",replacement="Skills",target="*"}};}
        internal static List<GlossaryEntry> Validate(IEnumerable<GlossaryEntry> source){
            var result=new List<GlossaryEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in source){
                if(item==null||String.IsNullOrWhiteSpace(item.source)||String.IsNullOrWhiteSpace(item.replacement)||item.target==null||
                    item.source.Length>64||item.replacement.Length>128||Regex.IsMatch(item.source+item.replacement,"[\\x00-\\x1F]")||
                    !Regex.IsMatch(item.target,"^(\\*|[a-z]{2,3}(?:-[A-Z]{2})?)$"))
                    throw new InvalidOperationException(Ui.T("请填写原词、固定译法和目标语言；原词最多 64 字，译法最多 128 字。","Enter a term, replacement and target. Limit: 64 characters per term, 128 per replacement."));
                string target=item.target=="zh"?"zh-CN":item.target=="zt"?"zh-TW":item.target;
                var entry=new GlossaryEntry{source=item.source.Trim(),replacement=item.replacement.Trim(),target=target};
                if(!seen.Add(target+"\n"+entry.source))throw new InvalidOperationException(Ui.T("同一目标语言不能重复添加相同原词。","A term can appear only once for each target language."));
                result.Add(entry);if(result.Count>100)throw new InvalidOperationException(Ui.T("最多保存 100 条术语。","Save up to 100 terms."));
            }
            return result;
        }
        internal static List<GlossaryEntry> Load(){
            if(!File.Exists(FilePath))return Defaults();
            try{
                if(new FileInfo(FilePath).Length>256000)throw new InvalidDataException();
                var data=Json.Decode(File.ReadAllText(FilePath,Encoding.UTF8));object raw;
                if(!data.TryGetValue("entries",out raw)||!(raw is IEnumerable)||raw is string)throw new InvalidDataException();
                var rows=new List<GlossaryEntry>();foreach(var value in (IEnumerable)raw){var row=value as Dictionary<string,object>;if(row==null)throw new InvalidDataException();rows.Add(new GlossaryEntry{source=Json.Str(row,"source"),replacement=Json.Str(row,"replacement"),target=Json.Str(row,"target")});}
                return Validate(rows);
            }catch{throw new InvalidOperationException(Ui.T("术语词典无法读取，请检查 glossary.json；原文件未改动。","Cannot read glossary.json. Check the file; it has not been changed."));}
        }
        internal static void Save(IEnumerable<GlossaryEntry> rows){
            var entries=Validate(rows);Directory.CreateDirectory(Preferences.DataDirectory);
            string temporary=Path.Combine(Preferences.DataDirectory,"glossary-"+Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllText(temporary,Json.Encode(new{version=1,entries=entries}),new UTF8Encoding(false));
                if(File.Exists(FilePath))File.Replace(temporary,FilePath,null);else File.Move(temporary,FilePath);
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    internal sealed class GlossaryForm:Form {
        private readonly DataGridView grid;
        internal GlossaryForm(){
            Text=Ui.T("术语词典","Glossary");Size=new Size(650,460);MinimumSize=new Size(610,390);StartPosition=FormStartPosition.CenterParent;
            BackColor=Theme.Cream;ForeColor=Theme.Ink;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=3};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=Ui.T("固定常用词的译法，在线和离线都生效。\n区分大小写；较长词组优先，链接和代码保持原样。\n只保存你填写的词典；翻译记录不会保存到磁盘。","Keep recurring terms consistent, online and offline.\nCase-sensitive; longer phrases win. Links and code stay unchanged.\nOnly this glossary is saved. Translation history stays in memory."),ForeColor=Theme.Muted},0,0);
            grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AllowUserToAddRows=true,AllowUserToDeleteRows=true,RowHeadersWidth=28,SelectionMode=DataGridViewSelectionMode.CellSelect};
            grid.Columns.Add("source",Ui.T("原词 / 词组","Term / phrase"));grid.Columns.Add("replacement",Ui.T("固定译法","Replacement"));
            var targets=new DataGridViewComboBoxColumn{Name="target",HeaderText=Ui.T("译成此语言时生效","Apply when translating to"),DisplayMember="Name",ValueMember="Code",FlatStyle=FlatStyle.Flat};
            var choices=new List<GlossaryTarget>{new GlossaryTarget{Code="*",Name=Ui.T("所有语言","All languages")}};
            foreach(var choice in LanguageChoice.All)choices.Add(new GlossaryTarget{Code=choice.Code,Name=choice.Name});
            var entries=GlossaryStore.Load();foreach(var item in entries)if(!choices.Exists(delegate(GlossaryTarget c){return c.Code==item.target;}))choices.Add(new GlossaryTarget{Code=item.target,Name=item.target});
            targets.DataSource=choices;grid.Columns.Add(targets);grid.Columns["target"].FillWeight=115;
            grid.DefaultValuesNeeded+=delegate(object sender,DataGridViewRowEventArgs args){args.Row.Cells["target"].Value="*";};
            foreach(var entry in entries)grid.Rows.Add(entry.source,entry.replacement,entry.target);
            layout.Controls.Add(grid,0,1);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var save=Theme.Button(Ui.T("保存","Save"),true);save.Click+=delegate{SaveRows();};
            var cancel=Theme.Button(Ui.T("取消","Cancel"),false);cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            var remove=Theme.Button(Ui.T("删除此行","Remove row"),false);remove.Width=112;remove.Click+=delegate{if(grid.CurrentRow!=null&&!grid.CurrentRow.IsNewRow)grid.Rows.Remove(grid.CurrentRow);};
            actions.Controls.AddRange(new Control[]{save,cancel,remove});layout.Controls.Add(actions,0,2);Controls.Add(layout);CancelButton=cancel;
        }
        private void SaveRows(){
            try{grid.EndEdit();var entries=new List<GlossaryEntry>();foreach(DataGridViewRow row in grid.Rows){if(row.IsNewRow)continue;entries.Add(new GlossaryEntry{source=Convert.ToString(row.Cells[0].Value),replacement=Convert.ToString(row.Cells[1].Value),target=Convert.ToString(row.Cells[2].Value)});}GlossaryStore.Save(entries);DialogResult=DialogResult.OK;Close();}
            catch(Exception error){MessageBox.Show(this,error is InvalidOperationException?error.Message:Ui.T("词典未保存，请检查文件夹写入权限。","Glossary was not saved. Check folder write permissions."),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        private sealed class GlossaryTarget { public string Code{get;set;}public string Name{get;set;} }
    }
}
