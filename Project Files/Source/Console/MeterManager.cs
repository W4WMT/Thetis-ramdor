﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

//directX
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
//

namespace Thetis
{
    // this enum is similar to MeterRXMode & MeterTXMode
    public enum Reading
    {
        NONE = -1,
        SIGNAL_STRENGTH = 0,
        AVG_SIGNAL_STRENGTH,
        ADC_REAL,
        ADC_IMAG,
        AGC_GAIN,
        MIC,
        PWR,
        ALC,
        EQ,
        LEVELER,
        COMP,
        CPDR,
        ALC_G,
        LVL_G,
        MIC_PK,
        ALC_PK,
        EQ_PK,
        LEVELER_PK,
        COMP_PK,
        CPDR_PK,
        CFC_PK,
        CFC_G,
        //additional to MeterRXMode & MeterTXMode
        REVERSE_PWR,
        SWR,
        ADC2_REAL,
        ADC2_IMAG,
        //pa
        DRIVE_FWD_ADC,
        FWD_ADC,
        REV_ADC,
        DRIVE_PWR,
        PA_FWD_PWR,
        PA_REV_PWR,
        CAL_FWD_PWR,
        REV_VOLT,
        FWD_VOLT,
        LAST
    }

    internal static class MeterManager
    {
        #region MeterManager
        private enum MeterType
        {
            NONE = 0,
            RX = 1,
            MOX = 2,
            TUNE = 4,
            TWOTONE = 8
        }
        // member variables
        private static Console _console;
        private static bool _delegatesAdded;
        private static Dictionary<int, clsReadings> _readings;
        private static Dictionary<string, clsMeter> _meters;
        private static Thread _meterThread;
        private static bool _meterThreadRunning;
        private static object _readingsLock = new object();
        private static bool _mox;
        //
        private static DXRenderer _rx1Renderer;
        private static DXRenderer _rx2Renderer;
        //

        static MeterManager()
        {
            // static constructor
            _rx1Renderer = null;
            _rx2Renderer = null;
            _delegatesAdded = false;
            _console = null;
            _readings = new Dictionary<int, clsReadings>();
            _meters = new Dictionary<string, clsMeter>();

            _mox = false;

            _readings.Add(1, new clsReadings());
            _readings.Add(2, new clsReadings());

            // test
            clsMeter cm = new clsMeter(1, "test1meter", (int)MeterType.RX, 1f, 1f);
            _meters.Add(cm.Name, cm);

            //cm = new clsMeter(2, "test2meter", (int)MeterType.RX, 1f, 1f);
            //_meters.Add(cm.Name, cm);

            cm = new clsMeter(1, "test3meter", (int)MeterType.MOX | (int)MeterType.TUNE | (int)MeterType.TWOTONE, 1f, 1f);
            _meters.Add(cm.Name, cm);
            //

            _meterThreadRunning = false;
            _meterThread = new Thread(new ThreadStart(UpdateMeters))
            {
                Name = "Multimeter Thread",
                Priority = ThreadPriority.Lowest,
                IsBackground = true
            };
            _meterThread.Start();
        }

        private static void UpdateMeters()
        {
            _meterThreadRunning = true;
            while (_meterThreadRunning)
            {
                // udpate any meter
                foreach (KeyValuePair<string, clsMeter> kvp in _meters)
                {
                    List<Reading> readingsUsed = new List<Reading>();

                    kvp.Value.Update(ref readingsUsed);

                    lock (_readingsLock)
                    {
                        // use/invalidate any readings that have been used, so that they can be updated
                        foreach (Reading rt in readingsUsed)
                        {
                            _readings[kvp.Value.RX].UseReading(rt);
                        }
                    }
                }

                Thread.Sleep(overallUpdateInterval());
            }
        }
        public static void Init(Console c, Control rx1, Control rx2, string sImagePath = "")
        {
            _console = c;
            addDelegates();

            if (rx1 != null)
                _rx1Renderer = new DXRenderer(1, rx1, _console, sImagePath);
            if (rx2 != null)
                _rx2Renderer = new DXRenderer(2, rx2, _console, sImagePath);
        }
        public static void Shutdown()
        {
            if (_rx1Renderer != null) _rx1Renderer.ShutdownDX();
            if (_rx2Renderer != null) _rx2Renderer.ShutdownDX();

            removeDelegates();
            
            _meterThreadRunning = false;
            if (_meterThread != null && _meterThread.IsAlive) _meterThread.Join();
        }
        private static void addDelegates()
        {
            if (_delegatesAdded) return;

            _console.MeterReadingsChangedHandlers += OnMeterReadings;
            _console.MoxChangeHandlers += OnMox;
            _delegatesAdded = true;
        }
        private static void removeDelegates()
        {
            if (!_delegatesAdded) return;

            _console.MeterReadingsChangedHandlers -= OnMeterReadings;
            _console.MoxChangeHandlers -= OnMox;

            _delegatesAdded = false;
        }
        private static float GetReading(int rx, Reading rt, bool bUseReading = false)
        {
            lock (_readingsLock)
            {
                return _readings[rx].GetReading(rt, bUseReading);
            }
        }
        private static void setReading(int rx, Reading rt, ref Dictionary<Reading, float> readings)
        {
            if (_readings[rx].RequiresUpdate(rt)) _readings[rx].SetReading(rt, readings[rt]);
        }
        private static void OnMeterReadings(int rx, bool mox, ref Dictionary<Reading, float> readings)
        {
            lock (_readingsLock)
            {
                //
                if (!mox)
                {
                    setReading(rx, Reading.SIGNAL_STRENGTH, ref readings);
                    setReading(rx, Reading.AVG_SIGNAL_STRENGTH, ref readings);
                    setReading(rx, Reading.ADC_REAL, ref readings);
                    setReading(rx, Reading.ADC_IMAG, ref readings);
                }
                else
                {
                    setReading(rx, Reading.MIC_PK, ref readings);
                    setReading(rx, Reading.EQ_PK, ref readings);
                    setReading(rx, Reading.LEVELER_PK, ref readings);
                    setReading(rx, Reading.LVL_G, ref readings);
                    setReading(rx, Reading.CFC_PK, ref readings);
                    setReading(rx, Reading.CFC_G, ref readings);
                    setReading(rx, Reading.CPDR_PK, ref readings);
                    setReading(rx, Reading.CPDR, ref readings);

                    setReading(rx, Reading.ALC_PK, ref readings);
                    setReading(rx, Reading.ALC, ref readings);
                    setReading(rx, Reading.ALC_G, ref readings);

                    setReading(rx, Reading.PWR, ref readings);
                    setReading(rx, Reading.REVERSE_PWR, ref readings);
                    setReading(rx, Reading.SWR, ref readings);

                    if (rx == 1)
                    { // only rx1 data
                        setReading(rx, Reading.DRIVE_FWD_ADC, ref readings);
                        setReading(rx, Reading.FWD_ADC, ref readings);
                        setReading(rx, Reading.REV_ADC, ref readings);
                        setReading(rx, Reading.DRIVE_PWR, ref readings);
                        setReading(rx, Reading.PA_FWD_PWR, ref readings);
                        setReading(rx, Reading.PA_REV_PWR, ref readings);
                        setReading(rx, Reading.CAL_FWD_PWR, ref readings);
                    }
                }
            }
        }
        private static void OnMox(int rx, bool oldMox, bool newMox)
        {
            _mox = newMox;
        }
        public static bool RequiresUpdate(int rx, Reading rt)
        {
            lock (_readingsLock)
            {
                return _readings[rx].RequiresUpdate(rt);
            }
        }

        // meters
        public static int QuickestUpdateInterval(int rx, bool mox)
        {
            int updateRate = 50;

            if (!mox)
            {
                foreach (KeyValuePair<string, clsMeter> kvp in _meters.Where(kvp => kvp.Value.RX == rx && (kvp.Value.TypeBitField & (int)MeterType.RX) == (int)MeterType.RX))
                {
                    int ui = kvp.Value.QuickestUpdateInterval;
                    if (ui < updateRate) updateRate = ui;
                }
            }
            else
            {
                foreach (KeyValuePair<string, clsMeter> kvp in _meters.Where(kvp => kvp.Value.RX == rx && (kvp.Value.TypeBitField & (int)MeterType.RX) != (int)MeterType.RX))
                {
                    int ui = kvp.Value.QuickestUpdateInterval;
                    if (ui < updateRate) updateRate = ui;
                }
            }
            return updateRate;
        }
        private static int overallUpdateInterval()
        {
            int updateInterval = 5000;

            for (int rx = 1; rx <= 2; rx++)
            {
                int tmp = QuickestUpdateInterval(rx, false);
                if (tmp < updateInterval) updateInterval = tmp;

                tmp = QuickestUpdateInterval(rx, true);
                if (tmp < updateInterval) updateInterval = tmp;
            }

            return updateInterval;
        }
        #endregion
        #region clsMeterItem
        private class clsMeterItem
        {
            public enum MeterItemType
            {
                BASE = 0,
                H_BAR,
                V_BAR,
                H_SCALE,
                V_SCALE,
                NEEDLE,
                TEXT,
                IMAGE,
                SOLID_COLOUR
            }

            private string _sGUID;

            private MeterItemType _ItemType;

            private PointF _topLeft; // 0-1,0-1
            private PointF _bottomRight; // 0-1,0-1
            private int _zOrder; // lower first
            private int _msUpdateInterval; //ms
            private float _attackRatio; // 0f - 1f
            private float _decayRatio; // 0f - 1f
            private float _value;
            private System.Drawing.Color _historyColour;
            private Reading _readingType;

            private Stopwatch _stopwatch;

            private Dictionary<float, PointF> _scaleCalibration;

            public clsMeterItem()
            {
                // constructor
                _sGUID = Guid.NewGuid().ToString();
                _ItemType = MeterItemType.BASE;
                _readingType = Reading.NONE;
                _topLeft.X = 0;
                _topLeft.Y = 0;
                _zOrder = 0;
                _bottomRight.X = 1;
                _bottomRight.Y = 1;
                _msUpdateInterval = 5000; //ms
                _attackRatio = 0.8f;
                _decayRatio = 0.2f;
                _value = -200;
                _historyColour = System.Drawing.Color.FromArgb(128, 255, 255, 255);
                _scaleCalibration = new Dictionary<float, PointF>();

                _stopwatch = new Stopwatch();
            }

            public string ID { get { return _sGUID; } }
            public PointF TopLeft {
                get { return _topLeft; }
                set { _topLeft = value; }
            }
            public PointF BottomRight {
                get { return _bottomRight; }
                set { _bottomRight = value; }
            }
            public int UpdateInterval
            {
                get { return _msUpdateInterval; }
                set
                {
                    _msUpdateInterval = value;
                    if (_msUpdateInterval < 1) _msUpdateInterval = 1;
                }
            }
            public float AttackRatio
            {
                get { return _attackRatio; }
                set {
                    _attackRatio = value;
                    if (_attackRatio < 0) _attackRatio = 0;
                    if (_attackRatio > 1f) _attackRatio = 1f;
                }
            }
            public float DecayRatio
            {
                get { return _decayRatio; }
                set
                {
                    _decayRatio = value;
                    if (_decayRatio < 0) _decayRatio = 0;
                    if (_decayRatio > 1f) _decayRatio = 1f;
                }
            }
            public MeterItemType ItemType
            {
                get { return _ItemType; }
                set { _ItemType = value; }
            }
            public Reading ReadingSource
            {
                get { return _readingType; }
                set { _readingType = value; }
            }

            public bool RequiresUpdate
            {
                get
                {
                    if (!_stopwatch.IsRunning) _stopwatch.Restart();
                    bool bNeedsUpdate = _stopwatch.ElapsedMilliseconds >= _msUpdateInterval;
                    if(bNeedsUpdate) _stopwatch.Stop();

                    return bNeedsUpdate;
                }
            }
            public virtual void Update(int rx, ref List<Reading> readingsUsed)
            {
                // can be overriden by derived

                //// get latest reading
                //float reading = MeterManager.GetReading(rx, ReadingSource);

                //Value = reading;

                //// this reading has been used
                //if (!readingsUsed.Contains(ReadingSource))
                //    readingsUsed.Add(ReadingSource);
            }
            public float Value
            {
                get { return _value; }
                set { _value = value; }
            }
            public int ZOrder
            {
                get { return _zOrder; }
                set { _zOrder = value; }
            }
            public virtual void History(out float minHistory, out float maxHistory)
            {
                minHistory = 0f;
                maxHistory = 0f;
            }
            public virtual float MinHistory
            {
                get
                {
                    return 0f;
                }
            }
            public virtual float MaxHistory
            {
                get
                {
                    return 0f;
                }
            }
            public virtual bool ShowHistory
            {
                get { return false; }
                set { }
            }
            public virtual System.Drawing.Color HistoryColour
            {
                get { return _historyColour; }
                set { _historyColour = value; }
            }
            public virtual Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }
        }
        //
        private class clsSolidColour : clsMeterItem
        {
            private System.Drawing.Color _colour;
            public clsSolidColour()
            {
                ItemType = MeterItemType.SOLID_COLOUR;

                _colour = System.Drawing.Color.Black;
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set
                { 
                    _colour = value;
                }
            }
        }
        private class clsImage : clsMeterItem
        {
            private string _name;
            private PointF _clipTopLeft;
            private PointF _clipBottomRight;
            private bool _clipped;
            public clsImage()
            {
                ItemType = MeterItemType.IMAGE;

                _name = "";
                _clipTopLeft = new PointF(0f, 0f);
                _clipBottomRight = new PointF(1f, 1f);
                _clipped = false;
            }
            public string ImageName
            {
                get { return _name; }
                set
                {
                    _name = value;
                }
            }
            public PointF ClipTopLeft
            {
                get { return _clipTopLeft; }
                set { _clipTopLeft = value; }
            }
            public PointF ClipBottomRight
            {
                get { return _clipBottomRight; }
                set { _clipBottomRight = value; }
            }
            public bool Clipped
            {
                get { return _clipped; }
                set { _clipped = value; }
            }
        }
        private class clsBarItem : clsMeterItem
        {
            public enum BarStyle
            {
                Line = 0,
                SolidFilled,
                GradientFilled,
                Segments
            }
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private bool _showHistory;
            private object _historyLock = new object();
            private BarStyle _style;
            private System.Drawing.Color _colour;
            private float _strokeWidth;

            private Dictionary<float, PointF> _scaleCalibration;

            public clsBarItem()
            {
                _history = new List<float>();
                _msHistoryDuration = 2000;
                _showHistory = false;

                _style = BarStyle.Line;
                _colour = System.Drawing.Color.Red;
                _strokeWidth = 3f;

                _scaleCalibration = new Dictionary<float, PointF>();

                ItemType = MeterItemType.H_BAR;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
            }
            public override void Update(int rx, ref List<Reading> readingsUsed)
            {
                // get latest reading
                float reading = MeterManager.GetReading(rx, ReadingSource);

                lock (_historyLock)
                {
                    if (reading > Value)
                        Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    // signal history
                    _history.Add(Value); // adds to end of the list
                    int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                    // the list is sized based on delay
                    if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                }

                // this reading has been used
                if (!readingsUsed.Contains(ReadingSource))
                    readingsUsed.Add(ReadingSource);
            }

            public BarStyle Style
            {
                get { return _style; }
                set { _style = value; }
            }
            public override bool ShowHistory
            {
                get { return _showHistory; }
                set { _showHistory = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        return _history.Max();
                    }
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                lock(_historyLock)
                {
                    minHistory = _history.Min();
                    maxHistory = _history.Max();
                }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set { _colour = value; }
            }
            public float StrokeWidth
            {
                get { return _strokeWidth; }
                set { _strokeWidth = value; }
            }
            public override Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }
        }
        private class clsNeedleItem : clsMeterItem
        {
            public enum NeedlePlacement
            {
                Bottom = 0,
                Top,
                Left,
                Right
            }
            public enum NeedleStyle
            {
                Line = 0,
                Arrow,
                Hollow
            }
            public enum NeedleDirection
            {
                Clockwise = 0,
                CounterClockwise
            }
            private List<float> _history;
            private int _msHistoryDuration; //ms
            private bool _showHistory;
            private object _historyLock = new object();
            private NeedleStyle _style;
            private PointF _needleOffset;
            private NeedlePlacement _placement;
            private System.Drawing.Color _colour;
            private float _strokeWidth;
            private NeedleDirection _needleDirection;
            private float _lengthFactor;
            private bool _setup;
            private Dictionary<float, PointF> _scaleCalibration;
            private PointF _radiusRatio;

            public clsNeedleItem()
            {
                _history = new List<float>();
                _msHistoryDuration = 2000;
                _showHistory = false;
                _placement = NeedlePlacement.Bottom;
                _needleOffset.X = 0f;
                _needleOffset.Y = 0.5f;
                _lengthFactor = 1f;
                _setup = false;
                _radiusRatio.X = 1f;
                _radiusRatio.Y = 1f;
                _style = NeedleStyle.Line;
                _colour = System.Drawing.Color.White;
                _strokeWidth = 3f;
                _needleDirection = NeedleDirection.Clockwise;
                _scaleCalibration = new Dictionary<float, PointF>();

                ItemType = MeterItemType.NEEDLE;
                ReadingSource = Reading.NONE;
                UpdateInterval = 100;
                AttackRatio = 0.8f;
                DecayRatio = 0.2f;
            }
            public override void Update(int rx, ref List<Reading> readingsUsed)
            {
                // get latest reading
                float reading = MeterManager.GetReading(rx, ReadingSource);

                lock (_historyLock)
                {
                    if (reading > Value)
                       Value = (reading * AttackRatio) + (Value * (1f - AttackRatio));
                    else
                        Value = (reading * DecayRatio) + (Value * (1f - DecayRatio));

                    // signal history
                    _history.Add(Value); // adds to end of the list
                    int numberToRemove = _history.Count - (_msHistoryDuration / UpdateInterval);
                    // the list is sized based on delay
                    if (numberToRemove > 0) _history.RemoveRange(0, numberToRemove); // remove the oldest, the head of the list
                }

                // this reading has been used
                if (!readingsUsed.Contains(ReadingSource))
                    readingsUsed.Add(ReadingSource);
            }
            public float StrokeWidth
            {
                get { return _strokeWidth; }
                set { _strokeWidth = value; }
            }
            public System.Drawing.Color Colour
            {
                get { return _colour; }
                set
                { _colour = value; }
            }
            public bool Setup
            {
                get { return _setup; }
                set { _setup = value; }
            }
            public NeedlePlacement Placement
            {
                get { return _placement; }
                set { _placement = value; }
            }
            public NeedleDirection Direction
            {
                get { return _needleDirection; }
                set { _needleDirection = value; }
            }
            public PointF NeedleOffset
            {
                get { return _needleOffset; }
                set { _needleOffset = value; }
            }
            public NeedleStyle Style
            {
                get { return _style; }
                set { _style = value; }
            }
            public override bool ShowHistory
            {
                get { return _showHistory; }
                set { _showHistory = value; }
            }
            public int HistoryDuration
            {
                get { return _msHistoryDuration; }
                set
                {
                    _msHistoryDuration = value;
                    if (_msHistoryDuration < UpdateInterval) _msHistoryDuration = UpdateInterval;
                }
            }
            public override float MinHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        return _history.Min();
                    }
                }
            }
            public override float MaxHistory
            {
                get
                {
                    lock (_historyLock)
                    {
                        return _history.Max();
                    }
                }
            }
            public override void History(out float minHistory, out float maxHistory)
            {
                lock (_historyLock)
                {
                    minHistory = _history.Min();
                    maxHistory = _history.Max();
                }
            }
            public float LengthFactor
            {
                get { return _lengthFactor; }
                set { _lengthFactor = value; }
            }
            public override Dictionary<float, PointF> ScaleCalibration
            {
                get { return _scaleCalibration; }
                set { }
            }
            public PointF RadiusRatio
            {
                get { return _radiusRatio; }
                set { _radiusRatio = value; }
            }
        }
        //
        #endregion
        #region clsMeter
        private class clsMeter
        {
            // the single meter, that may contain many bars/labels/analog/images/etc
            private string _sGuid;
            private string _Name;
            private int _TypeBitfield; // bitfield of MeterType
            private int _rx;

            private float _XRatio; // 0-1
            private float _YRatio; // 0-1

            private Dictionary<string, clsMeterItem> _meterItems;

            public clsMeter(int rx, string sName, int typeBitfield, float XRatio = 1f, float YRatio = 0.5f)
            {
                // constructor
                _sGuid = Guid.NewGuid().ToString();
                _rx = rx;
                _Name = sName;
                _TypeBitfield = typeBitfield;

                _XRatio = XRatio;
                _YRatio = YRatio;

                _meterItems = new Dictionary<string, clsMeterItem>();

                //test
                if ((TypeBitField & (int)MeterType.RX) == (int)MeterType.RX)
                {
                    //clsBarItem cb;

                    //cb = new clsBarItem();
                    //cb.TopLeft = new PointF(0.1f, 0.1f);
                    //cb.BottomRight = new PointF(0.9f, 0.15f);
                    //cb.ReadingSource = Reading.SIGNAL_STRENGTH;
                    //cb.AttackRatio = 0.1f;
                    //cb.DecayRatio = 0.05f;
                    //cb.UpdateInterval = 16;
                    //cb.ShowHistory = false;
                    //cb.Colour = System.Drawing.Color.DarkGray;
                    //cb.StrokeWidth = 1f;
                    //cb.Style = clsBarItem.BarStyle.Line;
                    //cb.ScaleCalibration.Add(-127, new PointF(0, 0)); // position for S0 or below
                    //cb.ScaleCalibration.Add(-73, new PointF(0.5f, 0)); // position for S9
                    //cb.ScaleCalibration.Add(-13, new PointF(1, 0)); // position for S9+60dB or above
                    //_meterItems.Add(cb.ID, cb);

                    //cb = new clsBarItem();
                    //cb.TopLeft = new PointF(0.1f, 0.1f);
                    //cb.BottomRight = new PointF(0.9f, 0.15f);
                    //cb.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                    //cb.AttackRatio = 0.1f;
                    //cb.DecayRatio = 0.05f;
                    //cb.UpdateInterval = 16;
                    //cb.HistoryDuration = 4000;
                    //cb.ShowHistory = true;
                    //cb.Colour = System.Drawing.Color.White;
                    //cb.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                    //cb.Style = clsBarItem.BarStyle.Line;
                    //cb.ScaleCalibration.Add(-127, new PointF(0, 0)); // position for S0 or below
                    //cb.ScaleCalibration.Add(-73, new PointF(0.5f, 0)); // position for S9
                    //cb.ScaleCalibration.Add(-13, new PointF(1, 0)); // position for S9+60dB or above
                    //cb.ZOrder = 1;
                    //_meterItems.Add(cb.ID, cb);

                    //cb = new clsBarItem();
                    //cb.TopLeft = new PointF(0.1f, 0.2f);
                    //cb.BottomRight = new PointF(0.9f, 0.25f);
                    //cb.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                    //cb.AttackRatio = 0.1f;
                    //cb.DecayRatio = 0.05f;
                    //cb.UpdateInterval = 16;
                    //cb.HistoryDuration = 2000;
                    //cb.ShowHistory = true;
                    //cb.Style = clsBarItem.BarStyle.Line;
                    //_meterItems.Add(cb.ID, cb);

                    //cb = new clsBarItem();
                    //cb.TopLeft = new PointF(0.1f, 0.3f);
                    //cb.BottomRight = new PointF(0.9f, 0.35f);
                    //cb.ReadingSource = Reading.ADC_REAL;
                    //cb.ShowHistory = true;
                    //_meterItems.Add(cb.ID, cb);

                    //cb = new clsBarItem();
                    //cb.TopLeft = new PointF(0.1f, 0.4f);
                    //cb.BottomRight = new PointF(0.9f, 0.45f);
                    //cb.ReadingSource = Reading.ADC_IMAG;
                    //cb.ShowHistory = true;
                    //_meterItems.Add(cb.ID, cb);

                    //clsSolidColour sc = new clsSolidColour();
                    //sc.TopLeft = new PointF(0.1f, 0.1f);
                    //sc.BottomRight = new PointF(0.9f, 0.15f);
                    //sc.Colour = System.Drawing.Color.Black;
                    //sc.ZOrder = -1;
                    //_meterItems.Add(sc.ID, sc);

                    //clsNeedleItem ni = new clsNeedleItem(); ;
                    //ni.TopLeft = new PointF(0f, 0f);
                    //ni.BottomRight = new PointF(1f, 0.579f);
                    //ni.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                    //ni.AttackRatio = 0.1f;
                    //ni.DecayRatio = 0.05f;
                    //ni.UpdateInterval = 16;
                    //ni.HistoryDuration = 4000;
                    //ni.ShowHistory = true;
                    //ni.Style = clsNeedleItem.NeedleStyle.Line;
                    //ni.Colour = System.Drawing.Color.YellowGreen;
                    //ni.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Green);
                    //ni.ZOrder = 1;
                    //ni.NeedleOffset = new PointF(-0.004f, 0.616f);
                    //ni.LengthFactor = 1.14f;
                    ////ni.Setup = true;
                    //ni.ScaleCalibration.Add(-121f, new PointF(0.091f, 0.42f));
                    //ni.ScaleCalibration.Add(-115f, new PointF(0.144f, 0.359f));
                    //ni.ScaleCalibration.Add(-109f, new PointF(0.191f, 0.281f));
                    //ni.ScaleCalibration.Add(-103f, new PointF(0.25f, 0.243f));
                    //ni.ScaleCalibration.Add(-97f, new PointF(0.306f, 0.184f));
                    //ni.ScaleCalibration.Add(-91f, new PointF(0.37f, 0.17f));
                    //ni.ScaleCalibration.Add(-85f, new PointF(0.432f, 0.134f));
                    //ni.ScaleCalibration.Add(-79f, new PointF(0.495f, 0.147f));
                    //ni.ScaleCalibration.Add(-73f, new PointF(0.56f, 0.134f));
                    //ni.ScaleCalibration.Add(-63f, new PointF(0.623f, 0.153f));
                    //ni.ScaleCalibration.Add(-53f, new PointF(0.686f, 0.184f));
                    //ni.ScaleCalibration.Add(-43f, new PointF(0.745f, 0.225f));
                    //ni.ScaleCalibration.Add(-33f, new PointF(0.802f, 0.279f));
                    //ni.ScaleCalibration.Add(-23f, new PointF(0.865f, 0.344f));
                    //ni.ScaleCalibration.Add(-13f, new PointF(0.902f, 0.419f));
                    //_meterItems.Add(ni.ID, ni);

                    //ni = new clsNeedleItem();
                    //ni.TopLeft = new PointF(0f, 0f);
                    //ni.BottomRight = new PointF(1f, 0.579f);
                    //ni.ReadingSource = Reading.SIGNAL_STRENGTH;
                    //ni.AttackRatio = 0.1f;
                    //ni.DecayRatio = 0.05f;
                    //ni.UpdateInterval = 16;
                    //ni.ShowHistory = false;
                    //ni.Style = clsNeedleItem.NeedleStyle.Line;
                    //ni.Colour = System.Drawing.Color.LimeGreen;
                    //ni.StrokeWidth = 2f;
                    //ni.NeedleOffset = new PointF(-0.004f, 0.616f);
                    //ni.LengthFactor = 1.14f;
                    //ni.ScaleCalibration.Add(-121f, new PointF(0.091f, 0.42f));
                    //ni.ScaleCalibration.Add(-115f, new PointF(0.144f, 0.359f));
                    //ni.ScaleCalibration.Add(-109f, new PointF(0.191f, 0.281f));
                    //ni.ScaleCalibration.Add(-103f, new PointF(0.25f, 0.243f));
                    //ni.ScaleCalibration.Add(-97f, new PointF(0.306f, 0.184f));
                    //ni.ScaleCalibration.Add(-91f, new PointF(0.37f, 0.17f));
                    //ni.ScaleCalibration.Add(-85f, new PointF(0.432f, 0.134f));
                    //ni.ScaleCalibration.Add(-79f, new PointF(0.495f, 0.147f));
                    //ni.ScaleCalibration.Add(-73f, new PointF(0.56f, 0.134f));
                    //ni.ScaleCalibration.Add(-63f, new PointF(0.623f, 0.153f));
                    //ni.ScaleCalibration.Add(-53f, new PointF(0.686f, 0.184f));
                    //ni.ScaleCalibration.Add(-43f, new PointF(0.745f, 0.225f));
                    //ni.ScaleCalibration.Add(-33f, new PointF(0.802f, 0.279f));
                    //ni.ScaleCalibration.Add(-23f, new PointF(0.865f, 0.344f));
                    //ni.ScaleCalibration.Add(-13f, new PointF(0.902f, 0.419f));
                    //_meterItems.Add(ni.ID, ni);

                    ////sc = new clsSolidColour();
                    ////sc.TopLeft = new PointF(0.1f, 0.5f);
                    ////sc.BottomRight = new PointF(0.9f, 0.9f);
                    ////sc.Colour = System.Drawing.Color.FromArgb(255,48,48,48);
                    ////sc.ZOrder = -1;
                    ////_meterItems.Add(sc.ID, sc);

                    //clsImage img = new clsImage();
                    //img.TopLeft = new PointF(0f, 0f);
                    //img.BottomRight = new PointF(1f, 0.579f);
                    //img.ZOrder = -2;
                    //img.ImageName = "s-meter-green";
                    //_meterItems.Add(img.ID, img);

                    //img = new clsImage();
                    //img.TopLeft = new PointF(0.4f, 0.579f - 0.05f);
                    //img.BottomRight = new PointF(0.6f, 0.579f + 0.15f);
                    //img.ZOrder = 10;
                    //img.ImageName = "cover";
                    //img.Clipped = true;
                    //img.ClipTopLeft = new PointF(0f, 0f);
                    //img.ClipBottomRight = new PointF(1f, 0.579f);
                    //_meterItems.Add(img.ID, img);
                    ////
                    ///
                    clsNeedleItem ni = new clsNeedleItem(); ;
                    ni.TopLeft = new PointF(0f, 0f);
                    ni.BottomRight = new PointF(1f, 0.409f);
                    ni.ReadingSource = Reading.AVG_SIGNAL_STRENGTH;
                    ni.AttackRatio = 0.1f;
                    ni.DecayRatio = 0.05f;
                    ni.UpdateInterval = 16;
                    ni.HistoryDuration = 4000;
                    ni.ShowHistory = true;
                    ni.ShowHistory = true;
                    ni.Style = clsNeedleItem.NeedleStyle.Line;
                    ni.Colour = System.Drawing.Color.Black;
                    ni.HistoryColour = System.Drawing.Color.FromArgb(128, 238, 195, 154);
                    ni.ZOrder = 1;
                    ni.NeedleOffset = new PointF(0f, 0.832f);
                    ni.RadiusRatio = new PointF(1f, 0.58f);
                    ni.LengthFactor = 1.65f;
                    //ni.Setup = true;
                    ni.ScaleCalibration.Add(-127f, new PointF(0.076f, 0.31f));
                    ni.ScaleCalibration.Add(-121f, new PointF(0.131f, 0.272f));
                    ni.ScaleCalibration.Add(-115f, new PointF(0.189f, 0.254f));
                    ni.ScaleCalibration.Add(-109f, new PointF(0.233f, 0.211f));
                    ni.ScaleCalibration.Add(-103f, new PointF(0.284f, 0.207f));
                    ni.ScaleCalibration.Add(-97f, new PointF(0.326f, 0.177f));
                    ni.ScaleCalibration.Add(-91f, new PointF(0.374f, 0.177f));
                    ni.ScaleCalibration.Add(-85f, new PointF(0.414f, 0.151f));
                    ni.ScaleCalibration.Add(-79f, new PointF(0.459f, 0.168f));
                    ni.ScaleCalibration.Add(-73f, new PointF(0.501f, 0.142f));
                    ni.ScaleCalibration.Add(-63f, new PointF(0.564f, 0.172f));
                    ni.ScaleCalibration.Add(-53f, new PointF(0.63f, 0.164f));
                    ni.ScaleCalibration.Add(-43f, new PointF(0.695f, 0.203f));
                    ni.ScaleCalibration.Add(-33f, new PointF(0.769f, 0.211f));
                    ni.ScaleCalibration.Add(-23f, new PointF(0.838f, 0.272f));
                    ni.ScaleCalibration.Add(-13f, new PointF(0.926f, 0.31f));
                    _meterItems.Add(ni.ID, ni);

                    clsImage img = new clsImage();
                    img.TopLeft = new PointF(0f, 0f);
                    img.BottomRight = new PointF(1f, 0.409f);
                    img.ZOrder = 0;
                    img.ImageName = "kenwood";
                    _meterItems.Add(img.ID, img);
                }
                else if ((typeBitfield & (int)MeterType.MOX) == (int)MeterType.MOX)
                {
                    clsNeedleItem ni;
                    clsImage img;
                    //// pwr
                    //ni = new clsNeedleItem();
                    //ni.TopLeft = new PointF(0.1f, 0.54f);
                    //ni.BottomRight = new PointF(0.9f, 0.99f);
                    //ni.ReadingSource = Reading.PWR;
                    //ni.AttackRatio = 0.2f;
                    //ni.DecayRatio = 0.05f;
                    //ni.UpdateInterval = 16;
                    //ni.HistoryDuration = 1000;
                    //ni.ShowHistory = true;
                    //ni.Style = clsNeedleItem.NeedleStyle.Line;
                    //ni.Colour = System.Drawing.Color.Red;
                    //ni.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red);
                    //ni.ZOrder = 1;
                    //ni.NeedleOffset = new PointF(0f, 0.64f);
                    //ni.LengthFactor = 1.08f;
                    //ni.ScaleCalibration.Add(0, new PointF(0.119f, 0.468f)); // position for 0W
                    //ni.ScaleCalibration.Add(5, new PointF(0.214f, 0.341f));
                    //ni.ScaleCalibration.Add(10, new PointF(0.277f, 0.292f));
                    //ni.ScaleCalibration.Add(20, new PointF(0.356f, 0.240f));
                    //ni.ScaleCalibration.Add(30, new PointF(0.428f, 0.208f));
                    //ni.ScaleCalibration.Add(50, new PointF(0.557f, 0.124f));
                    //ni.ScaleCalibration.Add(60, new PointF(0.615f, 0.225f));
                    //ni.ScaleCalibration.Add(100, new PointF(0.786f, 0.346f)); // position for MAX W
                    ////ni.Setup = true;
                    //_meterItems.Add(ni.ID, ni);

                    ////swr
                    //ni = new clsNeedleItem();
                    //ni.TopLeft = new PointF(0.1f, 0.54f);
                    //ni.BottomRight = new PointF(0.9f, 0.99f);
                    //ni.ReadingSource = Reading.SWR;
                    //ni.AttackRatio = 0.1f;
                    //ni.DecayRatio = 0.05f;
                    //ni.UpdateInterval = 16;
                    //ni.HistoryDuration = 500;
                    //ni.ShowHistory = true;
                    //ni.Style = clsNeedleItem.NeedleStyle.Line;
                    //ni.Colour = System.Drawing.Color.Yellow;
                    //ni.HistoryColour = System.Drawing.Color.FromArgb(128, System.Drawing.Color.Yellow);
                    //ni.ZOrder = 2;
                    //ni.NeedleOffset = new PointF(0f, 0.64f);
                    //ni.LengthFactor = 0.88f;
                    //ni.ScaleCalibration.Add(1f, new PointF(0.179f, 0.577f)); // position for 1:1
                    //ni.ScaleCalibration.Add(1.5f, new PointF(0.238f, 0.483f));
                    //ni.ScaleCalibration.Add(2f, new PointF(0.298f, 0.427f));
                    //ni.ScaleCalibration.Add(2.5f, new PointF(0.370f, 0.379f));
                    //ni.ScaleCalibration.Add(3f, new PointF(0.419f, 0.358f));
                    //ni.ScaleCalibration.Add(3.5f, new PointF(0.467f, 0.352f));
                    //ni.ScaleCalibration.Add(4f, new PointF(0.503f, 0.350f));
                    //ni.ScaleCalibration.Add(5f, new PointF(0.554f, 0.358f));
                    //ni.ScaleCalibration.Add(10f, new PointF(0.651f, 0.397f)); // position for 10:1
                    //ni.ScaleCalibration.Add(float.MaxValue, new PointF(0.744f, 0.479f)); // position for infinity
                    //_meterItems.Add(ni.ID, ni);

                    //img = new clsImage();
                    //img.TopLeft = new PointF(0.1f, 0.54f);
                    //img.BottomRight = new PointF(0.9f, 0.99f);
                    //img.ZOrder = -2;
                    //img.ImageName = "s-meter-green";
                    //_meterItems.Add(img.ID, img);

                    //img = new clsImage();
                    //img.TopLeft = new PointF(0.4f, 0.95f);
                    //img.BottomRight = new PointF(0.6f, 1.15f);
                    //img.ZOrder = 10;
                    //img.ImageName = "cover";
                    //img.Clipped = true;
                    //img.ClipTopLeft = new PointF(0.1f, 0.54f);
                    //img.ClipBottomRight = new PointF(0.9f, 0.99f); ;
                    //_meterItems.Add(img.ID, img);


                    // cross
                    ni = new clsNeedleItem();
                    ni.TopLeft = new PointF(0f, 0f);
                    ni.BottomRight = new PointF(1f, 0.782f);
                    ni.ReadingSource = Reading.PWR;
                    ni.AttackRatio = 0.2f;//0.325f;
                    ni.DecayRatio = 0.1f;//0.5f;
                    ni.UpdateInterval = 16;
                    ni.HistoryDuration = 1000;
                    ni.ShowHistory = true;
                    ni.StrokeWidth = 2.5f;
                    ni.Style = clsNeedleItem.NeedleStyle.Line;
                    ni.Colour = System.Drawing.Color.Black;
                    ni.HistoryColour = System.Drawing.Color.FromArgb(64, System.Drawing.Color.Red);
                    ni.ZOrder = 2;
                    ni.NeedleOffset = new PointF(0.322f, 0.611f);
                    ni.LengthFactor = 1.62f;
                    ni.ScaleCalibration.Add(0f, new PointF(0.052f, 0.732f));
                    ni.ScaleCalibration.Add(5f, new PointF(0.146f, 0.528f));
                    ni.ScaleCalibration.Add(10f, new PointF(0.188f, 0.434f));
                    ni.ScaleCalibration.Add(15f, new PointF(0.235f, 0.387f));
                    ni.ScaleCalibration.Add(20f, new PointF(0.258f, 0.338f));
                    ni.ScaleCalibration.Add(25f, new PointF(0.303f, 0.313f));
                    ni.ScaleCalibration.Add(30f, new PointF(0.321f, 0.272f));
                    ni.ScaleCalibration.Add(35f, new PointF(0.361f, 0.257f));
                    ni.ScaleCalibration.Add(40f, new PointF(0.381f, 0.223f));
                    ni.ScaleCalibration.Add(50f, new PointF(0.438f, 0.181f));
                    ni.ScaleCalibration.Add(60f, new PointF(0.483f, 0.155f));
                    ni.ScaleCalibration.Add(70f, new PointF(0.532f, 0.13f));
                    ni.ScaleCalibration.Add(80f, new PointF(0.577f, 0.111f));
                    ni.ScaleCalibration.Add(90f, new PointF(0.619f, 0.098f));
                    ni.ScaleCalibration.Add(100f, new PointF(0.662f, 0.083f));
                    _meterItems.Add(ni.ID, ni);

                    ni = new clsNeedleItem();
                    ni.TopLeft = new PointF(0f, 0f);
                    ni.BottomRight = new PointF(1f, 0.782f);
                    ni.ReadingSource = Reading.REVERSE_PWR;
                    ni.AttackRatio = 0.2f;//0.325f;
                    ni.DecayRatio = 0.1f;//0.5f;
                    ni.UpdateInterval = 16;
                    ni.HistoryDuration = 1000;
                    ni.ShowHistory = false;
                    ni.StrokeWidth = 2.5f;
                    ni.Style = clsNeedleItem.NeedleStyle.Line;
                    ni.Colour = System.Drawing.Color.Black;
                    ni.HistoryColour = System.Drawing.Color.FromArgb(64, System.Drawing.Color.Yellow);
                    ni.ZOrder = 1;
                    ni.NeedleOffset = new PointF(-0.322f, 0.611f);
                    ni.LengthFactor = 1.62f;
                    ni.ScaleCalibration.Add(0f, new PointF(0.948f, 0.74f));
                    ni.ScaleCalibration.Add(1f, new PointF(0.854f, 0.538f));
                    ni.ScaleCalibration.Add(2f, new PointF(0.814f, 0.443f));
                    ni.ScaleCalibration.Add(3f, new PointF(0.769f, 0.4f));
                    ni.ScaleCalibration.Add(4f, new PointF(0.744f, 0.351f));
                    ni.ScaleCalibration.Add(5f, new PointF(0.702f, 0.321f));
                    ni.ScaleCalibration.Add(6f, new PointF(0.682f, 0.285f));
                    ni.ScaleCalibration.Add(7f, new PointF(0.646f, 0.268f));
                    ni.ScaleCalibration.Add(8f, new PointF(0.626f, 0.234f));
                    ni.ScaleCalibration.Add(9f, new PointF(0.596f, 0.228f));
                    ni.ScaleCalibration.Add(10f, new PointF(0.569f, 0.406f));
                    ni.ScaleCalibration.Add(12f, new PointF(0.524f, 0.166f));
                    ni.ScaleCalibration.Add(14f, new PointF(0.476f, 0.14f));
                    ni.ScaleCalibration.Add(16f, new PointF(0.431f, 0.121f));
                    ni.ScaleCalibration.Add(18f, new PointF(0.393f, 0.109f));
                    ni.ScaleCalibration.Add(20f, new PointF(0.349f, 0.098f));
                    _meterItems.Add(ni.ID, ni);

                    img = new clsImage();
                    img.TopLeft = new PointF(0f, 0f);
                    img.BottomRight = new PointF(1f, 0.782f);
                    img.ZOrder = 0;
                    img.ImageName = "cross-needle-100";
                    _meterItems.Add(img.ID, img);

                    img = new clsImage();
                    img.TopLeft = new PointF(0f, 0.6f);
                    img.BottomRight = new PointF(1f, 1f);
                    img.ZOrder = 3;
                    img.ImageName = "cross-needle-bg";
                    _meterItems.Add(img.ID, img);
                }
            }

            public float XRatio { get { return _XRatio; } }
            public float YRatio { get { return _YRatio; } }
            public string Name { get { return _Name; } }
            public int TypeBitField { get { return _TypeBitfield; } }
            public string ID { get { return _sGuid; } }
            public int RX { get { return _rx; } }

            public int QuickestUpdateInterval
            {
                get
                {
                    int updateInterval = int.MaxValue;
                    foreach (var kvp in _meterItems)
                    {
                        clsMeterItem mi = kvp.Value;
                        if (mi.UpdateInterval < updateInterval) updateInterval = mi.UpdateInterval;
                    }
                    return updateInterval;
                }
            }
            public void Update(ref List<Reading> readingsUsed)
            {
                // this is called for each meter from the meter manager thread
                foreach (KeyValuePair<string, clsMeterItem> kvp in _meterItems)
                {
                    // now we need to update each item in the meter
                    if(kvp.Value.RequiresUpdate) kvp.Value.Update(_rx, ref readingsUsed);
                }
            }
            public Dictionary<string, clsMeterItem> MeterItems
            {
                get { return _meterItems; }
            }
        }
        #endregion
        #region clsReading
        private class clsReading
        {
            public Reading Type;
            public float Reading;
            public bool Updated;
        }
        private class clsReadings
        {
            private Dictionary<Reading, clsReading> _latestReading;
            public clsReadings()
            {
                _latestReading = new Dictionary<Reading, clsReading>();

                for (int n = 0; n < (int)Reading.LAST; n++)
                {
                    clsReading cr = new clsReading() { Type = (Reading)n, Reading = -200f, Updated = false };
                    _latestReading.Add((Reading)n, cr);
                }
            }
            public float GetReading(Reading rt, bool useReading = false)
            {
                if (useReading) UseReading(rt);
                return _latestReading[rt].Reading;
            }
            public void SetReading(Reading rt, float value)
            {
                _latestReading[rt].Reading = value;
                _latestReading[rt].Updated = true;
            }
            public bool RequiresUpdate(Reading rt)
            {
                return !_latestReading[rt].Updated;
            }
            public void UseReading(Reading rt)
            {
                _latestReading[rt].Updated = false;
            }
        }
        #endregion       
        #region DirectX
        //based on display.cs
        private class DXRenderer
        {
            private const SharpDX.Direct2D1.AlphaMode _ALPHA_MODE = SharpDX.Direct2D1.AlphaMode.Premultiplied;
            private bool _bDXSetup;
            private bool _displayRunning;
            private bool _dxDisplayThreadRunning;
            private Thread _dxRenderThread;
            private PresentFlags _NoVSYNCpresentFlag;
            private SharpDX.DXGI.Factory1 _factory1;
            private SharpDX.Direct3D11.Device _device;
            private int _nBufferCount;
            private Surface _surface;
            private SwapChain _swapChain;
            private SwapChain1 _swapChain1;
            private RenderTarget _renderTarget;
            private SharpDX.Direct2D1.Factory _factory;
            private bool _bAntiAlias;
            private Vector2 _pixelShift;
            private int _nVBlanks;
            private Control _displayTarget;
            private object _DXlock = new object();
            //
            private Dictionary<System.Drawing.Color, SharpDX.Direct2D1.Brush> _DXBrushes;
            private Color4 _backColour;
            private Dictionary<string, SharpDX.Direct2D1.Bitmap> _images;
            //

            private int _rx;
            private Console _console;

            public DXRenderer(int rx, Control target, Console c, string sImagePath)
            {
                _rx = rx;
                _console = c;

                //dx
                _displayRunning = false;
                _DXBrushes = null;
                _bAntiAlias = true;
                _bDXSetup = false;
                _dxDisplayThreadRunning = false;
                _NoVSYNCpresentFlag = PresentFlags.None;
                _pixelShift = new Vector2(0.5f, 0.5f);
                _nVBlanks = 0;
                _displayTarget = target;
                _backColour = new Color4(128 / 255f, 128 / 255f, 128 / 255f, 255 / 255f);
                //_nBufferCount = 1;
                _images = new Dictionary<string, SharpDX.Direct2D1.Bitmap>();

                _displayTarget.Resize += target_Resize;

                init();

                loadImages(sImagePath);

                _dxDisplayThreadRunning = false;
                _dxRenderThread = new Thread(new ThreadStart(dxRender))
                {
                    Name = "Multimeter DX Render Thread",
                    Priority = ThreadPriority.Lowest,
                    IsBackground = true
                };
                _dxRenderThread.Start();

                _displayRunning = true;
            }

            //DIRECTX
            private void loadImages(string sImagePath)
            {
                if (!sImagePath.EndsWith("\\")) sImagePath += "\\";

                if (System.IO.Directory.Exists(sImagePath))
                {
                    string[] sFiles = System.IO.Directory.GetFiles(sImagePath);
                    foreach (string sFile in sFiles)
                    {
                        string sID = System.IO.Path.GetFileNameWithoutExtension(sFile);
                        loadImage(sFile, sID);
                    }
                }
            }
            private void loadImage(string sPath, string sID)
            {
                if (System.IO.File.Exists(sPath) && !_images.ContainsKey(sID))
                {
                    try
                    {
                        System.Drawing.Image image = System.Drawing.Image.FromFile(sPath);

                        if (image != null)
                        {
                            using (System.Drawing.Bitmap graphicsImage = new System.Drawing.Bitmap(image))
                            {
                                SharpDX.Direct2D1.Bitmap img = bitmapFromSysBitmap(_renderTarget, graphicsImage);
                                _images.Add(sID, img);
                            }
                        }
                    }
                    catch { }
                }
            }
            private int getMaxSamples()
            {
                //D3D11_MAX_MULTISAMPLE_SAMPLE_COUNT 32u32
                //NOTE: can not be used with FlipDiscard atm unfortunately
                for (int n = 32; n>0; n /= 2)
                {
                    if(_device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, n) > 0)
                    {
                        return n;
                    }
                }
                return 1;
            }
            private void init(DriverType driverType = DriverType.Hardware)
            {
                // code based on display.cs
                if (_bDXSetup) return;

                try
                {
                    DeviceCreationFlags debug = DeviceCreationFlags.None;

                    // to get this to work, need to target the os
                    // https://www.prugg.at/2019/09/09/properly-detect-windows-version-in-c-net-even-windows-10/
                    // you need to enable operating system support in the app1.manifest file, otherwise majVers will not report 10+
                    // note: windows 10, 11, server 2016, server 2019, server 2022 all use the windows 10 os id in the manifest file at this current time
                    int majVers = Environment.OSVersion.Version.Major;
                    int minVers = Environment.OSVersion.Version.Minor;

                    SharpDX.Direct3D.FeatureLevel[] featureLevels;

                    if (majVers >= 10) // win10 + 11
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_12_1,
                            SharpDX.Direct3D.FeatureLevel.Level_12_0,
                            SharpDX.Direct3D.FeatureLevel.Level_11_1, // windows 8 and up
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.DoNotWait;
                    }
                    else if (majVers == 6 && minVers >= 2) // windows 8, windows 8.1
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_11_1, // windows 8 and up
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.DoNotWait;
                    }
                    else if (majVers == 6 && minVers < 2) // windows 7, 2008 R2, 2008, vista
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_11_0, // windows 7 and up (level 11 was only partial on 7, not 11_1)
                            SharpDX.Direct3D.FeatureLevel.Level_10_1,
                            SharpDX.Direct3D.FeatureLevel.Level_10_0,
                            SharpDX.Direct3D.FeatureLevel.Level_9_3,
                            SharpDX.Direct3D.FeatureLevel.Level_9_2,
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.None;
                    }
                    else
                    {
                        featureLevels = new SharpDX.Direct3D.FeatureLevel[] {
                            SharpDX.Direct3D.FeatureLevel.Level_9_1
                        };
                        _NoVSYNCpresentFlag = PresentFlags.None;
                    }

                    _factory1 = new SharpDX.DXGI.Factory1();

                    _device = new SharpDX.Direct3D11.Device(driverType, debug | DeviceCreationFlags.PreventAlteringLayerSettingsFromRegistry | DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded, featureLevels);

                    SharpDX.DXGI.Device1 device1 = _device.QueryInterfaceOrNull<SharpDX.DXGI.Device1>();
                    if (device1 != null)
                    {
                        device1.MaximumFrameLatency = 1;
                        Utilities.Dispose(ref device1);
                        device1 = null;
                    }

                    //this code should ideally be used to prevent use of flip if vsync is 0
                    //but is not used at this time
                    //SharpDX.DXGI.Factory5 f5 = factory.QueryInterfaceOrNull<SharpDX.DXGI.Factory5>();
                    //bool bAllowTearing = false;
                    //if(f5 != null)
                    //{
                    //    int size = Marshal.SizeOf(typeof(bool));
                    //    IntPtr pBool = Marshal.AllocHGlobal(size);

                    //    f5.CheckFeatureSupport(SharpDX.DXGI.Feature.PresentAllowTearing, pBool, size);

                    //    bAllowTearing = Marshal.ReadInt32(pBool) == 1;

                    //    Marshal.FreeHGlobal(pBool);
                    //}
                    //

                    // check if the device has a factory4 interface
                    // if not, then we need to use old bitplit swapeffect
                    SwapEffect swapEffect;

                    SharpDX.DXGI.Factory4 factory4 = _factory1.QueryInterfaceOrNull<SharpDX.DXGI.Factory4>();
                    bool bFlipPresent = false;
                    if (factory4 != null)
                    {
                        /*if (!_bUseLegacyBuffers)*/ bFlipPresent = true;
                        Utilities.Dispose(ref factory4);
                        factory4 = null;
                    }

                    //https://walbourn.github.io/care-and-feeding-of-modern-swapchains/
                    swapEffect = bFlipPresent ? SwapEffect.FlipDiscard : SwapEffect.Discard; //NOTE: FlipSequential should work, but is mostly used for storeapps
                    _nBufferCount = bFlipPresent ? 2 : 1;

                    //int multiSample = 2; // eg 2 = MSAA_2, 2 times multisampling
                    //int maxQuality = _device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, multiSample) - 1;
                    //maxQuality = Math.Max(0, maxQuality);

                    ModeDescription md = new ModeDescription(_displayTarget.Width, _displayTarget.Height,
                                                               new Rational(60, 1)/*console.DisplayFPS, 1)*/, Format.B8G8R8A8_UNorm);
                    md.ScanlineOrdering = DisplayModeScanlineOrder.Progressive;
                    md.Scaling = DisplayModeScaling.Centered;
                    
                    SwapChainDescription desc = new SwapChainDescription()
                    {
                        BufferCount = _nBufferCount,
                        ModeDescription = md,
                        IsWindowed = true,
                        OutputHandle = _displayTarget.Handle,
                        //SampleDescription = new SampleDescription(multiSample, maxQuality),
                        SampleDescription = new SampleDescription(1, 0), // no multi sampling (1 sample), no antialiasing
                        SwapEffect = swapEffect,
                        Usage = Usage.RenderTargetOutput,// | Usage.BackBuffer,  // dont need usage.backbuffer as it is implied
                        Flags = SwapChainFlags.None,
                    };

                    _factory1.MakeWindowAssociation(_displayTarget.Handle, WindowAssociationFlags.IgnoreAll);
                    _swapChain = new SwapChain(_factory1, _device, desc);
                    _swapChain1 = _swapChain.QueryInterface<SwapChain1>();

                    _factory = new SharpDX.Direct2D1.Factory(FactoryType.SingleThreaded, DebugLevel.None);
                    
                    _surface = _swapChain1.GetBackBuffer<Surface>(0);

                    RenderTargetProperties rtp = new RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(_swapChain.Description.ModeDescription.Format, _ALPHA_MODE));                    
                    _renderTarget = new RenderTarget(_factory, _surface, rtp);

                    if (debug == DeviceCreationFlags.Debug)
                    {
                        _device.DebugName = "MeterDeviceDB_" + _rx.ToString();
                        _swapChain.DebugName = "MeterSwapChainDB_" + _rx.ToString();
                        _swapChain1.DebugName = "MeterSwapChain1DB_" + _rx.ToString();
                        _surface.DebugName = "MeterSurfaceDB_" + _rx.ToString();
                    }
                    else
                    {
                        _device.DebugName = ""; // used in shutdown
                    }

                    _bDXSetup = true;

                    setupAliasing();

                    buildDXResources();
                    //buildDXFonts();
                }
                catch (Exception e)
                {
                    // issue setting up dx
                    ShutdownDX();
                    MessageBox.Show("Problem initialising Meter DirectX !" + System.Environment.NewLine + System.Environment.NewLine + "[" + e.ToString() + "]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            public void ShutdownDX(bool bFromRenderThread = false)
            {
                if(_displayTarget != null) _displayTarget.Resize -= target_Resize;

                if (!_bDXSetup) return;

                _dxDisplayThreadRunning = false;
                if (_dxRenderThread != null && _dxRenderThread.IsAlive && !bFromRenderThread) _dxRenderThread.Join();

                try
                {
                    if (_device != null && _device.ImmediateContext != null)
                    {
                        _device.ImmediateContext.ClearState();
                        _device.ImmediateContext.Flush();
                    }

                    foreach (KeyValuePair<string, SharpDX.Direct2D1.Bitmap> kvp in _images)
                    {
                        SharpDX.Direct2D1.Bitmap b = kvp.Value;
                        Utilities.Dispose(ref b);
                    }
                    _images.Clear();

                    //releaseDXFonts();
                    releaseDXResources();

                    Utilities.Dispose(ref _renderTarget);
                    Utilities.Dispose(ref _swapChain1);
                    Utilities.Dispose(ref _swapChain);
                    Utilities.Dispose(ref _surface);
                    Utilities.Dispose(ref _factory);
                    Utilities.Dispose(ref _factory1);

                    _renderTarget = null;
                    _swapChain1 = null;
                    _swapChain = null;
                    _surface = null;
                    _factory = null;
                    _factory1 = null;

                    if (_device != null && _device.ImmediateContext != null)
                    {
                        SharpDX.Direct3D11.DeviceContext dc = _device.ImmediateContext;
                        Utilities.Dispose(ref dc);
                        dc = null;
                    }

                    SharpDX.Direct3D11.DeviceDebug ddb = null;
                    if (_device != null && _device.DebugName != "")
                    {
                        ddb = new SharpDX.Direct3D11.DeviceDebug(_device);
                        ddb.ReportLiveDeviceObjects(ReportingLevel.Detail);
                    }

                    if (ddb != null)
                    {
                        Utilities.Dispose(ref ddb);
                        ddb = null;
                    }
                    Utilities.Dispose(ref _device);
                    _device = null;

                    _bDXSetup = false;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Problem Shutting Down Meter DirectX !" + System.Environment.NewLine + System.Environment.NewLine + "[" + e.ToString() + "]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            private void dxRender()
            {
                if (!_bDXSetup) return;

                try
                {
                    _dxDisplayThreadRunning = true;
                    while (_dxDisplayThreadRunning)
                    {
                        if (_displayRunning && _displayTarget.Visible)
                        {
                            lock (_DXlock)
                            {
                                _renderTarget.BeginDraw();

                                // middle pixel align shift
                                Matrix3x2 t = _renderTarget.Transform;
                                t.TranslationVector = _pixelShift;
                                _renderTarget.Transform = t;

                                // background for entire form/area?
                                _renderTarget.Clear(_backColour);

                                drawMeters();
                                //

                                // undo the translate
                                _renderTarget.Transform = Matrix3x2.Identity;

                                _renderTarget.EndDraw();

                                // render
                                // note: the only way to have Present non block when using vsync number of blanks 0 , is to use DoNotWait
                                // however the gpu will error if it is busy doing something and the data can not be queued
                                // It will error and just ignore everything, we try present and ignore the 0x887A000A error
                                PresentFlags pf = _nVBlanks == 0 ? _NoVSYNCpresentFlag : PresentFlags.None;
                                Result r = _swapChain1.TryPresent(_nVBlanks, pf);

                                if (r != Result.Ok && r != 0x887A000A)
                                {
                                    string sMsg = "";
                                    if (r == 0x887A0001) sMsg = "Present Device Invalid Call" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";    //DXGI_ERROR_INVALID_CALL
                                    if (r == 0x887A0007) sMsg = "Present Device Reset" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";           //DXGI_ERROR_DEVICE_RESET
                                    if (r == 0x887A0005) sMsg = "Present Device Removed" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";         //DXGI_ERROR_DEVICE_REMOVED
                                    if (r == 0x88760870) sMsg = "Present Device DD3DDI Removed" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";  //D3DDDIERR_DEVICEREMOVED
                                                                                                                                                                                //if (r == 0x087A0001) sMsg = "Present Device Occluded" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]";      //DXGI_STATUS_OCCLUDED
                                                                                                                                                                                //(ignored in the preceding if statement) if (r == 0x887A000A) sMsg = "Present Device Still Drawping" + Environment.NewLine + "" + Environment.NewLine + "[ " + r.ToString() + " ]"; //DXGI_ERROR_WAS_STILL_DRAWING

                                    if (sMsg != "") throw (new Exception(sMsg));
                                }
                            }
                        }
                        if(_displayTarget.Visible)
                            Thread.Sleep(overallUpdateInterval());
                        else
                            Thread.Sleep(500); // if not visible, sleep for half second
                    }
                }
                catch (Exception e)
                {
                    ShutdownDX(true);
                    MessageBox.Show("Problem in DirectX Meter Renderer !" + System.Environment.NewLine + System.Environment.NewLine + "[ " + e.ToString() + " ]", "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            private void setupAliasing()
            {
                if (!_bDXSetup) return;

                if (_bAntiAlias)
                    _renderTarget.AntialiasMode = AntialiasMode.PerPrimitive; // this will antialias even if multisampling is off
                else
                    _renderTarget.AntialiasMode = AntialiasMode.Aliased; // this will result in non antialiased lines only if multisampling = 1

                _renderTarget.TextAntialiasMode = TextAntialiasMode.Default;
            }
            public int VerticalBlanks
            {
                get { return _nVBlanks; }
                set
                {
                    int v = value;
                    if (v < 0) v = 0;
                    if (v > 4) v = 4;
                    _nVBlanks = v;
                }
            }
            public Control Target
            {
                get { return _displayTarget; }
                set
                {
                    _displayTarget = value;
                }
            }
            private int targetWidth
            {
                get { return _displayTarget.Width; }
            }
            private int targetHeight
            {
                get { return _displayTarget.Height; }
            }
            private void buildDXResources()
            {
                if (!_bDXSetup) return;

                releaseDXResources();
            }
            private void releaseDXResources()
            {
                if (!_bDXSetup) return;

                clearAllDynamicBrushes();               
            }
            private void clearAllDynamicBrushes()
            {
                if (!_bDXSetup || _DXBrushes == null) return;

                foreach (KeyValuePair<System.Drawing.Color, SharpDX.Direct2D1.Brush> kvp in _DXBrushes)
                {
                    SharpDX.Direct2D1.Brush b = kvp.Value;
                    Utilities.Dispose(ref b);
                    b = null;
                }

                _DXBrushes.Clear();
                _DXBrushes = null;
            }
            private SharpDX.Direct2D1.Brush getDXBrushForColour(System.Drawing.Color c, int replaceAlpha = -1)
            {
                if (!_bDXSetup) return null;

                if (_DXBrushes == null) _DXBrushes = new Dictionary<System.Drawing.Color, SharpDX.Direct2D1.Brush>();

                System.Drawing.Color newC;
                if (replaceAlpha >= 0 && replaceAlpha <= 255)
                    newC = System.Drawing.Color.FromArgb(replaceAlpha, c.R, c.G, c.B); // override the alpha
                else
                    newC = c;

                if (_DXBrushes.ContainsKey(newC)) return _DXBrushes[newC];

                SolidBrush sb = new SolidBrush(newC);
                SharpDX.Direct2D1.Brush b = convertBrush(sb);
                sb.Dispose();

                _DXBrushes.Add(newC, b);

                return b;
            }
            private SharpDX.Direct2D1.SolidColorBrush convertBrush(SolidBrush b)
            {
                return new SharpDX.Direct2D1.SolidColorBrush(_renderTarget, convertColour(b.Color));
            }
            private SharpDX.Color4 convertColour(System.Drawing.Color c)
            {
                return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }
            private void resizeDX()
            {
                lock (_DXlock)
                {
                    if (!_bDXSetup) return;

                    try
                    {
                        Utilities.Dispose(ref _renderTarget);
                        Utilities.Dispose(ref _surface);

                        _renderTarget = null;
                        _surface = null;

                        _device.ImmediateContext.ClearState();
                        _device.ImmediateContext.Flush();

                        _swapChain1.ResizeBuffers(_nBufferCount, targetWidth, targetHeight, _swapChain.Description.ModeDescription.Format, SwapChainFlags.None);

                        _surface = _swapChain1.GetBackBuffer<Surface>(0);

                        RenderTargetProperties rtp = new RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(_swapChain.Description.ModeDescription.Format, _ALPHA_MODE));
                        _renderTarget = new RenderTarget(_factory, _surface, rtp);

                        setupAliasing();
                    }
                    catch (Exception e)
                    {
                        ShutdownDX();
                        MessageBox.Show("DirectX resizeDX() Meter failure\n" + e.Message, "DirectX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            //
            private void target_Resize(object sender, System.EventArgs e)
            {
                resizeDX();
            }
            //
            private void drawMeters()
            {
                foreach (KeyValuePair<string, clsMeter> mkvp in _meters)
                {
                    clsMeter m = mkvp.Value;

                    float tw;
                    if (targetWidth >= targetHeight)
                        tw = targetHeight - 0.5f;
                    else
                        tw = targetWidth - 0.5f;

                    SharpDX.RectangleF rect = new SharpDX.RectangleF(0, 0, tw * m.XRatio, tw * m.YRatio);

                    if (m.RX == _rx)
                    {
                        // this rx
                        if (
                            ((m.TypeBitField & (int)MeterType.RX) == (int)MeterType.RX && !_mox) ||
                            ((m.TypeBitField & (int)MeterType.MOX) == (int)MeterType.MOX && _mox)
                            )
                        {
                            // display when rx only
                            _renderTarget.DrawRectangle(rect, getDXBrushForColour(System.Drawing.Color.LimeGreen), 2f);

                            //test
                            foreach(KeyValuePair<string, clsMeterItem> mikvp in mkvp.Value.MeterItems.OrderBy(mikvp => mikvp.Value.ZOrder))
                            {
                                clsMeterItem mi = mikvp.Value;

                                switch (mi.ItemType)
                                {
                                    case clsMeterItem.MeterItemType.H_BAR:
                                        renderHBar(rect, mi);
                                        break;
                                    case clsMeterItem.MeterItemType.SOLID_COLOUR:
                                        renderSolidColour(rect, mi);
                                        break;
                                    case clsMeterItem.MeterItemType.NEEDLE:
                                        renderNeedle(rect, mi);
                                        break;
                                    case clsMeterItem.MeterItemType.IMAGE:
                                        renderImage(rect, mi);
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            private float limit(float value, float min, float max)
            {
                if(value < min) value = min;
                if(value > max) value = max;
                return value;
            }

            private void renderHBar(SharpDX.RectangleF rect, clsMeterItem mi)
            {
                clsBarItem cbi = (clsBarItem)mi;

                float x = cbi.TopLeft.X * rect.Width;
                float y = cbi.TopLeft.Y * rect.Height;
                float w = (cbi.BottomRight.X * rect.Width) - x;
                float h = (cbi.BottomRight.Y * rect.Height) - y;

                SharpDX.RectangleF mirect = new SharpDX.RectangleF(x, y, w, h);
                _renderTarget.DrawRectangle(mirect, getDXBrushForColour(System.Drawing.Color.Red));

                PointF min, max;
                float percX, percY;

                if (cbi.ShowHistory)
                {
                    getPerc(cbi, cbi.MinHistory, out percX, out percY, out min, out max);
                    float minHistory_x = percX * w;
                    getPerc(cbi, cbi.MaxHistory, out percX, out percY, out min, out max);
                    float maxHistory_x = percX * w;

                    SharpDX.RectangleF history = new SharpDX.RectangleF(minHistory_x + x, y, maxHistory_x - minHistory_x, h);
                    _renderTarget.FillRectangle(history, getDXBrushForColour(cbi.HistoryColour));
                }

                getPerc(cbi, cbi.Value, out float pixel_x, out float pixel_y, out min, out max);
                pixel_x *= w;

                switch (cbi.Style)
                {
                    case clsBarItem.BarStyle.SolidFilled:
                        SharpDX.RectangleF barrect = new SharpDX.RectangleF(x, y, pixel_x, h);
                        _renderTarget.FillRectangle(barrect, getDXBrushForColour(cbi.Colour));
                        break;
                    case clsBarItem.BarStyle.Line:
                        _renderTarget.DrawLine(new SharpDX.Vector2(pixel_x + x, y), new SharpDX.Vector2(pixel_x + x, y + h), getDXBrushForColour(cbi.Colour), cbi.StrokeWidth);
                        break;
                }
            }
            private void renderSolidColour(SharpDX.RectangleF rect, clsMeterItem mi)
            {
                clsSolidColour sc = (clsSolidColour)mi;

                float x = sc.TopLeft.X * rect.Width;
                float y = sc.TopLeft.Y * rect.Height;
                float w = (sc.BottomRight.X * rect.Width) - x;
                float h = (sc.BottomRight.Y * rect.Height) - y;

                SharpDX.RectangleF rectSC = new SharpDX.RectangleF(x, y, w, h);
                _renderTarget.FillRectangle(rectSC, getDXBrushForColour(sc.Colour));
            }
            private void renderImage(SharpDX.RectangleF rect, clsMeterItem mi)
            {
                clsImage ipg = (clsImage)mi;

                float x = ipg.TopLeft.X * rect.Width;
                float y = ipg.TopLeft.Y * rect.Height;
                float w = (ipg.BottomRight.X * rect.Width) - x;
                float h = (ipg.BottomRight.Y * rect.Height) - y;

                string sImage = ipg.ImageName;

                if (_images.ContainsKey(sImage))
                {
                    if ((w < 150 || h < 150) && _images.ContainsKey(sImage + "_small")) sImage += "_small"; // use small version of the image if available

                    SharpDX.RectangleF imgRect = new SharpDX.RectangleF(x, y, w, h);

                    if (!ipg.Clipped)
                    {
                        _renderTarget.PushAxisAlignedClip(imgRect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle, no nee to cut the image
                    }
                    else
                    {
                        float cx = ipg.ClipTopLeft.X * rect.Width;
                        float cy = ipg.ClipTopLeft.Y * rect.Height;
                        float cw = (ipg.ClipBottomRight.X * rect.Width) - cx;
                        float ch = (ipg.ClipBottomRight.Y * rect.Height) - cy;

                        SharpDX.RectangleF clipRect = new SharpDX.RectangleF(cx, cy, cw, ch);
                        _renderTarget.PushAxisAlignedClip(clipRect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle, no nee to cut the image
                    }

                    SharpDX.Direct2D1.Bitmap b = _images[sImage];

                    // maintain aspect ratio, the clip removes anything outside the rect
                    float im_w = b.Size.Width;
                    float im_h = b.Size.Height;

                    if (w > h) 
                        imgRect.Height = imgRect.Width * (im_h / im_w);
                    else
                        imgRect.Width = imgRect.Height * (im_w / im_h);

                    _renderTarget.DrawBitmap(b, imgRect, 1f, BitmapInterpolationMode.Linear);//, sourceRect);

                    _renderTarget.PopAxisAlignedClip();
                }
            }
            private void renderNeedle(SharpDX.RectangleF rect, clsMeterItem mi)
            {
                clsNeedleItem ni = (clsNeedleItem)mi;

                float x = ni.TopLeft.X * rect.Width;
                float y = ni.TopLeft.Y * rect.Height;
                float w = (ni.BottomRight.X * rect.Width) - x;
                float h = (ni.BottomRight.Y * rect.Height) - y;

                SharpDX.RectangleF nirect = new SharpDX.RectangleF(x, y, w, h);
                //_renderTarget.DrawRectangle(nirect, getDXBrushForColour(System.Drawing.Color.Red));

                _renderTarget.PushAxisAlignedClip(nirect, AntialiasMode.Aliased); // prevent anything drawing from outside the rectangle

                //float startX = x + (w * ni.NeedleOffset.X);
                //float startY = y + (h * ni.NeedleOffset.Y);

                // needle offset from centre
                float cX = x + (w / 2);
                float cY = y + (h / 2);
                float startX = cX + (w * ni.NeedleOffset.X);
                float startY = cY + (h * ni.NeedleOffset.Y);

                float rotation = 180f;

                float radiusX = (w / 2) * (ni.LengthFactor * ni.RadiusRatio.X);
                float radiusY = (w / 2) * (ni.LengthFactor * ni.RadiusRatio.Y);

                //todo
                switch (ni.Placement)
                {
                    case clsNeedleItem.NeedlePlacement.Bottom:
                        rotation = 180f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Left:
                        //rotation = 90f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Top:
                        //rotation = 0f;
                        break;
                    case clsNeedleItem.NeedlePlacement.Right:
                        //rotation = 270f;
                        break;
                }

                float eX, eY, dX, dY;

                if (ni.ShowHistory || ni.Setup)
                {
                    float valueMin;
                    float valueMax;

                    if (ni.Setup) 
                    {
                        KeyValuePair<float, PointF> kvp;
                        kvp = ni.ScaleCalibration.OrderBy(p => p.Key).First();
                        valueMin = kvp.Key;
                        kvp = ni.ScaleCalibration.OrderByDescending(p => p.Key).First();
                        valueMax = kvp.Key;
                    }
                    else
                    {
                        valueMin = ni.MinHistory;
                        valueMax = ni.MaxHistory;
                    }

                    getPerc(ni, valueMin, out float minPercX, out float minPercY, out PointF minHistoryMin, out PointF minHistoryMax);
                    getPerc(ni, valueMax, out float maxPercX, out float maxPercY, out PointF maxHistoryMin, out PointF maxHistoryMax);

                    if (ni.Direction == clsNeedleItem.NeedleDirection.CounterClockwise)
                    {
                        minPercX = 1f - minPercX;
                        maxPercX = 1f - maxPercX;
                        minPercY = 1f - minPercY;
                        maxPercY = 1f - maxPercY;
                    }

                    // map the meter scales to pixels
                    float eXmin = x + (minHistoryMin.X * w) + (minPercX * ((minHistoryMax.X - minHistoryMin.X) * w));
                    float eYmin = y + (minHistoryMin.Y * h) + (minPercY * ((minHistoryMax.Y - minHistoryMin.Y) * h));
                    float eXmax = x + (maxHistoryMin.X * w) + (maxPercX * ((maxHistoryMax.X - maxHistoryMin.X) * w));
                    float eYmax = y + (maxHistoryMin.Y * h) + (maxPercY * ((maxHistoryMax.Y - maxHistoryMin.Y) * h));

                    // calc angle required
                    float dXmin = startX - eXmin;
                    float dYmin = startY - eYmin;
                    float dXmax = startX - eXmax;
                    float dYmax = startY - eYmax;
                    // expand for ellipse
                    dXmin /= ni.RadiusRatio.X;
                    dYmin /= ni.RadiusRatio.Y;
                    dXmax /= ni.RadiusRatio.X;
                    dYmax /= ni.RadiusRatio.Y;
                    //
                    float angMin = (float)Math.Atan2(dYmin, dXmin);
                    float angMax = (float)Math.Atan2(dYmax, dXmax);

                    float endMinX = startX + (float)(Math.Cos(angMin + degToRad(rotation)) * radiusX);
                    float endMinY = startY + (float)(Math.Sin(angMin + degToRad(rotation)) * radiusY);
                    float endMaxX = startX + (float)(Math.Cos(angMax + degToRad(rotation)) * radiusX);
                    float endMaxY = startY + (float)(Math.Sin(angMax + degToRad(rotation)) * radiusY);

                    PathGeometry sharpGeometry = new PathGeometry(_renderTarget.Factory);

                    GeometrySink geo = sharpGeometry.Open();
                    geo.BeginFigure(new SharpDX.Vector2(startX, startY), FigureBegin.Filled);

                    geo.AddLine(new SharpDX.Vector2(endMinX, endMinY));

                    ArcSegment arcSegment = new ArcSegment();
                    arcSegment.Point = new SharpDX.Vector2(endMaxX, endMaxY);
                    arcSegment.SweepDirection = ni.Direction == clsNeedleItem.NeedleDirection.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
                    arcSegment.ArcSize = Math.Abs(radToDeg(angMax) - radToDeg(angMin)) <= 180f ? ArcSize.Small : ArcSize.Large;
                    arcSegment.Size = new Size2F(radiusX, radiusY);
                    geo.AddArc(arcSegment);

                    geo.EndFigure(FigureEnd.Closed); // adds the closing line
                    geo.Close();

                    _renderTarget.FillGeometry(sharpGeometry, getDXBrushForColour(ni.HistoryColour));

                    Utilities.Dispose(ref geo);
                    geo = null;
                    Utilities.Dispose(ref sharpGeometry);
                    sharpGeometry = null;
                }

                getPerc(ni, ni.Value, out float percX, out float percY, out PointF min, out PointF max);
                if (ni.Direction == clsNeedleItem.NeedleDirection.CounterClockwise)
                {
                    percX = 1f - percX;
                    percY = 1f - percY;
                }

                // map the meter scales to pixels
                eX = x + (min.X * w) + (percX * ((max.X - min.X) * w));
                eY = y + (min.Y * h) + (percY * ((max.Y - min.Y) * h));

                // calc angle required
                dX = startX - eX;
                dY = startY - eY;
                // expand
                dX /= ni.RadiusRatio.X;
                dY /= ni.RadiusRatio.Y;
                float ang = (float)Math.Atan2(dY, dX);

                float endX = startX + (float)(Math.Cos(ang + degToRad(rotation)) * radiusX);
                float endY = startY + (float)(Math.Sin(ang + degToRad(rotation)) * radiusY);

                _renderTarget.DrawLine(new SharpDX.Vector2(startX, startY), new SharpDX.Vector2(endX, endY), getDXBrushForColour(ni.Colour), ni.StrokeWidth);

                if (ni.Setup)
                {
                    foreach (KeyValuePair<float, PointF> kvp in ni.ScaleCalibration)
                    {
                        PointF p = kvp.Value;
                        _renderTarget.FillEllipse(new Ellipse(new SharpDX.Vector2(x + (p.X * w), y + (p.Y * h)), 2f, 2f), getDXBrushForColour(System.Drawing.Color.Red));
                    }
                }

                _renderTarget.PopAxisAlignedClip();
            }
            private double degToRad(float deg)
            {
                return (deg * Math.PI) / 180f;
            }
            private double radToDeg(float rad)
            {
                return rad * (180f / Math.PI);
            }
            private void getPerc(clsMeterItem mi, float value, out float percX, out float percY, out PointF min, out PointF max)
            {
                percX = 0;
                percY = 0;
                min = new PointF(0f, 0f);
                max = new PointF(0f, 0f);

                value = (float)Math.Round(value, 3);

                if (mi.ScaleCalibration.Count > 0)
                {
                    // todo, only implemented for clockwise needles atm
                    PointF p_low = PointF.Empty;
                    PointF p_high = PointF.Empty;
                    float value_low = -200f;
                    float value_high = -200f;
                    bool bEqual = false;

                    if (mi.ScaleCalibration.ContainsKey(value))
                    {
                        p_low = mi.ScaleCalibration[value];
                        p_high = mi.ScaleCalibration[value];
                        value_low = value;
                        value_high = value;
                        bEqual = true;
                    }
                    else if (value < mi.ScaleCalibration.OrderBy(p => p.Key).First().Key) // below lowest
                    {
                        p_low = p_high = mi.ScaleCalibration.OrderBy(p => p.Key).First().Value;
                        value_low = value_high = mi.ScaleCalibration.OrderBy(p => p.Key).First().Key;
                        bEqual = true;
                    }
                    else if (value > mi.ScaleCalibration.OrderBy(p => p.Key).Last().Key) // above highest
                    {
                        p_low = p_high = mi.ScaleCalibration.OrderBy(p => p.Key).Last().Value;
                        value_low = value_high = mi.ScaleCalibration.OrderBy(p => p.Key).Last().Key;
                        bEqual = true;
                    }
                    else
                    {
                        //low side
                        foreach (KeyValuePair<float, PointF> kvp in mi.ScaleCalibration.OrderByDescending(p => p.Key))
                        {
                            if (kvp.Key <= value)
                            {
                                p_low = kvp.Value;
                                value_low = kvp.Key;
                                break;
                            }
                        }

                        // high
                        foreach (KeyValuePair<float, PointF> kvp in mi.ScaleCalibration.OrderBy(p => p.Key))
                        {
                            if (kvp.Key >= value)
                            {
                                p_high = kvp.Value;
                                value_high = kvp.Key;
                                break;
                            }
                        }
                    }

                    //get mins/maxs
                    float minX = (float)Math.Round(mi.ScaleCalibration.OrderBy(p => p.Value.X).First().Value.X, 3);
                    float maxX = (float)Math.Round(mi.ScaleCalibration.OrderByDescending(p => p.Value.X).First().Value.X, 3);

                    float minY = (float)Math.Round(mi.ScaleCalibration.OrderBy(p => p.Value.Y).First().Value.Y, 3);
                    float maxY = (float)Math.Round(mi.ScaleCalibration.OrderByDescending(p => p.Value.Y).First().Value.Y, 3);

                    float rangeX = maxX - minX;
                    float rangeY = maxY - minY;

                    if (bEqual)
                    {
                        percX = (p_low.X - minX) / rangeX;
                        percY = (p_low.Y - minY) / rangeY;
                    }
                    else
                    {
                        float value_range = value_high - value_low;
                        float ratio = (value - value_low) / value_range;
                        float newRangeX = p_high.X - p_low.X;
                        float newRangeY = p_high.Y - p_low.Y;

                        percX = ((p_low.X - minX) + (newRangeX * ratio)) / rangeX;
                        percY = ((p_low.Y - minY) + (newRangeY * ratio)) / rangeY;
                    }

                    percX = (float)Math.Round(percX, 3);
                    percY = (float)Math.Round(percY, 3);
                    min = new PointF(minX, minY);
                    max = new PointF(maxX, maxY);
                }
            }
            private SharpDX.Direct2D1.Bitmap bitmapFromSysBitmap(RenderTarget rt, System.Drawing.Bitmap bitmap)
            {
                System.Drawing.Rectangle sourceArea = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapProperties bitmapProperties = new BitmapProperties(new SharpDX.Direct2D1.PixelFormat(Format.B8G8R8A8_UNorm, _ALPHA_MODE)); //was R8G8B8A8_UNorm  //MW0LGE_21k9
                Size2 size = new Size2(bitmap.Width, bitmap.Height);

                // Transform pixels from BGRA to RGBA
                int stride = bitmap.Width * sizeof(int);
                DataStream tempStream = new DataStream(bitmap.Height * stride, true, true);

                // Lock System.Drawing.Bitmap
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(sourceArea, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                // Convert all pixels 
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int offset = bitmapData.Stride * y;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        byte B = Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte G = Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte R = Marshal.ReadByte(bitmapData.Scan0, offset++);
                        byte A = Marshal.ReadByte(bitmapData.Scan0, offset++);
                        //int rgba = R | (G << 8) | (B << 16) | (A << 24); //MW0LGE_21k9
                        //tempStream.Write(rgba);
                        int bgra = B | (G << 8) | (R << 16) | (A << 24);
                        tempStream.Write(bgra);
                    }

                }
                bitmap.UnlockBits(bitmapData);

                tempStream.Position = 0;

                SharpDX.Direct2D1.Bitmap dxBitmap = new SharpDX.Direct2D1.Bitmap(rt, size, tempStream, stride, bitmapProperties);

                Utilities.Dispose(ref tempStream);
                tempStream = null;

                return dxBitmap;
            }
        }        
        #endregion
    }
}