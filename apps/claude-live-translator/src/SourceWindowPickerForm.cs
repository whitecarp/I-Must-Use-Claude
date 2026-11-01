using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // Lists window identities only. No desktop capture or accessibility-tree scan is used.
    internal sealed class SourceWindowPickerForm : Form {
        private readonly SourceWindow current;
        private readonly bool fixtureMode;
        private readonly List<SourceWindow> fixtures;
        private readonly Dictionary<string,Bitmap> icons=new Dictionary<string,Bitmap>();
        private readonly HashSet<string> iconAttempts=new HashSet<string>();
        private readonly ToolTip tooltips=new ToolTip { AutoPopDelay=15000,InitialDelay=400,ReshowDelay=100 };
        private readonly Bitmap fallbackIcon;
        private readonly TextBox search;
        private readonly FlowLayoutPanel cardsPanel;
        private readonly Label heading,description,hint,searchLabel,footnote;
        private readonly Button refresh,cancel;
        private List<SourceWindow> allSources=new List<SourceWindow>();
        private readonly List<SourceWindowCard> cards=new List<SourceWindowCard>();
        private CancellationTokenSource loadCancellation;
        private int generation,iconGeneration=-1;
        private bool loading,stopping,closedHint;
        public SourceWindow SelectedSource { get; private set; }

        public SourceWindowPickerForm(SourceWindow current) : this(current,null) { }
        // Fixtures deliberately bypass all enumeration and native icon retrieval.
        internal SourceWindowPickerForm(SourceWindow current,IEnumerable<SourceWindow> fixtures) {
            this.current=current;fixtureMode=fixtures!=null;
            if(fixtureMode)this.fixtures=new List<SourceWindow>(fixtures);
            fallbackIcon=SystemIcons.Application.ToBitmap();
            Font=new Font("Microsoft YaHei UI",10F);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;
            Size=new Size(770,660);MinimumSize=new Size(610,480);ShowInTaskbar=false;
            MinimizeBox=false;MaximizeBox=false;KeyPreview=true;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(22,14,22,16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
            var header=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0) };
            header.Controls.Add(new WhaleBadge { Location=new Point(0,9),Size=new Size(72,60) });
            heading=new Label { Location=new Point(86,8),Height=34,AutoSize=false,Font=new Font(Font.FontFamily,18,FontStyle.Bold),ForeColor=Theme.Ink };
            description=new Label { Location=new Point(88,48),Height=36,AutoSize=false,Font=new Font(Font.FontFamily,9F),ForeColor=Theme.Muted };
            header.Controls.Add(heading);header.Controls.Add(description);
            header.Resize+=delegate { heading.Width=Math.Max(1,header.ClientSize.Width-88);description.Width=Math.Max(1,header.ClientSize.Width-90); };
            layout.Controls.Add(header,0,0);

            var searchRow=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0) };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,99));
            searchLabel=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,0,6,0) };
            search=new TextBox { Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,8,12,0),TabIndex=0 };
            search.TextChanged+=delegate { RenderCards(); };
            refresh=Theme.Button("",false);refresh.Dock=DockStyle.Fill;refresh.Margin=new Padding(0,3,0,5);refresh.TabIndex=1;
            refresh.Click+=async delegate { closedHint=false;await RefreshWindowsAsync(); };
            searchRow.Controls.Add(searchLabel,0,0);searchRow.Controls.Add(search,1,0);searchRow.Controls.Add(refresh,2,0);
            layout.Controls.Add(searchRow,0,1);
            hint=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,9F),AutoEllipsis=true,Margin=new Padding(0) };
            layout.Controls.Add(hint,0,2);
            cardsPanel=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,BackColor=Theme.Cream,Margin=new Padding(0),Padding=new Padding(0,3,0,0),TabIndex=2 };
            cardsPanel.Resize+=delegate { ResizeCards(); };
            layout.Controls.Add(cardsPanel,0,3);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0,5,0,0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));
            footnote=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8.5F),Margin=new Padding(0,0,12,0) };
            cancel=Theme.Button("",false);cancel.Dock=DockStyle.Fill;cancel.DialogResult=DialogResult.Cancel;cancel.Margin=new Padding(0,4,0,1);cancel.TabIndex=3;
            footer.Controls.Add(footnote,0,0);footer.Controls.Add(cancel,1,0);layout.Controls.Add(footer,0,4);
            Controls.Add(layout);CancelButton=cancel;
            Ui.LanguageChanged+=RefreshLanguage;
            if(fixtureMode)allSources=new List<SourceWindow>(this.fixtures);
            RefreshLanguage();
            Shown+=async delegate {
                search.Focus();
                if(!fixtureMode)await RefreshWindowsAsync();
            };
        }

        private string T(string zh,string en) { return Ui.T(zh,en); }
        private void RefreshLanguage() {
            if(stopping)return;
            Text=T("选择翻译窗口 · 大肥鱼","Choose a window · Whale Translator");
            heading.Text=T("这次想读哪个窗口？","Which window shall we read?");
            description.Text=T("点一下窗口卡片，就可以开始划选翻译。","Click a window card, then select text to translate.");
            searchLabel.Text=T("搜索","Search");search.AccessibleName=T("搜索程序或窗口标题","Search app or window title");
            refresh.Text=T("刷新","Refresh");cancel.Text=T("取消","Cancel");
            footnote.Text=T("只读取所选窗口里的划选文字。Esc 可取消。","Only selections in the chosen window. Esc to cancel.");
            RenderCards();
        }

        private async Task RefreshWindowsAsync() {
            if(stopping||loading)return;
            loading=true;refresh.Enabled=false;iconAttempts.Clear();
            if(loadCancellation!=null) { loadCancellation.Cancel();loadCancellation.Dispose(); }
            loadCancellation=new CancellationTokenSource();var cancellation=loadCancellation.Token;
            int request=++generation;
            hint.Text=closedHint?T("这个窗口已关闭，正在刷新列表…","That window closed. Refreshing the list…"):T("正在寻找打开的窗口…","Finding your open windows…");
            try {
                var found=fixtureMode?new List<SourceWindow>(fixtures):await Task.Run(delegate { return SourceWindow.List(cancellation); },cancellation);
                if(stopping||request!=generation||cancellation.IsCancellationRequested)return;
                allSources=found;loading=false;RenderCards();
            } catch(OperationCanceledException) { }
            catch {
                if(!stopping&&request==generation)hint.Text=T("暂时无法获取窗口，请点刷新重试。","Could not list windows. Click Refresh to retry.");
            } finally {
                if(!stopping&&request==generation) { loading=false;refresh.Enabled=true; }
            }
        }

        private void RenderCards() {
            if(stopping||cardsPanel==null)return;
            string query=(search.Text??"").Trim();
            cardsPanel.SuspendLayout();tooltips.RemoveAll();
            while(cardsPanel.Controls.Count>0)cardsPanel.Controls[0].Dispose();
            cards.Clear();
            foreach(var source in allSources) {
                if(source==null||!MatchesSearch(source,query))continue;
                Bitmap icon;if(!icons.TryGetValue(IdentityKey(source),out icon))icon=fallbackIcon;
                bool isCurrent=current!=null&&source.Matches(current.Handle,current.ProcessId,current.Started);
                var card=new SourceWindowCard(source,isCurrent,icon,Font) { TabIndex=cards.Count,Margin=new Padding(0,0,12,10) };
                card.Click+=delegate(object sender,EventArgs args) { ChooseSource(((SourceWindowCard)sender).Source); };
                tooltips.SetToolTip(card,source.FriendlyAppName+Environment.NewLine+(source.Title??""));
                cards.Add(card);cardsPanel.Controls.Add(card);
            }
            if(cards.Count==0&&!loading) {
                var empty=new Label { AutoSize=false,Height=84,Margin=new Padding(12,20,12,0),ForeColor=Theme.Muted,TextAlign=ContentAlignment.TopLeft,
                    Text=query.Length>0?T("没有找到匹配的窗口。\r\n试试程序名或窗口标题里的其他词。","No matching windows.\r\nTry another app name or title."):T("还没有可选择的窗口。\r\n打开要阅读的应用，再点「刷新」。","No windows to choose yet.\r\nOpen the app you want to read, then click Refresh.") };
                cardsPanel.Controls.Add(empty);
            }
            ResizeCards();cardsPanel.ResumeLayout();UpdateHint();BeginIconLoad();
        }
        internal static bool MatchesSearch(SourceWindow source,string query) {
            return String.IsNullOrWhiteSpace(query)||(source.Title??"").IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||
                source.FriendlyAppName.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||(source.App??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
        }
        private void ResizeCards() {
            if(cardsPanel==null)return;
            int available=Math.Max(120,cardsPanel.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);
            int columns=available>=650?2:1;int width=available/columns-12;
            foreach(Control child in cardsPanel.Controls)child.Width=width;
        }
        private void UpdateHint() {
            if(loading)return;
            hint.ForeColor=closedHint?Color.FromArgb(190,72,72):Theme.Muted;
            hint.Text=closedHint?T("这个窗口已关闭，请选择其他卡片。","That window closed. Please choose another card."):
                T("找到 ","Found ")+cards.Count+T(" 个窗口 · 点击卡片即可选择"," windows · Click a card to choose");
        }
        private async void ChooseSource(SourceWindow source) {
            if(stopping||loading)return;
            if(source==null||!source.IsAlive()) {
                closedHint=true;await RefreshWindowsAsync();return;
            }
            SelectedSource=source;DialogResult=DialogResult.OK;Close();
        }
        private static string IdentityKey(SourceWindow source) { return source.Handle.ToInt64()+":"+source.ProcessId+":"+source.Started; }
        private async void BeginIconLoad() {
            if(stopping||fixtureMode||loading||loadCancellation==null||iconGeneration==generation)return;
            int request=generation;var cancellation=loadCancellation.Token;iconGeneration=request;
            try {
                // One bounded icon query at a time; a search change changes the next candidate.
                while(!stopping&&request==generation&&!cancellation.IsCancellationRequested) {
                    SourceWindow next=null;string key=null;
                    foreach(var card in cards) {
                        string candidate=IdentityKey(card.Source);
                        if(!icons.ContainsKey(candidate)&&!iconAttempts.Contains(candidate)) { next=card.Source;key=candidate;break; }
                    }
                    if(next==null)break;
                    Bitmap icon=await Task.Run(delegate { return cancellation.IsCancellationRequested?null:next.GetSmallIcon(); });
                    if(stopping||request!=generation||cancellation.IsCancellationRequested) { if(icon!=null)icon.Dispose();return; }
                    iconAttempts.Add(key);
                    if(icon!=null) {
                        icons[key]=icon;
                        foreach(var card in cards)if(IdentityKey(card.Source)==key) { card.IconImage=icon;card.Invalidate(); }
                    }
                }
            } catch { /* A missing icon does not make a window unavailable. */ }
            finally { if(iconGeneration==request)iconGeneration=-1; }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);if(e.Cancel)return;
            stopping=true;++generation;if(loadCancellation!=null)loadCancellation.Cancel();
        }
        protected override void Dispose(bool disposing) {
            if(disposing&&!stopping) { stopping=true;++generation;if(loadCancellation!=null)loadCancellation.Cancel(); }
            if(disposing) {
                Ui.LanguageChanged-=RefreshLanguage;tooltips.Dispose();
                if(loadCancellation!=null) { loadCancellation.Dispose();loadCancellation=null; }
                foreach(var icon in icons.Values)icon.Dispose();icons.Clear();fallbackIcon.Dispose();
            }
            base.Dispose(disposing);
        }
        internal void RenderPreview(string path) {
            if(!fixtureMode)throw new InvalidOperationException("Preview requires synthetic window fixtures.");
            TopMost=false;ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path); }
            Close();
        }
    }

    internal sealed class SourceWindowCard : Control {
        internal readonly SourceWindow Source;
        internal readonly bool IsCurrent;
        internal Bitmap IconImage;
        private readonly Font appFont,titleFont;
        private bool hover;
        internal SourceWindowCard(SourceWindow source,bool current,Bitmap icon,Font font) {
            Source=source;IsCurrent=current;IconImage=icon;Font=font;
            appFont=new Font(font.FontFamily,9F,FontStyle.Bold);titleFont=new Font(font.FontFamily,10F);
            Height=108;TabStop=true;Cursor=Cursors.Hand;
            AccessibleRole=AccessibleRole.PushButton;AccessibleName=source.FriendlyAppName+" · "+source.Title;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate();base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate();base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space) { e.Handled=true;e.SuppressKeyPress=true;OnClick(EventArgs.Empty); }
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys keyData) { return (keyData&Keys.KeyCode)==Keys.Enter||base.IsInputKey(keyData); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g=e.Graphics;g.Clear(Theme.Cream);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Width<20||Height<20)return;
            var face=new RectangleF(1,1,Width-3,Height-5);
            using(var path=Theme.RoundedRect(face,15f))
            using(var fill=new SolidBrush(IsCurrent?Theme.Blue:hover?Theme.Mist:Color.White))
            using(var border=new Pen(IsCurrent||Focused?Theme.BlueDark:Theme.Line,Focused?2f:1f)) { g.FillPath(fill,path);g.DrawPath(border,path); }
            if(IconImage!=null)g.DrawImage(IconImage,new Rectangle(16,20,32,32));
            int textLeft=61,textWidth=Math.Max(1,Width-textLeft-14);
            if(IsCurrent) {
                string badge=Ui.T("正在使用","Current");int badgeWidth=TextRenderer.MeasureText(badge,appFont).Width+8;
                TextRenderer.DrawText(g,badge,appFont,new Rectangle(Width-badgeWidth-13,14,badgeWidth,20),Theme.BlueDark,TextFormatFlags.Right|TextFormatFlags.SingleLine);
                textWidth=Math.Max(1,textWidth-badgeWidth-8);
            }
            TextRenderer.DrawText(g,Source.FriendlyAppName,appFont,new Rectangle(textLeft,14,textWidth,22),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g,Source.Title??"",titleFont,new Rectangle(textLeft,40,Math.Max(1,Width-textLeft-14),49),Theme.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.TextBoxControl);
            if(Focused&&ShowFocusCues)using(var focus=Theme.RoundedRect(RectangleF.Inflate(face,-4,-4),11f))using(var pen=new Pen(Theme.BlueDark,1f) { DashStyle=DashStyle.Dot })g.DrawPath(pen,focus);
        }
        protected override void Dispose(bool disposing) { if(disposing) { appFont.Dispose();titleFont.Dispose(); }base.Dispose(disposing); }
    }
}
