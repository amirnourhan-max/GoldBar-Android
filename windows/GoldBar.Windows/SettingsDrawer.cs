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
        Dock=DockStyle.Fill;
        BackColor=Bg;
        RightToLeft=RightToLeft.Yes;
        Font=new Font("Segoe UI",9.5f);
        Build();
        Populate();
        LoadValues();
    }

    private void Build()
    {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Bg,Margin=Padding.Empty,Padding=Padding.Empty};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
        root.Controls.Add(BuildHeader(),0,0);
        root.Controls.Add(BuildBody(),0,1);
        root.Controls.Add(BuildFooter(),0,2);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Bg,Padding=new Padding(16,10,14,8)};
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));
        var titles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Bg};
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var h=L("تنظیمات ترازو و گزارش",17,TextMain,true);h.Dock=DockStyle.Fill;h.TextAlign=ContentAlignment.MiddleRight;
        var s=L("RS-232، فیلتر پایداری و مسیر گزارش",8.8f,Muted,false);s.Dock=DockStyle.Fill;s.TextAlign=ContentAlignment.TopRight;
        titles.Controls.Add(h,0,0);titles.Controls.Add(s,0,1);
        var close=Secondary("×");close.Dock=DockStyle.Fill;close.Font=new Font("Segoe UI",16,FontStyle.Bold);close.Click+=(_,_)=>CloseRequested?.Invoke();
        head.Controls.Add(titles,0,0);head.Controls.Add(close,1,0);
        return head;
    }

    private Control BuildBody()
    {
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Bg,Padding=Padding.Empty};
        var content=new TableLayoutPanel{Dock=DockStyle.Top,Height=1110,ColumnCount=1,RowCount=4,BackColor=Bg,Padding=new Padding(12,4,12,12),Margin=Padding.Empty};
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute,138));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute,308));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute,448));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute,190));
        content.Controls.Add(BuildReportCard(),0,0);
        content.Controls.Add(BuildSerialCard(),0,1);
        content.Controls.Add(BuildBehaviorCard(),0,2);
        content.Controls.Add(BuildFormatCard(),0,3);
        scroll.Controls.Add(content);
        return scroll;
    }

    private Control BuildReportCard()
    {
        var card=CardShell("گزارش",3);
        var layout=(TableLayoutPanel)card.Controls[0];
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var pathRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Card,Margin=Padding.Empty};
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
        _report.Dock=DockStyle.Fill;
        var browse=Secondary("انتخاب پوشه");browse.Dock=DockStyle.Fill;browse.Click+=(_,_)=>ChooseFolder();
        pathRow.Controls.Add(_report,0,0);pathRow.Controls.Add(browse,1,0);
        var note=L("مسیر فقط ذخیره می‌شود؛ ساخت پوشه هنگام ذخیره گزارش بررسی خواهد شد.",8.3f,Muted,false);note.Dock=DockStyle.Fill;note.TextAlign=ContentAlignment.MiddleRight;
        layout.Controls.Add(pathRow,0,1);layout.Controls.Add(note,0,2);
        return card;
    }

    private Control BuildSerialCard()
    {
        var card=CardShell("ارتباط سریال",2);
        var layout=(TableLayoutPanel)card.Controls[0];
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.Controls.Add(FieldMatrix(new[]{("مدل ترازو",(Control)_model),("COM Port",_port),("Baud Rate",_baud),("Data Bits",_bits),("Parity",_parity),("Stop Bits",_stop),("Flow Control",_flow)}),0,1);
        return card;
    }

    private Control BuildBehaviorCard()
    {
        var card=CardShell("رفتار دریافت وزن",5);
        var layout=(TableLayoutPanel)card.Controls[0];
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,178));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,102));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,82));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        var checks=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,BackColor=Card,Padding=Padding.Empty};
        foreach(var c in new[]{_auto,_up,_send,_print,_raw}){c.Dock=DockStyle.Fill;checks.Controls.Add(c);}
        layout.Controls.Add(checks,0,1);
        layout.Controls.Add(FieldMatrix(new[]{("تعداد قرائت پایدار",(Control)_samples),("تلرانس پایداری (g)",_tolerance),("مهلت دریافت (ms)",_timeout)}),0,2);
        layout.Controls.Add(FieldMatrix(new[]{("فرمان درخواست",(Control)_query),("پایان فرمان",_ending)}),0,3);
        var testHost=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Card};
        testHost.RowStyles.Add(new RowStyle(SizeType.Absolute,30));testHost.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _test.Dock=DockStyle.Fill;_test.TextAlign=ContentAlignment.MiddleRight;
        var btn=Secondary("تست اتصال و دریافت وزن");btn.Dock=DockStyle.Fill;btn.Click+=async(_,_)=>await TestScale(btn);
        testHost.Controls.Add(_test,0,0);testHost.Controls.Add(btn,0,1);
        layout.Controls.Add(testHost,0,4);
        return card;
    }

    private Control BuildFormatCard()
    {
        var card=CardShell("قالب وزن دریافتی",2);
        var layout=(TableLayoutPanel)card.Controls[0];
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.Controls.Add(FieldMatrix(new[]{("ممیز",(Control)_decimal),("قبل ممیز",_before),("بعد ممیز",_after),("حداقل بعد ممیز",_minAfter)}),0,1);
        return card;
    }

    private Control BuildFooter()
    {
        var foot=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,BackColor=Bg,Padding=new Padding(12,10,12,12)};
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,116));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        var reset=Secondary("بازنشانی");reset.Dock=DockStyle.Fill;reset.Click+=(_,_)=>ResetDefaults();
        var close=Secondary("بستن");close.Dock=DockStyle.Fill;close.Click+=(_,_)=>CloseRequested?.Invoke();
        var save=Primary("ذخیره تنظیمات");save.Dock=DockStyle.Fill;save.Click+=(_,_)=>Save();
        foot.Controls.Add(reset,0,0);foot.Controls.Add(close,1,0);foot.Controls.Add(save,2,0);
        return foot;
    }

    private static RoundedPanel CardShell(string title,int rows)
    {
        var card=new RoundedPanel{Dock=DockStyle.Fill,Margin=new Padding(2,6,2,6),Padding=new Padding(12),Radius=15,BackColor=Card,BorderColor=Border};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows,BackColor=Card,Margin=Padding.Empty,Padding=Padding.Empty};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var h=L(title,11.5f,TextMain,true);h.Dock=DockStyle.Fill;h.TextAlign=ContentAlignment.MiddleRight;
        layout.Controls.Add(h,0,0);
        card.Controls.Add(layout);
        return card;
    }

    private static TableLayoutPanel FieldMatrix((string Label,Control Control)[] fields)
    {
        var rows=(int)Math.Ceiling(fields.Length/2.0);
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=rows,BackColor=Card,Margin=Padding.Empty,Padding=new Padding(0,2,0,0)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        for(var r=0;r<rows;r++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
        for(var i=0;i<fields.Length;i++)grid.Controls.Add(FieldHost(fields[i].Label,fields[i].Control),i%2,i/2);
        return grid;
    }

    private static Control FieldHost(string label,Control control)
    {
        var host=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Card,Margin=new Padding(4,3,4,3)};
        host.RowStyles.Add(new RowStyle(SizeType.Absolute,20));host.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var l=L(label,8.1f,Muted,false);l.Dock=DockStyle.Fill;l.TextAlign=ContentAlignment.MiddleRight;
        control.Dock=DockStyle.Fill;
        host.Controls.Add(l,0,0);host.Controls.Add(control,0,1);
        return host;
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

    private static TextBox Input()=>new(){BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",10f),RightToLeft=RightToLeft.No,Height=36};
    private static ComboBox Combo()=>new(){BackColor=Card2,ForeColor=TextMain,FlatStyle=FlatStyle.Flat,DropDownStyle=ComboBoxStyle.DropDown,Font=new Font("Segoe UI",9.6f),RightToLeft=RightToLeft.No,Height=36};
    private static NumericUpDown Number(int min,int max)=>new(){Minimum=min,Maximum=max,BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.6f),TextAlign=HorizontalAlignment.Center,Height=36};
    private static NumericUpDown DecimalNumber(decimal min,decimal max,decimal inc,int decimals)=>new(){Minimum=min,Maximum=max,Increment=inc,DecimalPlaces=decimals,BackColor=Card2,ForeColor=TextMain,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",9.6f),TextAlign=HorizontalAlignment.Center,Height=36};
    private static CheckBox Check(string text)=>new(){Text=text,ForeColor=TextMain,AutoSize=false,Padding=new Padding(4,3,4,3),Font=new Font("Segoe UI",8.8f),TextAlign=ContentAlignment.MiddleRight};
    private static Label L(string text,float size,Color color,bool bold)=>new(){Text=text,AutoSize=false,ForeColor=color,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),RightToLeft=RightToLeft.Yes,TextAlign=ContentAlignment.MiddleRight};
    private static RoundButton Primary(string t)=>Btn(t,Gold,Color.FromArgb(25,18,2),Gold);private static RoundButton Secondary(string t)=>Btn(t,Card2,GoldSoft,Border);private static RoundButton Btn(string t,Color bg,Color fg,Color border){var b=new RoundButton{Text=t,Radius=11,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Segoe UI",9.2f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(3)};b.FlatAppearance.BorderColor=border;b.FlatAppearance.BorderSize=1;return b;}
    private static void Select(ComboBox c,string v){var i=c.FindStringExact(v);if(i>=0)c.SelectedIndex=i;else c.Text=v;}private static string Text(ComboBox c,string f)=>string.IsNullOrWhiteSpace(c.Text)?f:c.Text.Trim();private static int Int(ComboBox c,int f)=>int.TryParse(c.Text,out var v)?v:f;
}
