using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.Strategies;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.ScriptEngine.Globals.ScriptHelpers;

namespace FreePIE.Core.Plugins
{
    using Gx = GlobalExtensionMethods;

    [GlobalType(Type = typeof(VarGlobal))]
    public class VarPlugin : Plugin
    {
       // private readonly Dictionary<string, Stopwatch> stopwatches;
        private GetPressedStrategy<string> getVarPressedStrategy;
        public VarPlugin()
        {
            getVarPressedStrategy = new GetPressedStrategy<string>(IsVarDown);
        }
        public override object CreateGlobal()
        {
            return new VarGlobal(this);
        }

        public override string FriendlyName => "var";

        public bool IsPressed(bool value, string indexer) => getVarPressedStrategy.IsPressed(indexer, value);
        public bool IsReleased(bool value, string indexer) => getVarPressedStrategy.IsReleased(indexer, value);
        public bool IsVarDown(string indexer, bool value) => value;

        //begin single and double click

        public void SetLapseSingleClick(int value)
        {
            Gx.lapse_singleclick = value;
        }

        public bool IsSingleClicked(bool value, string indexer) => getVarPressedStrategy.IsSingleClicked(indexer, value);
        public bool IsDoubleClicked(bool value, string indexer) => getVarPressedStrategy.IsDoubleClicked(indexer, value);

        //end single and double click
        public bool IsHeldDown(bool value, int milliseconds, string indexer) => getVarPressedStrategy.IsHelDowned(indexer, value, milliseconds);
        public int IsHeldDown(bool value, long[] lapse, string indexer) => getVarPressedStrategy.IsHelDowned(indexer, value, lapse);

        public bool Repeat(bool value, int milliseconds, string indexer)
        {
            /*
                return true every lapse of milliseconds
            */
            return getVarPressedStrategy.Repeated(indexer, value, milliseconds);
        }
        //public int Get4Direction(float value, Axis a, int XorY, int Y = 0)
        //{
        //    switch (a)
        //    {
        //        case Axis.X:
        //        case Axis.Y:
        //            return FindDirection(value, a, XorY);
        //        case Axis.XY:
        //            return Get8Direction(value, XorY, Y, true);
        //        default:
        //            return -1;
        //    }
        //}
        //public int Get8Direction(float value, int X, int Y, bool fourdir = false)
        //{
        //    int x = FindDirection(value, Axis.X, X), y = FindDirection(value, Axis.Y, Y);
        //    if (x < 0) return y;
        //    if (y < 0) return x;
        //    if (fourdir) return -1;
        //    // x , y         x , y
        //    // 1 , 0 -> 4 ;  1 , 2 -> 5
        //    // 3 , 2 -> 6 ;  3 , 0 -> 7
        //    return x == 1 ? y / 2 + 4 : 7 - y / 2;
        //}
        //private int FindDirection(float value, Axis a, int XorY)
        //{
        //    if (Math.Abs(XorY) < value) return -1;
        //    return  XorY > 0? (int)a - 1 :(int)a + 1;
        //}

        public void SendCommand(string command, string section, int priority)
        {
            command.DecodelineOfCommand(section, priority);
        }
    }

    [Global(Name = "var")]
    public class VarGlobal
    {
        private readonly VarPlugin plugin;
        public VarGlobal(VarPlugin plugin)
        {
            this.plugin = plugin;
        }

        // *************** Pressed **************************************************************
        [NeedIndexer]
        public bool getPressedBip(bool value, int frequency, int duration, string indexer)
        {
            return plugin.IsPressed(value, indexer).PlaySound(frequency, duration);
        }

        [NeedIndexer]
        public bool getPressedSound(bool value, int id, string indexer)
        {
            return plugin.IsPressed(value, indexer).PlaySound(id);
        }
        [NeedIndexer]
        public bool getPressedSound(bool value, string audio, string indexer)
        {
            return plugin.IsPressed(value, indexer).PlaySound(audio);
        }

        [NeedIndexer]
        public bool getPressed(bool value, string indexer)
        {
            return plugin.IsPressed(value, indexer);
        }
        // *************** Released **************************************************************
        [NeedIndexer]
        public bool getReleasedBip(bool value, int frequency, int duration, string indexer)
        {
            return plugin.IsReleased(value, indexer).PlaySound(frequency, duration);
        }
        [NeedIndexer]
        public bool getReleasedSound(bool value, int id, string indexer)
        {
            return plugin.IsReleased(value, indexer).PlaySound(id);
        }

        [NeedIndexer]
        public bool getReleased(bool value, string indexer)
        {
            return plugin.IsReleased(value, indexer);
        }
        [NeedIndexer]
        public List<bool> getStates(bool value, int state /* 1 down 2 Pressed, 4 Released */, string indexer)
        {
            List<bool> b = new List<bool>();
            if ((state & 0x01) != 0) b.Add(value);
            if ((state & 0x02) != 0) b.Add(plugin.IsPressed(value, indexer));
            if ((state & 0x04) != 0) b.Add(plugin.IsReleased(value, indexer));
            return b;
        }
        // *************** 3 states, down, pressed and released *********************************
        //[NeedIndexer]
        //public List<bool> get3States(bool var, string indexer)
        //{
        //    return new List<bool>() { 
        //        var, 
        //        plugin.IsPressed(var, indexer), 
        //        plugin.IsReleased(var, indexer)
        //    };
        //}
        // *************** 2 states, down and pressed  *********************************
        //[NeedIndexer]
        //public List<bool> get2States(bool var, string indexer)
        //{
        //    return new List<bool>() {
        //        var,
        //        plugin.IsPressed(var, indexer),
        //    };
        //}
        // *************** get 4 or 8 directions from differents inputs (pov, buttons, axis.. ****************************

        // pov value to 8 directions
        // result is int = -1 (neutral), 0 (up), 4 (up/right), 1 (right), 5 (right/down), 
        //                               2 (down), 6 (down/left), 3 (left), 7 (left/up)
        //public int get8Ways(int pov)
        //{
        //    if (pov < 0) return -1;
        //    pov = pov / 4500;
        //    return pov % 2 == 0 ? pov / 2 : (pov / 2) + 4;
        //}

        // x, y to 8 directions
        // result is int = -1 (neutral), 0 (up), 4 (up/right), 1 (right), 5 (right/down), 
        //                               2 (down), 6 (down/left), 3 (left), 7 (left/up) 
        //public int get8Ways(float value, int X, int Y)
        //{
        //    return plugin.Get8Direction(value, X, Y);
        //}
        // x, y to 4 directions
        // return x or y, if x and y are != -1 then result = -1
        //public int get4Ways(float value, Axis a, int XorY, int Y = 0)
        //{
        //    return plugin.Get4Direction(value, a, XorY, Y);
        //}
        // pov value to 4 directions
        // result is int = -1 (neutral), 0 (up), 1 (right), 2 (down) or 3 (right)
        //public int get4Ways(int pov)
        //{
        //    return pov > 0 ? pov / 9000 : pov;
        //}
        // button state to 4 directions only one button have to be true
        // result is int = -1 (neutral), 0 (up), 1 (right), 2 (down) , 3 (right)
        //public int get4Ways(IList<bool> state)
        //{
        //    for (int i = 0; i < 4; i++)
        //    {
        //        if (state[i])
        //        {
        //            for (int j = i + 1; j < 4; j++)
        //                if (state[j]) return -1;
        //            return i;
        //        }
        //    }
        //    return -1;
        //}

        // *************** single ou double Clicked **************************************************************
        [NeedIndexer]
        public bool getClicked(bool value, bool doubleclick, string indexer)
        {
            return doubleclick ? plugin.IsDoubleClicked(value, indexer) : plugin.IsSingleClicked(value, indexer);
        }
        [NeedIndexer]
        public bool getClickedBip(bool value, bool doubleclick, int frequency, int duration, string indexer)
        {
            return doubleclick ? plugin.IsDoubleClicked(value, indexer).PlaySound(frequency, duration) : plugin.IsSingleClicked(value, indexer).PlaySound(frequency, duration);
        }
        // *************** heldDown **************************************************************
        [NeedIndexer]
       public bool getHeldDown(bool value, int lapse, string indexer)
       {
           return plugin.IsHeldDown(value, lapse, indexer);
       }
        [NeedIndexer]
        public int getHeldDown(bool value, IList<int> duration, string indexer)
        {
            long[] durations = new long[duration.Count];
            for (int i = 0; i < duration.Count; i++)
                durations[i] = duration[i];
            return plugin.IsHeldDown(value, durations, indexer);
        }

        [NeedIndexer]
        public bool repeat(bool value, int lapse, string indexer)
       {
           return plugin.Repeat(value, lapse, indexer);
       }

        // *************** change value duration sglClick ****************************************
        public void wait(int time)
        {
            Thread.Sleep(time);
        }
        public int lapseSingleClick
        {
            set { plugin.SetLapseSingleClick(value); }
        }
        // *************** call command file ****************************************
        public void sendCommand(string command, string section = "", int priority = 0)
        {
            plugin.SendCommand(command, section, priority);
        }
        // *************** send command to python script ********************************
        public bool existCommand()
        {
            if (Gx.subcmd1 != 'L' || Gx.cmd != 'K') return false;
            return true;
        }
        public string Cmd
        {
            get
            {
                var cmd = Gx.wd[1];
                Gx.NextAction();
                return cmd;
            }
        }
    }
}
