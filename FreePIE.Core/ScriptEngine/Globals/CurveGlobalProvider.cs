using System.Collections.Generic;
using System.Linq;
using FreePIE.Core.Common;
using FreePIE.Core.Contracts;
using FreePIE.Core.Model;
using FreePIE.Core.Persistence;
using System;

namespace FreePIE.Core.ScriptEngine.Globals
{
    public class CurveGlobalProvider : IGlobalProvider
    {
        private readonly ISettingsManager settingsManager;
        public CurveGlobalProvider(ISettingsManager settingsManager)
        {
            this.settingsManager = settingsManager;
        }

        public IEnumerable<object> ListGlobals()
        {
            return settingsManager.Settings.Curves.Where(c => !string.IsNullOrEmpty(c.Name)).Select(c => new CurveGlobal(c));
        }

        public class CurveGlobal : IGlobalNameProvider
        {
            private readonly Curve curve;
            private  Func<List<Point>, double, double> Lcurve;
            public CurveGlobal(Curve curve)
            {
                this.Lcurve = CurveMath.T1curves[curve.selectedidx];
                this.curve = curve;
            }

            public double getY(double x, int curveindex = 0, int curveparam = 0)
            {
                Lcurve = CurveMath.T1curves[curveindex];
                CurveMath.CurveParam = curveparam;
                return Lcurve(curve.Points, x);
            }
            public string Name { get { return curve.Name; } }
            public double test_getY(double x)
            {
                Lcurve = CurveMath.T1curves[CurveMath.CurveIndex];
                var y =  Lcurve(curve.Points, x);
                CurveMath.testpoint.X = x;
                CurveMath.testpoint.Y = y;
                return y;
            }

        }
    }
}
