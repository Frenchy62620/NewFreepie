using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FreePIE.Core.Common.Extensions;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptDCS
    {
        private readonly DCSPlugin plugin;
        public ScriptDCS(DCSPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
        }

        public void X()   // send command to DCS  DX;command
        {
            plugin.SendDCSCommand(Gx.wd[1]);
            Gx.NextAction();
        }

        public void W()   // DW;CDU_:500:SYS:LSK_3R  -> T0;300!DX;CDU_SYS 1!T0;500!DX;CDU_SYS 0!T0;500!CDU_LSK_3R 1!T0;500!CDU_LSK_3R 0
                          // Dw;CDU_SYS:500          -> T0;300!DX;CDU_SYS 1!T0;500!DX;CDU_SYS 0 
                          // DW;CDU_SYS              -> T0;300!DX;CDU_SYS 1!T0;300!DX;CDU_SYS 0
        {
            var val = Gx.wd[1].Split(':');
            string cmd;
            switch (val.Length)
            {
                case 1:
                case 2:
                    cmd = string.Format("T0;300!DX;{0} 1!T0;{1}!DX;{0} 0", val[0], val.Length == 2 ? val[1] : "300");
                    break;
                default:
                    StringBuilder format = new StringBuilder("T0;300", (val.Length - 2) * 64);
                    for (int i = 2; i < val.Length; i++)
                        format.AppendFormat("!DX;{0}{2} 1!T0;{1}!DX;{0}{2} 0!T0;{1}", val[0], val[1], val[i]);

                    cmd = format.ToString();
                    break;
            }
            cmd.DecodelineOfCommand(section: null, priority: 3);
        }

        public void F()   // DF;CDU_:500:SYS 1:LSK_3R 0 -> T0;300!DX;CDU_SYS 1!T0;500!DX;CDU_LSK_3R 0
        {
            var val = Gx.wd[1].Trim().Split(':');
            StringBuilder cmd = new StringBuilder("T0;300", (val.Length - 2) * 32);
            for (var i = 2; i < val.Length; i++)
            {
                cmd.AppendFormat("!DX;{0}{2}!T0;{1}", val[0], val[1], val[i]);
            }
            cmd.ToString().DecodelineOfCommand(section: null, priority: 3);
        }

        private int Rotateur(out string cmde, out int nowpos)
        {
            var buffer = Gx.keystyped.Split('.');
            var v = Gx.wd[1].Split(':');
            var x = v[0].Split(',');

            //var indice = Convert.ToInt32(v[2]);
            var siz = v[2].Split('.').Select(c => Convert.ToInt32(c)).ToArray();
            var pos = v[3].Split('.').Select(c => Convert.ToInt32(c)).ToArray();

            var a = new string('0', siz[0]) + buffer[0];
            var b = siz[1] == 0 ? "" : buffer[1] + new string('0', siz[1]);
            var f = a.Substring(a.Length - siz[0], siz[0]) + (siz[1] == 0 ? "" : b.Substring(0, siz[1]));

            cmde = v[1];
            if (x.Length > 2)
            {
                var address = Convert.ToUInt32(x[0]);
                var mask = Convert.ToInt32(x[1]);
                var shift = Convert.ToInt32(x[2]);
                nowpos = plugin.GetData(address, mask, shift);
            }
            else
            {
                nowpos = -1;
            }

            var result = f.Substring(pos[0], pos[1]);
           // var newpos = Gx.SearchPos(v[5], result, pos[1]);

            if (v[4].Contains('-'))
            {
                var s = Convert.ToInt32(result);
                var bornes = v[4].Split('-').Select(c => Convert.ToInt32(c)).ToArray();
                if (s < bornes[0] || s > bornes[1])
                    return -1;
                return s - bornes[0];
            }
            var tab = v[4].Split(',').Select(s =>
            {
                var st = new string('0', pos[1]) + s;
                return st.Substring(st.Length - pos[1], pos[1]);
            }).ToArray();
            for (int indice = 0; indice < tab.Count(); indice++)
            {
                if (tab[indice].Equals(result))
                    return indice;
            }
            return -1;
        }
        public void U()   // Set state DU;0:cmde:lg.lg:pos.lg:data0-data1,...  launch 0 then..
        {
            // gestion des rotateurs acces direct
            if (string.IsNullOrWhiteSpace(Gx.keystyped))
            {
                Gx.NextAction();
                return;
            }

            string cmde;
            int dumb;
            var newpos = Rotateur(out cmde, out dumb );

            if (newpos < 0)
            {
                Console.WriteLine("bad frequency");
                Gx.NextAction();
                return;
            }
            var cmd = $"DX;{cmde} {newpos}!T0;500";
            cmd.DecodelineOfCommand(section: null, priority: 3);
        }
        public void V()
        // DEC or INC Command no test 
        //  DV:address,mask,shift:cmde:3.3:pos.nbrchar:data0....."[3-15][0-9].[0-9][0,25,50,75][0-9][0-100]"
        {
            // gestion des rotateurs incrementation ou decrementation
            string cmde;
            int nowpos;
            if (string.IsNullOrWhiteSpace(Gx.keystyped))
            {
                Gx.NextAction();
                return;
            }
            var newpos = Rotateur(out cmde, out nowpos);

            if (newpos == nowpos || newpos < 0) //newpos < 0 = bad freq number
            {
                if (newpos < 0) Console.WriteLine("bad frequency");
                Gx.NextAction();
            }
            else
            {
                var action = newpos < nowpos ? "DEC" : "INC";
                var cmd = new StringBuilder("T0;300", 320);
                for (int i = 0; i < Math.Abs(newpos - nowpos); i++)
                    cmd.AppendFormat("!DX;{0} {1}!T0;500", cmde, action);
                cmd.ToString().DecodelineOfCommand(section: null, priority: 3);
            }
        }

        private void param(string val, out uint address, out int lg, out int mask, out int shift)
        {
            var v = val.Split(',');
            address = Convert.ToUInt32(v[0]);
            lg = Convert.ToInt32(v[1]);
            mask = lg;
            shift = v.Length == 3 ? Convert.ToInt32(v[2]) : lg;
        }

        public void B()   
        {
            // indextokeep = 0,1,0-6   char 0, 1 and 0 to 6th char, indextokeep = nothing -> all character selected
            //DB;separator$indextokeep:address,lg or address, mask, shift
            //DB;separator$:address,lg or address, mask, shift
            //DB;$indextokeep:address,lg or address, mask, shift
            //DB;$:address,lg or address, mask, shift
            char[] data;
            uint address;
            int lg, mask, shift;

            var v = Gx.wd[1].Split('$');    // isolate separator
            var separator = v[0];
            var xval = v[1].Split(':');

            param(xval[1], out address, out lg, out mask, out shift);
            data = v.Length == 2 ? plugin.GetData(address, lg).ToCharArray() : plugin.GetData(address, mask, shift).ToString().ToCharArray();
            var index = string.IsNullOrEmpty(xval[0]) ? $"0-{lg - 1}" : xval[0]; // char debut - fin

            var selchr = index.Split(',');
            List<int> newstring = new List<int>();
            foreach (var sa in selchr)
            {
                if (sa.Contains('-'))
                {
                    var c = sa.Split('-').Select(q => Convert.ToInt32(q)).ToArray();
                    for (int i = c[0]; i <= c[1]; i++)
                    {
                        if (i >= lg) break;
                         newstring.Add(i);
                    }
                }
                else
                {
                    var c = Convert.ToInt32(sa);
                    if (c < lg)
                        newstring.Add(c);
                }
            }
            Gx.keystyped = string.Join(separator, newstring.Select(q => data[q]).ToArray());
            Gx.NextAction();
        }

        public void O()       //If search True goto etiq else wait true
                              //op = [>, <, >=, <=, ==, <>,><]
                              //DO;:op:value1, valu2:etiq       test par rapport buffer

        //DO;:@C:value:etiq
        //DO;:@D:value:etiq
        //DO;:@Ixx:value:etiq       test char at place xx
        //DO;:@Lxx,op:value:etiq    test lgbuf xx op value
        // ---------------------------
        //DO;adr, mask, shift:op:value:etiq
        //DO;adr, mask, shift:op:value1,value2:etiq
        // ------ contains ----------------
        //DO;adr, lg:@C:value:etiq
        //DO;adr_deb, lg, adr_fin:@C:value:etiq;
        // ------- doesnt contain ------------
        //DO;adr, lg:@D:value:etiq
        //DO;adr_deb, lg, adr_fin:@D:value:etiq;
        // -------- test char at I place in adr,lg --------
        //DO;adr, lg:@Ixx:value:etiq
        //DO;adr_deb, lg, adr_fin:@Ixx:value:etiq
        // ---------test lg ------------
        //DO;adr, lg:@Lxx,op:value:etiq
        //DO;adr_deb, lg, adr_fin:@Lxx,op:value:etiq
        {
            var val = Gx.wd[1].Split(':'); var v = val[0].Split(','); var op = val[1];
            var etiq = val.Length == 4 ? val[3] : "";
            var result = false;
            switch (v.Length)
            {
                case 1: // buffer?
                    //DO;:op:value:etiq       test par rapport buffer op = [>, <, >=, <=, ==, <>]
                    //DO;:@C:value:etiq
                    //DO;:@D:value:etiq
                    //DO;:@Ixx:value:etiq       test char at place xx
                    //DO;:@Lxx,op:value:etiq    test lgbuf xx op value
                {
                    var value = Convert.ToInt32(val[2]);
                    result =
                        Gx.getDataTyped()
                            .Split(';')
                            .Any(
                                e =>
                                    op.Contains('@')
                                        ? Gx.Compare(op, e, val[2])
                                        : Gx.Compare(op, Convert.ToInt32(e), value));
                    break;
                }
                case 2://adress ,lg
                {
                    var adr_deb = Convert.ToUInt32(v[0]);
                    result = Gx.Compare(op, plugin.GetData(adr_deb, Convert.ToInt32(v[1])), val[2]);
                    break;
                }
                case 3://adress , mask, shift
                    {
                        var adr_deb = Convert.ToUInt32(v[0]);
                        if (op.Contains('@'))
                        {
                            var adr_fin = Convert.ToInt32(v[2]);
                            var pas = Convert.ToInt32(v[1]);
                            for (uint i = adr_deb; i <= adr_fin; i = i + (uint)pas)
                            {
                                result = Gx.Compare(op, plugin.GetData(i, pas), val[2]);
                                if (result) break;
                            }
                        }
                        else
                        {
                            var mask = Convert.ToInt32(v[1]);
                            var shift = Convert.ToInt32(v[2]);
                            var data = plugin.GetData(adr_deb, mask, shift);
                            if (val[2].Contains(','))   // >< ?
                            {
                                var value = val[2].Split(',').Select(c => Convert.ToInt32(c)).ToArray();
                                result = Gx.Compare(">", data, value[0]) && Gx.Compare("<", data, value[1]);
                                break;
                            }

                            result = Gx.Compare(op, data, Convert.ToInt32(val[2]));
                        }
                        break;
                    }
            }
            if (string.IsNullOrEmpty(etiq))     // wait true to continue
            {
                if (result)
                    Gx.NextAction();
            }
            else                                // if true goto etiq else continue next action
            {
                if (result)
                {
                    string.Format("G0;{0}", etiq).DecodelineOfCommand(section: null, priority: 3);
                    return;
                }
                Gx.NextAction();
            }
        }

        public void H()               //DH;address:mask:shift:CRS or HDG   HSI_CRS_KNOB = 4442 HSI_HDG_KNOB = 4444
        {
            var newHeadingDegree = Convert.ToInt32(Gx.getDataTyped());
            var val = Gx.wd[1].Split(':');
            var address = Convert.ToUInt32(val[0]);
            var mask = Convert.ToInt32(val[1]);
            var shift = Convert.ToInt32(val[2]);
            var returnToZero = ((int)(-plugin.GetData(address, mask, shift) * 360.0f / 57.29f));
            var newHeadingValue = (int)((newHeadingDegree * (65535 / 57.29f)));
            string cmd = string.Format("DX;HSI_{2}_KNOB {0}!T0;100!DX;HSI_{2}_KNOB {1}", returnToZero, newHeadingValue, val[3]);
            cmd.DecodelineOfCommand(section: null, priority: 3);
        }

        public void P()             //DP;address1:address2:lapse   course = 4442 heading = 4444
        {
            var val = Gx.wd[1].Split(':').Select(q => Convert.ToInt32(q)).ToArray();
            int k = 1;
            int oldPressure = 0;
            //int set_pressure = GetData((uint)val[1]); // knob pressure 4418
            for (uint i = 0; i < 8; i += 2)
            {
                oldPressure += (plugin.GetData((uint)val[0] + i, 0xFFFFF, 0) / 6553) * k;
                k = k * 10;
            }
            var diffPressure = Convert.ToInt32(Gx.getDataTyped()) - oldPressure;
            if (diffPressure == 0)
            {
                Gx.NextAction();
                return;
            }
            StringBuilder cmd = new StringBuilder("T0;300", 128);
            val[0] = Math.Abs(diffPressure);
            var pas = val[0] > 3 ? 3 : 1; // pas
            k = 16646 * Math.Sign(diffPressure);
            //if (set_pressure == 0) // set-pressure = 0 -> init
            //    cmd.Append("!DX;ALT_SET_PRESSURE -0!T0;300");
            var format = "!DX;ALT_SET_PRESSURE {0}!T0;{1}";
            for (int i = 0; i < val[0] / pas; i++)
            {
                cmd.AppendFormat(format, k * pas, val[2]);
            }
            if ((val[0] % pas) != 0)
                cmd.AppendFormat(format, k * (val[0] % pas), val[2]);
            cmd.ToString().DecodelineOfCommand(section: null, priority: 3);
        }

        //public void I()             //DP;address1:address2:lapse   course = 4442 heading = 4444
        //{
        //    StringBuilder cmd = new StringBuilder("T0;300", 128);
        //    uint adress;
        //    for (int i = 0; i<9;i++)
        //        for(int j = 0;i<24;j++)
        //        {
        //            adress = (uint)(4544 + i * 24 + j);
        //            plugin.GetData(adress, 24);
        //        }

        //}
    }
}

