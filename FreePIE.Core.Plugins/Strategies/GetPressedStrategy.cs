using System;
using System.Collections.Generic;
using System.Collections;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.Strategies
{
    using Gx = GlobalExtensionMethods;

    public class GetPressedStrategy<T>
    {
        private readonly Func<T, bool, bool> isDown;
        private readonly Dictionary<T, State> dico;// [ T ,[bool pressed, bool released] ]
        public GetPressedStrategy(Func<T, bool, bool> isDown)
        {
            this.isDown = isDown;
            dico = new Dictionary<T, State>();
        }
        private class State
        {
            public BitArray b;
            public long[] timer;
            public int h;
            public State()
            {
                b = new BitArray(2, false);         // 0 = pressed/clik, 1 = released/click
                timer = new long[3]{-1, -1, -1};    // 0 = 1clik, 1 = 2clik, 2 = held
                h = -1;                             // mutliple value Heldown
            }

            //public State(int numtimer)
            //{
            //    b = new BitArray(2, false);
            //    timer = new long[3];    // 0 = 1clik, 1 = 2clik, 2 = held
            //    timer[numtimer] = Gx.StartCount();
            //}
        }
        public bool IsPressed(T code, bool value = false)
        {
            State val;
            bool previouslyPressed = dico.TryGetValue(code, out val) && val.b[0];
            if (val == null)
                dico[code] = (val = new State());
            else
                val.b[0] = isDown(code, value);

            return previouslyPressed ? false : val.b[0];
        }
        public bool IsReleased(T code, bool value = false)
        {
            State val;
            bool previouslyPressed = dico.TryGetValue(code, out val) && val.b[1];
            if (val == null)
                dico[code] = (val = new State());
            else
                val.b[1] = isDown(code, value);

            return !previouslyPressed ? false : !val.b[1];
        }
        public bool IsSingleClicked(T code, bool value = false)
        {
            if (IsPressed(code, value))
            {
                var d = dico[code];
                d.timer[0] = Gx.StartCount();
                return false;
            }
            else if (IsReleased(code, value))
            {
                var d = dico[code];
                var lapse = d.timer[0].GetLapse();
                d.timer[0] = Gx.StopCount();
                return lapse <= Gx.lapse_singleclick;
            }
            else
                return false;
        }
        public bool IsDoubleClicked(T code, bool value = false)
        {      
            if (IsSingleClicked(code, value))
            {
                var d = dico[code];
                if (d.timer[1] < 0)
                {
                    d.timer[1] = Gx.StartCount();
                }
                else
                {
                    var lapse = d.timer[1].GetLapse();
                    d.timer[1] = Gx.StopCount();
                    return lapse <= Gx.lapse_singleclick;
                }
            }
            else
            {
                var d = dico[code];
                if (d.timer[1] >= 0 && d.timer[1].GetLapse() > Gx.lapse_singleclick)
                {
                    d.timer[1] = Gx.StopCount();
                }
            }
            return false;
        }

        public bool IsHelDowned(T code, bool value, long duration)
        {
            if (IsPressed(code, value))    // || (isDown(code, value) && dico[code].timer[2] < 0))
            {
                dico[code].timer[2] = Gx.StartCount();
                return false;
            }
            if (IsReleased(code, value))   // || (!isDown(code, value) && dico[code].timer[2] > 0))
            {
                dico[code].timer[2] = Gx.StopCount();
                return false;
            }
            if (value && dico[code].timer[2] >= 0)
                return dico[code].timer[2].GetLapse() >= duration;

            return false;
        }

        public int IsHelDowned(T code, bool value, long[] durations)
        {
            State val;
            if (!dico.TryGetValue(code, out val))
            {
                dico[code] = (val = new State());
                return -1;
            }

            int v = durations.Length;
            long dur = dico[code].timer[2].GetLapse();

            if (isDown(code, value))
            {
                if (dico[code].timer[2] < 0)
                    dico[code].timer[2] = Gx.StartCount();
                else
                {
                    for (int i = 0; i < v; i++)
                        if (dur <= durations[i])
                        {
                            v = i;
                            break;
                        }
                    if (dico[code].h != v)
                    {
                        dico[code].h = v;
                        return v * 10;
                    }
                }
            }
            else
            {
                if (dico[code].timer[2] >= 0)
                {
                    dico[code].timer[2] = Gx.StopCount();
                    for (int i = 0; i < v; i++)
                        if (dur <= durations[i])
                        {
                            v = i;
                            break;
                        }
                    dico[code].h = -1;
                    return v;
                }
            }
            return -1;
        }
        //public int IsHelDowned(T code, bool value, long [] durations)
        //{
        //    if (IsPressed(code, value))    // || (isDown(code, value) && dico[code].timer[2] < 0))
        //    {
        //        dico[code].timer[2] = Gx.StartCount();
        //        return -1;
        //    }
        //    if (IsReleased(code, value))   // || (!isDown(code, value) && dico[code].timer[2] > 0))
        //    {
        //        dico[code].timer[2] = Gx.StopCount();
        //        if (value && dico[code].timer[2] >= 0)
        //            for (int i = 0; i < durations.Length; i++)
        //                if (dico[code].timer[2].GetLapse() <= durations[i])
        //                    return i;
        //            return durations.Length;
        //    }
        //    if (value && dico[code].timer[2] >= 0)
        //        for (int i = durations.Length - 1; i >= 0; i--)
        //            if (dico[code].timer[2].GetLapse() > durations[i])
        //                Gx.DecodelineOfCommand("S");
        //                return i;
        //    return -1;
        //}
        public bool Repeated(T code, bool value, long duration)
        {
            if (IsHelDowned(code, value, duration))
            {
                dico[code].timer[2] = Gx.ReStartCount();
                return true;
            }
            return false;
        }
    }
}