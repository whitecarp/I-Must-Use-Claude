using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class CompanionForm : Form {
        private ComboBox direction,engine;
        private Label subtitle,privacy,targetLabel,autoLabel;
        private bool changingOptions,needsFreshSelection;
        private Button toggle, paste, copy, checkConnection, chooseSource, clearSource, language;
        private Label sourceName;
        private PictureBox sourceIcon;
        private SourceWindowPickerForm sourcePicker;
        private bool pickingSource, sourceActiveBeforePick;
        private SourceWindow sourceBeforePick;
        private int sourceIconEpoch;
        private CheckBox pin;
        private TextBox translated, original;
        private Label status, googleStatus, lockInfo;
        private readonly List<Action> labels = new List<Action>();
        private readonly SelectionWatcher watcher = new SelectionWatcher();
        private readonly System.Windows.Forms.Timer lockTimer = new System.Windows.Forms.Timer { Interval=1000 };
        private readonly WaitTicker wait = new WaitTicker(), connectionWait = new WaitTicker();
        private readonly Worker worker = new Worker();
        private Worker connectionWorker;
        private Process composeReaderProcess;
        private CancellationTokenSource captureCancellation;
        private readonly List<Task> captureTransactions = new List<Task>();
        private SourceWindow locked;
        private Snapshot latest;
        private Snapshot pendingOfflineRetry;
        private int pendingOfflineEpoch;
        private Button offlineRetry;
        private string seen="", done="";
        private int epoch, selectionEpoch;
        private bool active, translating, closing;
        private readonly bool preview;
        private string T(string zh,string en) { return Ui.T(zh,en); }

        public CompanionForm(bool isPreview) {
            preview=isPreview;
            var area=Screen.PrimaryScreen.WorkingArea;
            Size=new Size(660,Math.Min(900,area.Height-40));
            MinimumSize=new Size(640,740);
            StartPosition=FormStartPosition.Manual;
            Location=new Point(Math.Max(area.Left,area.Right-Width-28),area.Top+20);
            Font=new Font("Microsoft YaHei UI",10F); BackColor=Theme.Cream; ForeColor=Theme.Ink;
            AutoScaleMode=AutoScaleMode.Dpi; TopMost=Preferences.Pin;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(20,12,20,12) };
            foreach(float height in new float[]{78,134,46,82})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
            var heading=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0) };
            heading.Controls.Add(new WhaleBadge { Location=Point.Empty,Size=new Size(86,68) });
            var title=Label("大肥鱼翻译","Whale Translator");title.Font=new Font(Font.FontFamily,19,FontStyle.Bold);title.Location=new Point(94,5);heading.Controls.Add(title);
            subtitle=new Label{AutoSize=true,ForeColor=Theme.Muted};subtitle.Location=new Point(96,42);subtitle.Font=new Font(Font.FontFamily,9F);heading.Controls.Add(subtitle);
            language=Button("English","中文",false,86);language.Click+=delegate { Ui.SetEnglish(!Ui.English);if(!preview)Preferences.Save(); };
            heading.Controls.Add(language);heading.Resize+=delegate{language.Location=new Point(heading.ClientSize.Width-language.Width,2);};
            layout.Controls.Add(heading,0,0);

            var sourceRows=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=new Padding(0),BackColor=Color.White };
            sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));
            sourceRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));sourceRows.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
            sourceIcon=new PictureBox { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.CenterImage,Image=SystemIcons.Application.ToBitmap(),Margin=new Padding(0,0,8,0) };
            sourceRows.Controls.Add(sourceIcon,0,0);
            sourceName=new Label {Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,0,8,0),Font=new Font(Font.FontFamily,10.5F,FontStyle.Bold),ForeColor=Theme.Ink};
            sourceRows.Controls.Add(sourceName,1,0);
            var sourceActions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0)};
            chooseSource=Button("选择翻译窗口","Choose window",true,128);chooseSource.Click+=delegate{ChooseSource();};
            clearSource=Button("取消选择","Clear selection",false,128);clearSource.Height=28;clearSource.Margin=new Padding(0);clearSource.Click+=delegate{UnlockSource(false);};
            sourceActions.Controls.AddRange(new Control[]{chooseSource,clearSource});sourceRows.Controls.Add(sourceActions,2,0);
            lockInfo=new Label {Dock=DockStyle.Fill,ForeColor=Theme.Muted,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0),Font=new Font(Font.FontFamily,8.5F)};
            sourceRows.Controls.Add(lockInfo,0,1);sourceRows.SetColumnSpan(lockInfo,3);layout.Controls.Add(Theme.Card(sourceRows,new Padding(0,1,0,6)),0,1);

            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0) };
            toggle=Button("暂停划选","Pause",true,92);toggle.Click+=delegate{if(active)Pause();else Start();};
            paste=Button("粘贴翻译","Paste",false,84);paste.Click+=delegate{PasteText();};
            copy=Button("复制译文","Copy result",false,100);copy.Click+=delegate{try{if(!String.IsNullOrWhiteSpace(translated.Text)){Clipboard.SetText(translated.Text);SetStatus(T("译文已复制","Translation copied"),false);}}catch{SetStatus(T("剪贴板忙，请重试","Clipboard is busy; try again"),true);}};
            checkConnection=Button("检测连接","Test Google",false,110);checkConnection.Click+=async delegate{await CheckGoogleConnection();};
            var compose=Button("中文提问","Write in Chinese",false,130);compose.Click+=async delegate{await OpenCompose();};
            actions.Controls.AddRange(new Control[]{toggle,paste,copy,checkConnection,compose});layout.Controls.Add(actions,0,2);
            var optionRows=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0)};
            optionRows.RowStyles.Add(new RowStyle(SizeType.Absolute,38));optionRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var modeRow=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0),WrapContents=false};
            var modeLabel=Label("翻译方式","Translate with");modeLabel.Margin=new Padding(0,7,8,0);
            engine=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=205,Margin=new Padding(0,3,10,0),BackColor=Color.White,FlatStyle=FlatStyle.Flat};
            engine.SelectedIndexChanged+=delegate{if(!changingOptions)ChangeEngine();};
            var packs=Button("离线语言包","Offline packs",false,160);packs.Click+=delegate{ManagePacks();};
            modeRow.Controls.AddRange(new Control[]{modeLabel,engine,packs});optionRows.Controls.Add(modeRow,0,0);
            var options=new FlowLayoutPanel {Dock=DockStyle.Fill,Margin=new Padding(0),WrapContents=false};
            pin=new CheckBox {Checked=Preferences.Pin,AutoSize=true,Margin=new Padding(0,7,14,0)};Bind(pin,"置顶","Pin on top");pin.CheckedChanged+=delegate{TopMost=pin.Checked;Preferences.Pin=pin.Checked;if(!preview)Preferences.Save();};
            targetLabel=Label("译成","Translate to");targetLabel.Margin=new Padding(0,7,8,0);
            direction=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=150,Margin=new Padding(0,3,10,0),BackColor=Color.White,FlatStyle=FlatStyle.Flat };
            PopulateTargets();
            direction.SelectedIndexChanged+=delegate{if(changingOptions||direction.SelectedItem==null)return;if(Preferences.Offline){var pair=direction.SelectedItem as LanguagePack;if(pair==null)return;Preferences.OfflinePair=pair.Pair;}else Preferences.Target=((LanguageChoice)direction.SelectedItem).Code;if(!preview)Preferences.Save();ChangeDirection();};
            autoLabel=Label("原文自动识别","Auto-detect source");autoLabel.Margin=new Padding(0,7,0,0);autoLabel.Font=new Font(Font.FontFamily,9F);
            options.Controls.AddRange(new Control[]{pin,targetLabel,direction,autoLabel});optionRows.Controls.Add(options,0,1);layout.Controls.Add(optionRows,0,3);
            translated=TextBox(14F,Theme.Ink);layout.Controls.Add(Theme.Card(translated,new Padding(0,4,0,4)),0,4);
            var originalLabel=Label("原文对照","Original selection");originalLabel.Dock=DockStyle.Fill;originalLabel.TextAlign=ContentAlignment.BottomLeft;layout.Controls.Add(originalLabel,0,5);
            original=TextBox(9.5F,Theme.Muted);layout.Controls.Add(Theme.Card(original,new Padding(0,4,0,4)),0,6);
            var footer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0)};
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute,30));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,38));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            status=new Label {Dock=DockStyle.Fill,ForeColor=Theme.BlueDark,Font=new Font(Font.FontFamily,9F)};
            googleStatus=new Label {Dock=DockStyle.Fill,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8.5F)};
            privacy=new Label{ForeColor=Theme.Muted};privacy.Dock=DockStyle.Fill;privacy.Font=new Font(Font.FontFamily,8.5F);
            var serviceRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
            serviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));serviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            googleStatus.Margin=new Padding(0,2,6,0);
            offlineRetry=Button("改用离线翻译","Use offline",true,142);offlineRetry.Height=34;offlineRetry.Margin=new Padding(0);offlineRetry.Visible=false;
            offlineRetry.Click+=delegate{RetryOffline();};serviceRow.Controls.Add(googleStatus,0,0);serviceRow.Controls.Add(offlineRetry,1,0);
            footer.Controls.Add(status,0,0);footer.Controls.Add(serviceRow,0,1);footer.Controls.Add(privacy,0,2);layout.Controls.Add(footer,0,7);Controls.Add(layout);
            watcher.SelectionReleased+=async delegate(IntPtr handle,Point point){await ReadSelection(handle);};
            lockTimer.Tick+=delegate{if(locked!=null&&!locked.IsAlive())UnlockSource(true);};
            Ui.LanguageChanged+=RefreshLanguage;
            RefreshLanguage();
            Shown+=delegate{if(!preview)lockTimer.Start();};
            FormClosing+=CloseSafely;
        }
        private Label Label(string zh,string en){var l=new Label{AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0)};Bind(l,zh,en);return l;}
        private void Bind(Control c,string zh,string en){Action update=delegate{c.Text=T(zh,en);};labels.Add(update);update();}
        private Button Button(string zh,string en,bool primary,int width){var b=Theme.Button(T(zh,en),primary);b.Width=width;b.Margin=new Padding(0,3,5,3);Bind(b,zh,en);return b;}
        private TextBox TextBox(float size,Color color){return new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=color,Font=new Font(Font.FontFamily,size),WordWrap=true,ScrollBars=ScrollBars.Vertical,MaxLength=0};}
        private string SelectedDirection{get{var pair=direction.SelectedItem as LanguagePack;if(Preferences.Offline)return pair==null?Preferences.OfflinePair:pair.Pair;var choice=direction.SelectedItem as LanguageChoice;return "auto:"+(choice==null?Preferences.Target:choice.Code);}}
        private void RefreshLanguage(){
            if(closing)return;
            foreach(var update in labels)update();
            Text=T("大肥鱼翻译 0.8.1","Whale Translator 0.8.1");
            RefreshEngineLabels();
            chooseSource.AccessibleName=T("选择要翻译的程序窗口","Choose a program window to translate");direction.AccessibleName=T("目标语言","Target language");
            translated.AccessibleName=T("译文","Translation");original.AccessibleName=T("所选原文","Selected text");
            if(latest==null)translated.Text=T("① 点上方「选择翻译窗口」\r\n在卡片中找到 Claude、浏览器或文档。\r\n\r\n② 点一下那张窗口卡片\r\n\r\n③ 回到该窗口划选文字\r\n松开鼠标，大肥鱼就会自动翻译。","1. Click Choose window above.\r\nFind Claude, your browser or a document.\r\n\r\n2. Click its window card.\r\n\r\n3. Select text in that window.\r\nRelease the mouse to translate automatically.");
            SetGoogleStatus(Preferences.Offline?PackStatus():T("可点「检测连接」检查服务","Use Test Google to check the service"),false);
            RefreshLockLabels();
            wait.RefreshMessage(TranslationWait());
            connectionWait.RefreshMessage(T("正在用固定示例检测…","Testing with a fixed sample…"));
            SetStatus(locked==null?T("先选择一个窗口 · 也可粘贴翻译","Choose a window first · Or use Paste"):active?T("仅翻译所选窗口里的划选文字","Only selections in your chosen window are translated"):T("划选已暂停；仍可粘贴翻译","Selection paused; Paste is still available"),false);
            if(CanRetryOffline()){
                SetGoogleStatus(T("这段在线翻译未完成，可用离线包重试","This online translation failed. You can retry with an offline pack"),true);
                SetStatus(T("原文已保留，点「改用离线翻译」继续","Original retained. Click Use offline to continue"),true);
            }
        }
        private string TranslationWait(){return Preferences.Offline?T("大肥鱼正在本机翻译…","Your whale is translating on this device…"):T("大肥鱼正在请 Google 翻译…","Your whale is asking Google to translate…");}
        private void RefreshEngineLabels(){
            changingOptions=true;engine.Items.Clear();engine.Items.Add(T("离线翻译","Offline translation"));engine.Items.Add(T("Google 在线翻译","Google Translate · Online"));engine.SelectedIndex=Preferences.Offline?0:1;changingOptions=false;
            engine.AccessibleName=T("翻译方式","Translation mode");
            subtitle.Text=Preferences.Offline?T("离线语言包 · 没网也能读","Offline language packs · Read anywhere"):T("Google 在线翻译 · 陪你读懂每一句","Google Translate · Read with your whale");
            privacy.Text=Preferences.Offline?T("本机离线翻译 · 文字不上传 · 不自动保存原文","Offline on this device · No upload · Text is not saved"):T("选中文字发送给 Google · 不自动保存原文","Selected text goes to Google · Text is not saved");
            checkConnection.Text=Preferences.Offline?T("检测离线库","Test offline"):T("检测连接","Test Google");
            if(targetLabel!=null)targetLabel.Text=Preferences.Offline?T("翻译方向","Direction"):T("译成","Translate to");
            if(autoLabel!=null)autoLabel.Visible=!Preferences.Offline;
            direction.Width=Preferences.Offline?300:150;
            if(Preferences.Offline)PopulateTargets();
        }
        private void PopulateTargets(){
            changingOptions=true;direction.Items.Clear();
            if(Preferences.Offline){
                foreach(var item in LanguagePack.InstalledPacks())direction.Items.Add(item);
                if(direction.Items.Count==0){direction.Items.Add(T("先下载离线语言包…","Download a language pack first…"));direction.SelectedIndex=0;direction.Enabled=false;}
                else{direction.Enabled=true;direction.SelectedIndex=0;foreach(LanguagePack item in direction.Items)if(item.Pair==Preferences.OfflinePair)direction.SelectedItem=item;Preferences.OfflinePair=((LanguagePack)direction.SelectedItem).Pair;}
            }else{direction.Enabled=true;foreach(var item in LanguageChoice.All)direction.Items.Add(item);direction.SelectedIndex=0;foreach(LanguageChoice item in direction.Items)if(item.Code==Preferences.Target)direction.SelectedItem=item;}
            changingOptions=false;
        }
        private string PackStatus(){return direction.SelectedItem is LanguagePack?T("语言包已安装 · 翻译无需联网","Pack installed · Translation needs no network"):T("点「离线语言包」，联网时按需下载","Open Offline packs to download while online");}
        private void ClearOfflineRetry(){pendingOfflineRetry=null;pendingOfflineEpoch=0;offlineRetry.Visible=false;}
        private bool CanRetryOffline(){return !closing&&!Preferences.Offline&&pendingOfflineRetry!=null&&Object.ReferenceEquals(pendingOfflineRetry,latest)&&pendingOfflineEpoch==epoch&&pendingOfflineRetry.Direction==SelectedDirection;}
        private bool RecordOfflineRetry(Snapshot request,int requestedEpoch,bool offlineMode,Exception error){
            var failure=error as TranslationFailure;
            if(request==null||offlineMode||Preferences.Offline||closing||requestedEpoch!=epoch||!Object.ReferenceEquals(request,latest)||request.Direction!=SelectedDirection||failure==null||(failure.Code!="rate_limited"&&failure.Code!="google_blocked"))return false;
            pendingOfflineRetry=request;pendingOfflineEpoch=epoch;offlineRetry.Visible=true;
            SetStatus(T("Google 暂不可用，点「改用离线翻译」继续这段","Google is unavailable. Use offline to translate this selection"),true);return true;
        }
        private void RetryOffline(){
            if(!CanRetryOffline()){ClearOfflineRetry();return;}
            var request=pendingOfflineRetry;bool resume=active;Pause();int requestEpoch=pendingOfflineEpoch;
            DialogResult result;LanguagePack selected;
            using(var dialog=new OfflineRetryForm(OfflineRetryForm.ForTarget(LanguagePack.InstalledPacks(),request.Direction))){dialog.TopMost=TopMost;result=dialog.ShowDialog(this);selected=dialog.SelectedPack;}
            if(closing)return;
            if(resume&&locked!=null&&locked.IsAlive())Start();
            if(result==DialogResult.OK)ApplyOfflineRetry(request,requestEpoch,selected);
            else if(result==DialogResult.Retry){ManagePacks();if(CanRetryOffline())SetStatus(T("安装后，点「改用离线翻译」继续刚才那段","After installing, click Use offline to retry that selection"),false);}
            else if(CanRetryOffline())SetStatus(T("原文已保留，可稍后改用离线翻译","Original retained. You can retry offline later"),false);
        }
        private bool ApplyOfflineRetry(Snapshot request,int requestEpoch,LanguagePack pair){
            if(!CanRetryOffline()||requestEpoch!=epoch||!Object.ReferenceEquals(request,pendingOfflineRetry)||pair==null)return false;
            LanguagePack installed=null;foreach(var candidate in OfflineRetryForm.ForTarget(LanguagePack.InstalledPacks(),request.Direction))if(candidate.Pair==pair.Pair){installed=candidate;break;}
            if(installed==null){SetStatus(T("对应语言包已不可用，请打开「离线语言包」检查","The matching pack is unavailable. Check Offline packs"),true);return false;}
            ClearOfflineRetry();CancelCapture();++epoch;worker.Dispose();wait.Stop();
            if(connectionWorker!=null){connectionWorker.Dispose();connectionWait.Stop();}
            Preferences.Offline=true;Preferences.OfflinePair=installed.Pair;PopulateTargets();RefreshEngineLabels();if(!preview)Preferences.Save();
            SetGoogleStatus(PackStatus(),false);
            // This explicit button authorizes retrying only this failed selection locally.
            AcceptText(request.Text,request.Conversation);return true;
        }
        private void ManagePacks(){
            bool resume=active;Pause();worker.Dispose();needsFreshSelection=true;
            using(var manager=new LanguagePacksForm(preview)){manager.TopMost=TopMost;manager.ShowDialog(this);}
            if(closing)return;PopulateTargets();if(!preview)Preferences.Save();SetGoogleStatus(Preferences.Offline?PackStatus():T("语言包已更新，可切换离线模式","Packs updated. You can switch to offline mode"),false);
            if(resume&&locked!=null&&locked.IsAlive())Start();
        }
        private void ChangeEngine(){
            if(engine.SelectedIndex<0||closing)return;
            ClearOfflineRetry();
            CancelCapture();++epoch;worker.Dispose();wait.Stop();needsFreshSelection=true;
            if(connectionWorker!=null){connectionWorker.Dispose();connectionWait.Stop();}
            Preferences.Offline=engine.SelectedIndex==0;PopulateTargets();if(!preview)Preferences.Save();RefreshEngineLabels();
            SetGoogleStatus(Preferences.Offline?PackStatus():T("在线模式 · 下次翻译会发送文字给 Google","Online mode · Your next translation sends text to Google"),false);
            SetStatus(T("已切换翻译方式，请重新划选或粘贴","Mode changed. Select new text or use Paste"),false);
        }
        private void RefreshLockLabels(){
            chooseSource.Text=locked==null?T("选择翻译窗口","Choose window"):T("换个窗口","Change window");chooseSource.Enabled=!pickingSource;
            clearSource.Visible=locked!=null;clearSource.Enabled=locked!=null&&!pickingSource;
            toggle.Text=active?T("暂停划选","Pause"):T("开启划选","Resume");toggle.Enabled=locked!=null&&!pickingSource;
            sourceName.Text=locked==null?T("还没选择翻译窗口","No window selected"):locked.FriendlyAppName+"\r\n"+locked.Title;
            lockInfo.Text=locked==null?T("点右侧按钮，再点一张窗口卡片。","Click Choose window, then a window card."):active?T("✓ 正在翻译这个窗口的划选文字","✓ Translating selections from this window"):T("已选好这个窗口 · 划选已暂停","This window is selected · Translation paused");
        }
        private bool BeginSourcePick(){
            if(closing||pickingSource)return false;
            sourceBeforePick=locked;sourceActiveBeforePick=active;pickingSource=true;
            Pause();RefreshLockLabels();return true;
        }
        private void ChooseSource(){
            if(!BeginSourcePick())return;
            SourceWindow choice=null;
            try{
                using(var picker=new SourceWindowPickerForm(locked)){
                    sourcePicker=picker;picker.TopMost=TopMost;
                    if(picker.ShowDialog(this)==DialogResult.OK)choice=picker.SelectedSource;
                }
            }finally{sourcePicker=null;CompleteSourcePick(choice);}
        }
        private void CompleteSourcePick(SourceWindow choice){
            if(!pickingSource)return;
            bool resume=sourceActiveBeforePick;var prior=sourceBeforePick;
            pickingSource=false;sourceBeforePick=null;sourceActiveBeforePick=false;
            if(closing)return;
            if(choice!=null&&ApplySource(choice))return;
            if(locked!=null&&!locked.IsAlive())UnlockSource(true);
            else if(locked!=null&&locked==prior&&resume)Start();
            else{RefreshLockLabels();SetStatus(locked==null?T("先选择一个窗口 · 也可粘贴翻译","Choose a window first · Or use Paste"):T("保持原来的窗口选择 · 划选已暂停","Previous window retained · Translation paused"),false);}
            if(choice!=null)SetStatus(T("这个窗口已关闭，请重新选择","That window has closed; choose another"),true);
        }
        private bool ApplySource(SourceWindow source){
            if(closing||source==null||!source.IsAlive())return false;
            ClearOfflineRetry();
            CancelCapture();++epoch;if(translating)worker.Dispose();wait.Stop();
            locked=source;UpdateSourceIcon(source);Start();return true;
        }
        private async void UpdateSourceIcon(SourceWindow source){
            int ticket=++sourceIconEpoch;
            var fallback=SystemIcons.Application.ToBitmap();var old=sourceIcon.Image;sourceIcon.Image=fallback;if(old!=null)old.Dispose();
            if(source==null||preview)return;
            Bitmap icon=null;
            try{icon=await Task.Run(delegate{return source.GetSmallIcon();});
                if(icon==null)return;
                if(closing||IsDisposed||ticket!=sourceIconEpoch||locked!=source){icon.Dispose();return;}
                old=sourceIcon.Image;sourceIcon.Image=icon;if(old!=null)old.Dispose();
            }catch{if(icon!=null)icon.Dispose();}
        }
        private void UnlockSource(bool closed){
            ClearOfflineRetry();
            active=false;watcher.Pause();CancelCapture();++epoch;if(translating)worker.Dispose();wait.Stop();locked=null;
            UpdateSourceIcon(null);RefreshLockLabels();
            SetStatus(closed?T("来源窗口已关闭，请重新选择","Source window closed; choose another window"):T("已取消选择，可重新选择翻译窗口","Selection cleared; choose a window to translate"),false);
        }
        private void Start(){if(closing||pickingSource)return;if(locked!=null&&!locked.IsAlive()){UnlockSource(true);return;}if(locked==null){RefreshLockLabels();SetStatus(T("请先选择翻译窗口","Choose a window first"),false);return;}active=true;watcher.Start();RefreshLockLabels();SetStatus(T("回到所选窗口，划选文字后松开就翻译","Select text in your chosen window; release to translate"),false);}
        private void Pause(){active=false;watcher.Pause();CancelCapture();++epoch;if(pendingOfflineRetry!=null)pendingOfflineEpoch=epoch;if(translating)worker.Dispose();wait.Stop();RefreshLockLabels();SetStatus(T("划选已暂停；仍可粘贴翻译","Selection paused; Paste is still available"),false);}
        private void CancelCapture(){++selectionEpoch;var old=captureCancellation;captureCancellation=null;if(old!=null)old.Cancel();}
        private async void CloseSafely(object sender,FormClosingEventArgs e){
            if(closing)return;
            closing=true;++epoch;++sourceIconEpoch;CancelCapture();watcher.Dispose();lockTimer.Dispose();wait.Dispose();connectionWait.Dispose();worker.Dispose();
            if(sourcePicker!=null&&!sourcePicker.IsDisposed)sourcePicker.Close();
            var icon=sourceIcon.Image;sourceIcon.Image=null;if(icon!=null)icon.Dispose();
            if(connectionWorker!=null)connectionWorker.Dispose();
            if(composeReaderProcess!=null)try{composeReaderProcess.Kill();}catch{}
            Ui.LanguageChanged-=RefreshLanguage;
            if(captureTransactions.Count>0){
                e.Cancel=true;
                // Let any already-issued Copy finish its bounded clipboard cleanup before exit.
                var pending=Task.WhenAll(captureTransactions.ToArray());
                await Task.WhenAny(pending,Task.Delay(3500));
                if(!IsDisposed)Close();
            }
        }
        private void ChangeDirection(){ClearOfflineRetry();CancelCapture();if(latest==null||closing||needsFreshSelection)return;++epoch;if(translating)worker.Dispose();wait.Stop();latest=new Snapshot{Conversation=latest.Conversation,MessageId=epoch.ToString(),Text=latest.Text,Direction=SelectedDirection};translated.Clear();done="";seen=latest.Key+latest.Text;TranslateWhenIdle(epoch);}
        private void SetStatus(string value,bool error){if(!closing){status.Text="●  "+value;status.ForeColor=error?Color.FromArgb(190,72,72):Theme.BlueDark;}}
        private void SetGoogleStatus(string value,bool error){if(!closing){googleStatus.Text=(Preferences.Offline?T("离线：","Offline: "):"Google: ")+value;googleStatus.ForeColor=error?Color.FromArgb(190,72,72):Theme.Muted;}}
        private async Task ReadSelection(IntPtr handle){
            if(!active||closing||pickingSource||locked==null||handle!=locked.Handle)return;
            if(!locked.IsAlive()){UnlockSource(true);return;}
            CancelCapture();var cancellation=new CancellationTokenSource();captureCancellation=cancellation;
            int requestEpoch=epoch,requestSelection=selectionEpoch;var sourceWindow=locked;
            Task<SelectionClipboardResult> captureTask=null;
            SetStatus(T("正在复制选中文字…","Copying your selection…"),false);
            try{
                captureTask=SelectionClipboard.CaptureAsync(handle,(uint)sourceWindow.ProcessId,cancellation.Token);
                captureTransactions.Add(captureTask);
                var data=await captureTask;
                if(!active||closing||requestEpoch!=epoch||requestSelection!=selectionEpoch||locked!=sourceWindow)return;
                SelectionDiagnostics.Save(new Dictionary<string,object>{{"status",data.Status},{"diagnostics",new Dictionary<string,object>{{"route","clipboard"},{"pointLength",data.Length},{"phase",data.RestoreStatus??"unknown"}}}},0);
                if(!data.Ok){SetStatus(CopyStatus(data.Status),false);return;}
                string source="selection:"+handle.ToInt64().ToString("X");
                if(!needsFreshSelection&&latest!=null&&latest.Conversation==source&&latest.Text==data.Text&&latest.Direction==SelectedDirection&&done==seen){SetStatus(T("这段文字的译文已经显示","This selection is already translated"),false);return;}
                AcceptText(data.Text,source);
            }catch{if(requestEpoch==epoch&&requestSelection==selectionEpoch&&!closing)SetStatus(T("复制未完成，请重新划选或使用粘贴翻译","Could not copy; select again or use Paste"),true);}
            finally{if(captureTask!=null)captureTransactions.Remove(captureTask);if(captureCancellation==cancellation)captureCancellation=null;cancellation.Dispose();}
        }
        private string CopyStatus(string reason){
            string message;
            switch(reason){
                case "password_denied":message=T("密码输入框不会读取","Password fields are not read");break;
                case "unsafe_target":message=T("这个窗口不支持自动复制，请手动复制后粘贴","Auto-copy is disabled for this window; copy manually and use Paste");break;
                case "text_too_long":message=T("选中文字超过 32,000 字符，请少选一些","Selection exceeds 32,000 characters; select less text");break;
                case "foreground_changed":case "not_foreground":message=T("窗口已切换；请回到所选窗口重新划选","Window changed; select again in your chosen window");break;
                case "modifier_held":case "mouse_down":message=T("请松开鼠标和快捷键后重新划选","Release the mouse and modifier keys, then select again");break;
                case "clipboard_changed":message=T("剪贴板同时被修改，请重新划选","Clipboard changed during copying; select again");break;
                case "copy_input_failed":message=T("系统未接受复制快捷键；请手动复制后粘贴","Windows did not accept Copy; copy manually and use Paste");break;
                default:message=T("没有复制到新文字；可重新划选或使用粘贴翻译","No new text was copied; select again or use Paste");break;
            }
            return message+(latest!=null?T(" · 保留上一段译文"," · Previous translation retained"):"");
        }
        private void AcceptText(string text,string source){
            ClearOfflineRetry();
            needsFreshSelection=false;
            ++epoch;if(translating)worker.Dispose();wait.Stop();
            latest=new Snapshot{Conversation=source,MessageId=epoch.ToString(),Text=text,Direction=SelectedDirection};
            original.Text=DisplayLines(text);translated.Clear();done="";seen=latest.Key+text;TranslateWhenIdle(epoch);
        }
        private void PasteText(){
            CancelCapture();
            try{string text=Clipboard.ContainsText()?Clipboard.GetText():"";if(String.IsNullOrWhiteSpace(text)){SetStatus(T("剪贴板里没有文字","No text in the clipboard"),true);return;}if(text.Length>32000){SetStatus(T("请分段粘贴，每次不超过 32,000 字符","Paste up to 32,000 characters at a time"),true);return;}AcceptText(text,"manual");}
            catch{SetStatus(T("剪贴板暂不可用，请重试","Clipboard unavailable; try again"),true);}
        }
        private async void TranslateWhenIdle(int requested){while(translating&&!closing&&requested==epoch)await Task.Delay(100);if(!closing&&requested==epoch)await TranslateLatest();}
        private async Task TranslateLatest(){
            if(translating||latest==null||closing)return;
            if(Preferences.Offline&&!(direction.SelectedItem is LanguagePack)){SetStatus(T("请先点「离线语言包」安装所需方向","Open Offline packs and install the direction you need"),true);return;}
            var request=latest;int requestedEpoch=epoch;bool offlineMode=Preferences.Offline;translating=true;
            wait.Start(TranslationWait(),delegate(string text){SetStatus(text,false);},Preferences.Offline);
            try{string result=await worker.Translate(request.Text,request.Direction);if(!closing&&ResultGate.CanShow(requestedEpoch,epoch,request,latest)){wait.Stop();translated.Text=DisplayLines(result);done=request.Key+request.Text;if(connectionWorker==null)SetGoogleStatus(T("上次翻译完成 · ","Last translation completed · ")+DateTime.Now.ToString("HH:mm:ss"),false);SetStatus(T("译文已更新 · ","Translation updated · ")+DateTime.Now.ToString("HH:mm:ss"),false);}}
            catch(Exception ex){worker.Dispose();wait.Stop();if(!closing&&requestedEpoch==epoch){SetStatus(Preferences.Offline?T("离线翻译未完成，可检测离线库后重试","Offline translation failed; test the offline library and retry"):T("翻译未完成，可检测连接后重新划选","Translation failed; test Google and select again"),true);SetGoogleStatus(ex is InvalidOperationException?ex.Message:T("翻译未完成，请重试","Translation failed; try again"),true);RecordOfflineRetry(request,requestedEpoch,offlineMode,ex);}}
            finally{translating=false;wait.Stop();}
        }
        private async Task CheckGoogleConnection(){
            if(connectionWorker!=null||closing)return;
            if(Preferences.Offline&&!(direction.SelectedItem is LanguagePack)){SetGoogleStatus(PackStatus(),true);return;}
            checkConnection.Enabled=false;var probe=new Worker();connectionWorker=probe;bool offlineMode=Preferences.Offline;string probeDirection=offlineMode?SelectedDirection:"auto:zh-CN";
            string sample="Please keep the original file.";
            if(offlineMode){string source=((LanguagePack)direction.SelectedItem).From;switch(source){case "zh":case "zt":sample="请保留原始文件。";break;case "ja":sample="元のファイルを保存してください。";break;case "ko":sample="원본 파일을 보관하세요.";break;case "de":sample="Bitte behalten Sie die Originaldatei.";break;case "fr":sample="Veuillez conserver le fichier original.";break;case "es":sample="Conserve el archivo original.";break;case "ru":sample="Сохраните исходный файл.";break;}}
            connectionWait.Start(T("正在用固定示例检测…","Testing with a fixed sample…"),delegate(string text){SetGoogleStatus(text,false);},offlineMode);
            try{string result=await probe.Translate(sample,probeDirection,offlineMode);connectionWait.Stop();if(offlineMode!=Preferences.Offline||(offlineMode&&probeDirection!=SelectedDirection))return;if(String.IsNullOrWhiteSpace(result)||(!offlineMode&&!System.Text.RegularExpressions.Regex.IsMatch(result,"[\\u3400-\\u9fff]")))throw new InvalidOperationException(T("服务返回异常，请重试","Unexpected service response; try again"));SetGoogleStatus((offlineMode?T("离线库可用 · ","Offline library ready · "):T("连接成功 · ","Connected · "))+DateTime.Now.ToString("HH:mm:ss"),false);}
            catch(Exception ex){connectionWait.Stop();if(offlineMode==Preferences.Offline)SetGoogleStatus(ex is InvalidOperationException?ex.Message:T("检测未完成，请重试","Test failed; try again"),true);}
            finally{probe.Dispose();connectionWorker=null;if(!closing)checkConnection.Enabled=true;}
        }
        private async Task OpenCompose(){
            WindowChoice choice=null;
            try{if(composeReaderProcess!=null)return;
                var p=new Process{StartInfo=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ClaudeReader.exe"),"--list"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};composeReaderProcess=p;
                try{p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(await Task.WhenAny(output,Task.Delay(6000))==output){var data=Json.Decode(await output);object rows;if(data.TryGetValue("windows",out rows))foreach(var row in (System.Collections.IEnumerable)rows){var item=row as Dictionary<string,object>;if(item!=null){choice=new WindowChoice{Handle=Json.Str(item,"handle"),Title=Json.Str(item,"title")};break;}}}}
                finally{composeReaderProcess=null;try{if(!p.HasExited)p.Kill();}catch{}p.Dispose();}
            }catch{}
            if(!closing)new ComposeForm(choice??new WindowChoice{Handle="0",Title=T("未连接 Claude","Claude not connected")}){TopMost=TopMost}.Show();
        }
        private static string DisplayLines(string text){return text.Replace("\r\n","\n").Replace("\n","\r\n");}
        public void RenderPreview(string path,bool retry=false){
            translated.Text=T("我会保留原始文件。\r\n\r\n导出前，请先检查预览。\r\n只翻译你选中的文字。","I will keep the original files.\r\n\r\nCheck the preview before exporting.\r\nOnly the text you selected is translated.");
            original.Text="Keep the original files.\r\n\r\nCheck the preview before exporting.\r\nTranslate only the text you selected.";
            SetStatus(T("固定示例 · 不是实时翻译结果","Fixed sample · Not a live translation"),false);
            if(retry){
                translated.Clear();latest=new Snapshot{Conversation="preview",MessageId="retry",Text=original.Text,Direction=SelectedDirection};
                var error=new TranslationFailure("rate_limited");SetGoogleStatus(error.Message,true);RecordOfflineRetry(latest,epoch,false,error);
            }
            TopMost=false;ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path);}Close();
        }
    }
}
