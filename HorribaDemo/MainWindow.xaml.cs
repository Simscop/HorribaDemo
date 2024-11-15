using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Windows;
using JYCCDLib;
using JYCONFIGBROWSERCOMPONENTLib;
using JYMONOLib;
using JYSYSTEMLIBLib;
using Serilog;

namespace HorribaDemo;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

// load devices
partial class MainWindow
{
    private readonly JYConfigBrowerInterface _brower = new();

    private readonly JYMCDClass _ccd = new();

    private readonly MonochromatorClass _mono = new();

    bool LoadInit()
    {
        PinLog();
        _brower.Load();

        var mono = LoadMonos();
        if (mono is null) return false;

        var ccd = LoadCcd();
        if (ccd is null) return false;

        _ccd.Uniqueid = ccd.Value.id;

        var temperature = _ccd.CurrentTemperature;
        Log.Information("Get ccd temperature: {t}\n" +
                        "\t\tNOTE: Temperature will loop get to update.", temperature);

        return true;
    }

    private (string id, string name)? LoadMonos()
    {
        PinLog();

        var id = _brower.GetFirstMono(out var name);
        var count = 0;

        while (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
        {
            Log.Information("Append a mono device [{id}]:[{name}]", id, name);

            id = _brower.GetNextMono(out name);
            count++;
        }

        Log.Information("Current mono count:{count}", count);

        if (count < 0)
        {
            Log.Error("Mono count less than 1");
            return null;
        }

        Log.Information("Current select the first mono");

        id = _brower.GetFirstMono(out name);

        return (id, name);
    }

    public (string id, string name)? LoadCcd()
    {
        PinLog();

        var id = _brower.GetFirstCCD(out var name);
        var count = 0;

        while (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
        {
            Log.Information("Append a ccd device [{id}]:[{name}]", id, name);

            id = _brower.GetNextCCD(out name);
            count++;
        }

        Log.Information("Current ccd count:{count}", count);

        if (count < 0)
        {
            Log.Error("Ccd count less than 1");
            return null;
        }

        Log.Information("Current select the first ccd");

        id = _brower.GetFirstCCD(out name);

        return (id, name);
    }
}

// ccd connect
partial class MainWindow
{
    void ConnectCcd()
    {
        PinLog();

        // note: the ccd Uniqueid has been set in LoadInit method.

        _ccd._IJYDeviceReqdEvents_Event_Initialize += OnCcdInitialized;
        _ccd.OperationStatus += OnCcdOperationStatusChanged;
        _ccd.Update += OnCcdUpdated;

        _ccd.Load();

        try
        {
            _ccd.OpenCommunications();
        }
        catch (Exception e)
        {
            Log.Error("Ccd Hardware Not Detected");
            Log.Error(e.Message);
        }
    }

    private void OnCcdUpdated(int updateType, JYSYSTEMLIBLib.IJYEventInfo eventInfo)
    {
        PinLog($"OnCcdUpdated,UpdateType -> {updateType}");

        // when 100 goon
        if (updateType != 100)
            return;

        IJYResultsObject result;
        try
        {
            result = eventInfo.GetResult();
        }
        catch (Exception e)
        {
            Log.Error("Failed During Acquistion: Get Result Obj Failed");
            Log.Error(e.Message);
            return;
        }

        Log.Information("Acquisition Completed");

        IJYDataObject data;
        try
        {
            data = result.GetFirstDataObject();

            if (data is null)
                throw new NullReferenceException("GetFirstDataObject() Failed: NULL OBJECT");

            data.GetDataAsArray(out var obj, true, 1);

            Log.Information("Data Update received");

            // todo 这里之后就是数据处理的部分了，这里拿到的是原始的data数据
        }
        catch (Exception e)
        {
            Log.Error("");
            Log.Error(e.Message);
            return;
        }
    }

    private void OnCcdOperationStatusChanged(int status, JYSYSTEMLIBLib.IJYEventInfo eventInfo)
    {
        PinLog("OnCcdOperationStatusChanged");
    }

    private int _ccdGain = 0;
    private jyCCDDataType _ccdMode = jyCCDDataType.JYMCD_ACQ_FORMAT_IMAGE;
    private int _ccdAdc = 0;

    private void OnCcdInitialized(int status, JYSYSTEMLIBLib.IJYEventInfo eventInfo)
    {
        PinLog("OnCcdInitialized");

        Log.Information("Status {s}", status);
        Log.Information("Info {info}", eventInfo.Description);

        _ccd.GetChipSize(out var xPixels, out var yPixels);
        Log.Information("Ccd GetChipSize: ({xPixels}.{yPixels})", xPixels, yPixels);

        // GetDefaultUnits && IntegrationTime
        try
        {
            _ccd.GetDefaultUnits(jyUnitsType.jyutTime, out var units, out var oUnits);
            Log.Information("Units -> {u}", oUnits.ToString());

            var intergration = _ccd.IntegrationTime;
            Log.Information("Intergration -> {i}", intergration);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }

        // Area Info
        var mode = jyCCDDataType.JYMCD_ACQ_FORMAT_SCAN;
        mode = jyCCDDataType.JYMCD_ACQ_FORMAT_IMAGE;

        var xStart = 1;
        var yStart = 1;
        var xSize = xPixels;
        var ySize = yPixels;
        var xBin = 1;
        var xEnd = xPixels;
        var yEnd = yPixels;

        // Gain
        var gain = _ccd.Gain;
        Log.Information("Gain -> {gain}", gain);

        var gainToken = _ccd.GetFirstGain(out var gainName);
        if (gainToken == -1)
            Log.Information("The Current Device Has NOT Gain Mode!");
        else
        {
            while (gainToken != -1)
            {
                Log.Information("GAIN : {gainToken}\t->{name}", gainToken, gainName);

                gainToken = _ccd.GetNextGain(out gainName);
            }
        }

        // ADC
        var adc = _ccd.CurrentADC;
        Log.Information("Adc -> {adc}", adc);

        var adcToken = _ccd.GetFirstADC(out var adcName);
        _ccdAdc = adcToken;

        if (adcToken == -1)
            Log.Information("The Current Device Has NOT Adc Mode!");
        else
        {
            while (adcToken != -1)
            {
                Log.Information("GAIN : {adcToken}\t->{name}", adcToken, adcName);

                adcToken = _ccd.GetNextADC(out adcName);
            }
        }
    }
}

// ccd initialize
partial class MainWindow
{
    void InitializeCcd()
    {
        PinLog();

        try
        {
            _ccd.Initialize();
            //_ccd.Initialize(true);
        }
        catch (Exception e)
        {
            Log.Error("Ccd Hardware Not Detected");
            Log.Error(e.Message);
        }
    }
}

// ccd set params
partial class MainWindow
{
    public void SetCcdParams()
    {
        Log.Information("Try Set Gain");
        _ccd.Gain = _ccdGain;

        Log.Information("Try Set ADC");
        try
        {
            _ccd.SelectADC((jyADCType)_ccdAdc);
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }

        Log.Information("Try Set Intergration");
        _ccd.IntegrationTime = 10;

        Log.Information("Try Set DataType");
        _ccd.DefineAcquisitionFormat(jyCCDDataType.JYMCD_ACQ_FORMAT_SCAN, 1);

        Log.Information("Try Set Aera");
        _ccd.DefineArea(1, 1, 1, 1024, 256, 1, 256);

        if (_ccd.ReadyForAcquisition) return;

        Log.Warning("ReadyForAcquisition Failed.");
        return;
    }
}

// log
partial class MainWindow
{
    public static void PinLog(string msg = "", [CallerMemberName] string method = "")
        => Log.Verbose("[Call Method] {method} {msg}", method, msg);

    public static void ExitLog(string msg = "", [CallerMemberName] string method = "")
        => Log.Verbose("[Exit Method] {method} {msg}", method, msg);
}

// event
partial class MainWindow
{
    private void OnInitializeClicked(object sender, RoutedEventArgs e)
        => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ExitLog($"ConnectCcd -> {LoadInit()}");
        });

    private void OnCcdConnectionClicked(object sender, RoutedEventArgs e)
        => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ConnectCcd();
            ExitLog("ConnectCcd");

        });

    private void OnCcdInitializedClicked(object sender, RoutedEventArgs e)
        => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            InitializeCcd();
            ExitLog("InitializeCcd");
        });


    private void OnCcdParasSetting(object sender, RoutedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SetCcdParams();
            ExitLog("SetCcdParams");
        });
    }

    private void OnAcquisitionClicked(object sender, RoutedEventArgs e)
    {
        PinLog();

        _ccd.DoAcquisition(true);
        ExitLog("PinLog");
    }
}