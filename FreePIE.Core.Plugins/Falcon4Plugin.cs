using System;
using System.Timers;

using FreePIE.Core.Contracts;

using F4SharedMem;
using F4SharedMem.Headers;
namespace FreePIE.Core.Plugins
{

    [GlobalType(Type = typeof(Falcon4Global))]
    public class Falcon4Plugin : Plugin
    {
        Reader _sharedMemReader = new Reader();
        private FlightData _lastFlightData;
        private Timer _timer = new Timer();

        private FlightData ReadSharedMem()
        {
            return _lastFlightData = _sharedMemReader.GetCurrentData();
        }
 
        private void OnTimer(Object source, ElapsedEventArgs e)
        {
            if (ReadSharedMem() != null)
            {
               
            }
            else
            {
                
            }
        }
 
        public override object CreateGlobal()
        {
            return new Falcon4Global(this);
        }

        public override string FriendlyName
        {
            get { return "falcon4"; }
        }

        public override Action Start()
        {
            _timer.Elapsed += new ElapsedEventHandler(OnTimer);
            _timer.Interval = 20;
           // _timer.Start();

            OnStarted(this, new EventArgs());
            return null;
        }

        public override void Stop()
        {

        }

        public override void DoBeforeNextExecute()
        {


        }

        public bool autopilot()
        {
            if (ReadSharedMem() != null)
            {
                var lightBits = (LightBits)_lastFlightData.lightBits;
                //var flystates = _lastFlightData.IntellivibeData.IsExitGame;
                //return (lightBits & LightBits.AutoPilotOn) == LightBits.AutoPilotOn;
                return _lastFlightData.IntellivibeData.In3D;
            }
            return false;
        }
         public bool IsInGame()
         {
             if (ReadSharedMem() == null || !_lastFlightData.IntellivibeData.In3D) return false;
             return true;
         }
    }

    [Global(Name = "falcon4")]
    public class Falcon4Global 
    {
        private readonly Falcon4Plugin plugin;

        public Falcon4Global(Falcon4Plugin plugin)
        {
            this.plugin = plugin;
        }
        public bool isInGame()
        {
            return plugin.IsInGame();
        }
    }
}

