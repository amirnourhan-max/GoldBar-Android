using System.IO.Ports;

namespace GoldBar.Windows;

public sealed class SettingsDrawer : UserControl
{
    private static readonly Color Bg=Color.FromArgb(10,13,18), Card=Color.FromArgb(17,21,27), Card2=Color.FromArgb(23,28,36), Border=Color.FromArgb(50,57,69), Gold=Color.FromArgb(247,194,55), GoldSoft=Color.FromArgb(247,211,112), TextMain=Color.FromArgb(246,244,237), Muted=Color.FromArgb(151,160,176), Success=Color.FromArgb(92,218,135), Danger=Color.FromArgb(255,104,104);

    private AppSettings _source;
    private readonly TextBox _report=Input();
    private readonly ComboBox _model=Combo(),_port=Combo(),_baud=Combo(),_bits=Combo(),_parity=Combo(),_stop=Combo(),_flow=Combo(),_ending=Combo();
    private readonly CheckBox _auto=Check("خواندن خودکار فقط در حالت پایدار"),_up=Check("دریافت وزن با کلید ↑"),_send=Check("هنگام ↑ فرمان درخواست وزن ارسال شود"),_print=Check("دریافت PRINT ترازو"),_raw=Check("نمایش متن خام دریافتی");
    private readonly NumericUpDown _samples=Number(2,10),_tolerance=DecimalNumber(0.001m,5m,0.001m,3),_timeout=Number(500,10000),_before=Number(0,20),_after=Number(0,20),_minAfter=Number(0,20);
    private readonly TextBox _query=Input(),_decimal=Input();
    private readonly Label _test=L("آماده تست",9,Muted,true);

    public event Action? CloseRequested;
    public event Action<AppSettings>? SettingsSaved;

    public SettingsDrawer(AppSettings settings)
    {
        _source=settings;
        Dock=DockStyle.Fill;BackColor=Bg;RightToLeft=RightToLeft.Yes;Font=new Font("Segoe UI",9.5f);
        Build();Populate();LoadValues();
    }

    private void Build()
    {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Bg,Padding=Padding.Empty};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,74));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));

        var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Bg,Padding=new Padding(18,12,14,8)};head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));
        var titles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Bg};titles.RowStyles.Add(new RowStyle(SizeType.Absolute,34));titles.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var h=L("تنظیمات ترازو و گزارش",17,TextMain,true);h.Dock=DockStyle.Fill;h.TextAlign=ContentAlignment.MiddleRight;var s=L("RS-232، فیلتر پایداری و مسیر گزارش",8.8f,Muted,false);s.Dock=DockStyle.Fill;s.TextAlign=ContentAlignment.TopRight;titles.Controls.Add(h,0,0);titles.Controls.Add(s,0,1);
        var close=Secondary("×");close.Dock=DockStyle.Fill;close.Font=new Font("Segoe UI",16,FontStyle.Bold);close.Click+=(_,_)=>CloseRequested?.Invoke();head.Controls.Add(titles,0,0);head.Controls.Add(close,1,0);

        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Bg};
        var stack=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Bg,Padding=new Padding(14,6,14,18),RightToLeft=RightToLeft.Yes};scroll.Controls.Add(stack);
        stack.SizeChanged+=(_,_)=>{foreach(Control c in stack.Controls)c.Width=Math.Max(320,scroll.ClientSize.Width-42);};scroll.SizeChanged+=(_,_)=>{foreach(Control c in stack.Controls)c.Width=Math.Max(320,scroll.ClientSize.Width-42);};

        var report=Section("گزارش");
        var rr=new TableLayoutPanel{Dock=DockStyle.Top,Height=52,ColumnCount=2,BackColor=Card};rr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));rr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));_report.Dock=DockStyle.Fill;var browse=Secondary("انتخاب پوشه");browse.Dock=DockStyle.Fill;browse.Click+=(_,_)=>ChooseFolder();rr.Controls.Add(_report,0,0);rr.Controls.Add(browse,1,0);report.Controls.Add(rr);stack.Controls.Add(report);

        var serial=Section("ارتباط سریال");serial.Controls.Add(Grid(("مدل",_model),("COM Port",_port),("Baud Rate",_baud),("Data Bits",_bits),("Parity",_parity),("Stop Bits",_stop),("Flow Control",_flow)));stack.Controls.Add(serial);

        var behavior=Section("خواندن وزن");
        _auto.Dock=DockStyle.Top;_up.Dock=DockStyle.Top;_send.Dock=DockStyle.Top;_print.Dock=DockStyle.Top;_raw.Dock=DockStyle.Top;
        behavior.Controls.Add(_raw);behavior.Controls.Add(_print);behavior.Controls.Add(_send);behavior.Controls.Add(_up);behavior.Controls.Add(_auto);
        behavior.Controls.Add(Grid(("تعداد قرائت پایدار",_samples),("تلرانس پایداری (g)",_tolerance),("مهلت دریافت (ms)",_timeout)));
        behavior.Controls.Add(Grid(("فرمان درخواست",_query),("پایان فرمان",_ending)));
        var note=L("پیش‌فرض: Auto Read خاموش است. اگر روشن شود فقط وقتی چند قرائت پشت‌سرهم داخل تلرانس باشند وزن پذیرفته می‌شود.",8.8f,Muted,false);note.Dock=DockStyle.Top;note.Height=54;behavior.Controls.Add(note);
        _test.Dock=DockStyle.Top;_test.Height=32;behavior.Controls.Add(_test);var testBtn=Secondary("تست اتصال و دریافت وزن");testBtn.Dock=DockStyle.Top;testBtn.Height=44;testBtn.Click+=async(_,_)=>await TestScale(testBtn);behavior.Controls.Add(testBtn);stack.Controls.Add(behavior);

        var format=Section("قالب وزن دریافتی");format.Controls.Add(Grid(("ممیز",_decimal),("قبل ممیز",_before),("بعد ممیز",_after),("حداقل بعد ممیز",_minAfter)));stack.Controls.Add(format);

        var foot=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,BackColor=Bg,Padding=new Padding(14,10,14,12)};foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,118));foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        var reset=Secondary("بازنشانی");reset.Dock=DockStyle.Fill;reset.Click+=(_,_)=>ResetDefaults();var cancel=Secondary("بستن");cancel.Dock=DockStyle.Fill;cancel.Click+=(_,_)=>CloseRequested?.Invoke();var save=Primary("ذخیره تنظیمات");save.Dock=DockStyle.Fill;save.Click+=(_,_)=>Save();foot.Controls.Add(reset,0,0);foot.Controls.Add(cancel,1,0);foot.Controls.Add(save,2,0);

        root.Controls.Add(head,0,0);root.Controls.Add(scroll,0,1);root.Controls.Add(foot,0,2);Controls.Add(root);
    }

    private void ChooseFolder(){using var dlg=new FolderBrowserDialog{Description="پوشه گزارش‌های Gold Bar را انتخاب کن",UseDescriptionForTitle=true};if(Directory.Exists(_report.Text))dlg.SelectedPath=_report.Text;if(dlg.ShowDialog(this)==DialogResult.OK)_report.Text=dlg.SelectedPath;}

    private async Task TestScale(Button button)
    {
        button.Enabled=false;_test.Text="در حال اتصال…";_test.ForeColor=GoldSoft;using var reader=new ScaleReader();
        try{var cfg=BuildSettings();reader.ApplySettings(cfg,false);await reader.StartAsync();_test.Text=$"{cfg.PortName} باز شد؛ منتظر وزن…";var w=await reader.ReadNowAsync();_test.Text="وزن دریافتی: "+w.ToString("0.###")+" g";_test.ForeColor=Success;}
        catch(Exception ex){_test.Text="خطا: "+ex.Message;_test.ForeColor=Danger;}finally{button.Enabled=true;}
    }

    private void Save(){try{var next=BuildSettings();next.Save();_source=next;SettingsSaved?.Invoke(next);}catch(Exception ex){MessageBox.Show(this,"ذخیره تنظیمات انجام نشد:\n"+ex.Message,"تنظیمات",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    private void ResetDefaults(){_source=new AppSettings();LoadValues();_test.Text="مقادیر پیش‌فرض بارگذاری شد؛ برای اعمال، ذخیره را بزن.";_test.ForeColor=GoldSoft;}

    private AppSettings BuildSettings()=>new(){SettingsVersion=2,ReportFolder=_report.Text.Trim(),ScaleModel=Text(_model,"A&D"),PortName=Text(_port,"COM1"),BaudRate=Int(_baud,2400),DataBits=Int(_bits,7),Parity=Text(_parity,nameof(Parity.Even)),StopBits=Text(_stop,nameof(StopBits.Two)),Handshake=Text(_flow,nameof(Handshake.None)),DecimalSeparator=string.IsNullOrWhiteSpace(_decimal.Text)?".":_decimal.Text.Trim(),CharactersBeforeDecimal=(int)_before.Value,CharactersAfterDecimal=(int)_after.Value,MinimumAfterDecimal=(int)_minAfter.Value,ReceivePrintKey=_print.Checked,AutoRead=_auto.Checked,ReadOnUpArrow=_up.Checked,ShowRawText=_raw.Checked,StableAutoReadOnly=true,StableSampleCount=(int)_samples.Value,StableToleranceGrams=(double)_tolerance.Value,SendQueryOnUpArrow=_send.Checked,QueryCommand=_query.Text,QueryLineEnding=Text(_ending,"CRLF"),ReadTimeoutMs=(int)_timeout.Value};

    private void Populate(){_model.Items.AddRange(new object[]{"A&D","Custom / Generic"});foreach(var p in SerialPort.GetPortNames().OrderBy(x=>x))_port.Items.Add(p);if(!_port.Items.Contains("COM1"))_port.Items.Add("COM1");_baud.Items.AddRange(new object[]{"1200","2400","4800","9600","19200","38400","57600","115200"});_bits.Items.AddRange(new object[]{"7","8"});_parity.Items.AddRange(Enum.GetNames<Parity>());_stop.Items.AddRange(new object[]{nameof(StopBits.One),nameof(StopBits.OnePointFive),nameof(StopBits.Two)});_flow.Items.AddRange(Enum.GetNames<Handshake>());_ending.Items.AddRange(new object[]{"CRLF","CR","LF","None"});}
    private void LoadValues(){_report.Text=_source.ReportFolder;Select(_model,_source.ScaleModel);Select(_port,_source.PortName);Select(_baud,_source.BaudRate.ToString());Select(_bits,_source.DataBits.ToString());Select(_parity,_source.Parity);Select(_stop,_source.StopBits);Select(_flow,_source.Handshake);_auto.Checked=_source.AutoRead;_up.Checked=_source.ReadOnUpArrow;_send.Checked=_source.SendQueryOnUpArrow;_print.Checked=_source.ReceivePrintKey;_raw.Checked=_source.ShowRawText;_samples.Value=Math.Clamp(_source.StableSampleCount,(int)_samples.Minimum,(int)_samples.Maximum);_tolerance.Value=(decimal)Math.Clamp(_source.StableToleranceGrams,(double)_tolerance.Minimum,(double)_tolerance.Maximum);_timeout.Value=Math.Clamp(_source.ReadTimeoutMs,(int)_timeout.Minimum,(int)_timeout.Maximum);_query.Text=_source.QueryCommand;Select(_ending,_source.QueryLineEnding);_decimal.Text=_source.DecimalSeparator;_before.Value=Math.Clamp(_source.CharactersBeforeDecimal,(int)_before.Minimum,(int)_before.Maximum);_after.Value=Math.Clamp(_source.CharactersAfterDecimal,(int)_after.Minimum,(int)_after.Maximum);_minAfter.Value=Math.Clamp(_source.MinimumAfterDecimal,(int)_minAfter.Minimum,(int)_minAfter.Maximum);}

    private static RoundedPanel Section(string title){var p=new RoundedPanel{Width=380,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=Card,BorderColor=Border,Radius=16,Padding=new Padding(14),Margin=new Padding(0,0,0,12)};var h=L(title,11.5f,TextMain,true);h.Dock=DockStyle.Top;h.Height=34;p.Controls.Add(h);return p;}
    private static TableLayoutPanel Grid(params (string Label,Control C)[] fields){var g=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,BackColor=Card,RightToLeft=RightToLeft.Yes,Margin=new Padding(0,5,0,8)};g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));foreach(var f in fields){var host=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,BackColor=Card,Margin=new Padding(4)};var l=L(f.Label,8.2f,Muted,false);l.Dock=DockStyle.Top;l.Height=22;f.C.Dock=DockStyle.Top;f.C.Height=36;host.Controls.Add(l);host.Controls.Add(f.C);g.Controls.Add(host);}return g;}
    private static TextBox Input()=>new(){BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",10.2f),RightToLeft=RightToLeft.No,Height=36};
    private static ComboBox Combo()=>new(){BackColor=Card2,ForeColor=TextMain,FlatStyle=FlatStyle.Flat,DropDownStyle=ComboBoxStyle.DropDown,Font=new Font("Segoe UI",9.8f),RightToLeft=RightToLeft.No,Height=36};
    private static NumericUpDown Number(int min,int max)=>new(){Minimum=min,Maximum=max,BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.8f),TextAlign=HorizontalAlignment.Center,Height=36};
    private static NumericUpDown DecimalNumber(decimal min,decimal max,decimal inc,int decimals)=>new(){Minimum=min,Maximum=max,Increment=inc,DecimalPlaces=decimals,BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.8f),TextAlign=HorizontalAlignment.Center,Height=36};
    private static CheckBox Check(string text)=>new(){Text=text,ForeColor=TextMain,AutoSize=false,Height=36,Padding=new Padding(4,5,4,5),Font=new Font("Segoe UI",9f)};
    private static Label L(string text,float size,Color color,bool bold)=>new(){Text=text,AutoSize=false,ForeColor=color,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),RightToLeft=RightToLeft.Yes,TextAlign=ContentAlignment.MiddleRight};
    private static RoundButton Primary(string t)=>Btn(t,Gold,Color.FromArgb(25,18,2),Gold);private static RoundButton Secondary(string t)=>Btn(t,Card2,GoldSoft,Border);private static RoundButton Btn(string t,Color bg,Color fg,Color border){var b=new RoundButton{Text=t,Radius=11,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Segoe UI",9.3f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(4)};b.FlatAppearance.BorderColor=border;b.FlatAppearance.BorderSize=1;return b;}
    private static void Select(ComboBox c,string v){var i=c.FindStringExact(v);if(i>=0)c.SelectedIndex=i;else c.Text=v;}private static string Text(ComboBox c,string f)=>string.IsNullOrWhiteSpace(c.Text)?f:c.Text.Trim();private static int Int(ComboBox c,int f)=>int.TryParse(c.Text,out var v)?v:f;
}
