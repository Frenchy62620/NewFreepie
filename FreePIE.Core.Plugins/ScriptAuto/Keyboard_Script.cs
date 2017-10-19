using System;
using System.Collections.Generic;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptKeyboard
    {
        private Dictionary<int, int> azerty = new Dictionary<int, int>()
        {
            { (int)Key.A, (int)Key.Q},
            { (int)Key.Q, (int)Key.A},
            { (int)Key.Z, (int)Key.W},
            { (int)Key.W, (int)Key.Z},
            { (int)Key.Semicolon, (int)Key.M},
            { (int)Key.Comma, (int)Key.Semicolon},
            { (int)Key.M, (int)Key.Comma}
        };
        private readonly KeyboardPlugin plugin;
        public ScriptKeyboard(KeyboardPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
        }

        //private int GetValueOf(string enumName, string enumConst)
        //{
        //    Type enumType = Type.GetType(enumName);
        //    if (enumType == null)
        //    {
        //        throw new ArgumentException("Specified enum type could not be found", "enumName");
        //    }

        //    object value = Enum.Parse(enumType, enumConst);
        //    return Convert.ToInt32(value);
        //}
        public void Z()       //KZ;k1:k2:..  = D + U
        {
            foreach (var cd in Gx.wd[1].Split(':'))
                plugin.setKeyPressedStrategy.Add(Convert.ToInt32(cd));
            Gx.NextAction();
        }
        public void D()       //KD;k1:k2:..  keyDown
        {
            foreach (var cd in Gx.wd[1].Split(':'))
                plugin.KeyDown(Convert.ToInt32(cd));
            Gx.NextAction();
        }
        public void U()       //KU;k1:k2:..  keyUp  
        {
            foreach (var cd in Gx.wd[1].Split(':'))
                plugin.KeyUp(Convert.ToInt32(cd));
            Gx.NextAction();
        }
        public void H()       //KH;k1:k2:...   test if keyup
        {
            foreach (var cd in Gx.wd[1].Split(':'))
                if (plugin.IsKeyDown(Convert.ToInt32(cd))) return;
            Gx.NextAction();
        }
        public void P()   //KP;kcode     wait key pressed
        {
            if (plugin.IsPressed(Convert.ToInt32(Gx.wd[1]))) Gx.NextAction();
        }
        public void R()   //KR;kcode     wait key released
        {
            if (plugin.IsReleased(Convert.ToInt32(Gx.wd[1]))) Gx.NextAction();
        }
        public void L()       //KL;etiq    is led on? goto etiq  
        {
            if (plugin.isLedScrollLockOn())
            {
                string.Format("G0;{0}", Gx.wd[1]).DecodelineOfCommand(section: null, priority: 3);
                return;
            }
            Gx.NextAction();
        }

        public void K()       //KKx;                wait key from keyboard
        {
            var flagazerty = (Gx.subcmd2 & 1) != 0;     // azerty                               1
            var flagsemicolon = (Gx.subcmd2 & 2) != 0;  //rajoute un ; en fin de chaine         2
            var flagalpha = (Gx.subcmd2 & 4) != 0;      // utilise [A-Z]    en plus des nombres 4

            for (int k = (int)Key.A; k <= (int)Key.Semicolon; k++)
            {
                if (k == 36)
                    k = (int)Key.Comma;
                else if (k == 52)
                    k = (int)Key.NumberPad0;
                else if (k == 107)
                    k = (int)Key.Semicolon;

                if (flagalpha && ((k >= (int)Key.A && k <= (int)Key.Z) || k == (int)Key.Comma || k == (int)Key.Semicolon))
                {
                    var x = azerty.ContainsKey(k) ? azerty[k] : k;
                    var y = flagazerty ? x : k;

                    if (plugin.IsPressed(k))
                    {
                        switch ((Key)y)
                        {
                            case Key.Comma:   //
                                true.PlaySound(1000, 600);
                                Gx.keystosay = flagazerty ? "virgule" : "comma";
                                Gx.keystyped += ",";
                                return;
                            case Key.Semicolon:   //
                                true.PlaySound(1000, 600);
                                Gx.keystyped += ";";
                                return;
                            default:
                                Gx.keystosay = ((char)(y + 55)).ToString();
                                Gx.keystyped += Gx.keystosay;
                                return;
                        }
                    }
                }

                if ((k >= (int)Key.NumberPad0 && k <= (int)Key.NumberPadStar)
                    || k == (int)Key.Delete
                    || k == (int)Key.End)
                    if (plugin.IsPressed(k))
                    {
                        switch ((Key)k)
                        {
                            case Key.NumberPadEnter:   //numpad Enter = validate
                                BeepPlugin.BackgroundBeep.Beep(1000, 600);
                                //true.PlaySound(1000, 600);
                                if (flagsemicolon) Gx.keystyped += ';';
                                Gx.NextAction();
                                return;
                            case Key.NumberPadMinus:   //numpad - = signe * (code ascii 45)
                                                       //true.PlaySound(600, 1000);
                                Gx.keystosay = flagazerty ? "moins" : "minus";
                                Gx.keystyped += "-";
                                return;
                            case Key.NumberPadPlus:   //numpad + = ; (code ascii 59)
                                true.PlaySound(300, 300);
                                Gx.keystyped += ";";
                                return;
                            case Key.NumberPadPeriod:   //numpad . = ; (code ascii 59)
                                Gx.keystosay = flagazerty ? "point" : "dot";
                                Gx.keystyped += ".";
                                return;
                            case Key.Delete:   //delete = correction
                                Gx.keystosay = "correction";
                                if (Gx.keystyped.Length > 0)
                                    Gx.keystyped = Gx.keystyped.Substring(0, Gx.keystyped.Length - 1);
                                return;
                            case Key.End:   //End = raz
                                            // true.PlaySound(100, 600);
                                Gx.keystosay = flagazerty ? "effacer" : "clear";
                                Gx.keystyped = "";
                                return;
                            case Key.NumberPadStar:   //numpad * = *
                                Gx.keystosay = "*";
                                Gx.keystyped += "*";
                                return;
                            case Key.NumberPadSlash:   //numpad / = sayall
                                Gx.keystosay = Gx.keystyped;
                                return;
                            default:
                                Gx.keystosay = ((char)(k - 41)).ToString();
                                Gx.keystyped += Gx.keystosay;
                                return;
                        }
                    }
            }
        }
        public void I()       //if KI;@><=:val:etiq
        {
            var val = Gx.wd[1].Split(':');
            var value = Gx.getDataTyped();

            if (val[0][0] == '@')
            {
                if (value.Contains(val[1]))
                {
                    string.Format("G0;{0}", val[2]).DecodelineOfCommand(section: null, priority: 3);
                    return;
                }
                Gx.NextAction();
                return;
            }

            var vv = Convert.ToInt32(value);
            var v = Convert.ToInt32(val[1]);
            if (val[0].Contains("="))
            {
                if (v == vv)
                {
                    string.Format("G0;{0}", val[2]).DecodelineOfCommand(section: null, priority: 3);
                    return;
                }
                if (val[0].Length == 1)
                {
                    Gx.NextAction();
                    return;
                }
            }

            if (vv == v || (val[0].Contains(">") && vv < v) || (val[0].Contains("<") && vv > v))
            {
                Gx.NextAction();
                return;
            }

            string.Format("G0;{0}", val[2]).DecodelineOfCommand(section: null, priority: 3);
            return;
        }
    }
}


