using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using FreePIE.Core.Common;
using FreePIE.Core.Common.Extensions;
using FreePIE.Core.Model;
using FreePIE.Core.Model.Events;
using FreePIE.GUI.Common.Visiblox;
using FreePIE.GUI.Events;
using FreePIE.GUI.Result;
using FreePIE.GUI.Shells.Curves;
using IEventAggregator = FreePIE.Core.Common.Events.IEventAggregator;
using Point = FreePIE.Core.Model.Point;

namespace FreePIE.GUI.Views.Curves
{
    public class CurveViewModel : PropertyChangedBase
    {
        private readonly IEventAggregator eventAggregator;
        private readonly IResultFactory resultFactory;
        public Curve Curve { get; private set; }
        private int? selectedPointIndex;

        public CurveViewModel(IEventAggregator eventAggregator, IResultFactory resultFactory)
        {
            this.eventAggregator = eventAggregator;
            this.resultFactory = resultFactory;
        }

        public CurveViewModel Configure(Curve curve)
        {
            Curve = curve;
            InitCurve();
            
            return this;
        }
        
        private void InitCurve()
        {
            SelectedCurveIndex = 0;
            NameofButton = "Start Test";
            Enabled = true;
        }

        private void SetSelectablePoints(bool value = true)
        {
            if (value)
            {
                PointSize = 7;
                SelectablePoints = Curve.Points.Skip(1).TakeAllButLast();
            }
            else
            {
                //SelectablePoints = Points.TakeFirstAndLast();
                PointSize = 0;
                SelectablePoints = Curve.Points.TakeFirstAndLast();
            }
        }
        private int _pointsize { get; set; }
        public int PointSize
        {
            get { return _pointsize; }
            set
            {
                _pointsize = value;
                NotifyOfPropertyChange(() => PointSize);
            }
        }
        public string Name
        {
            get { return Curve.Name; }
            set 
            { 
                Curve.Name = value;
                eventAggregator.Publish(new CurveChangedNameEvent(Curve));
                NotifyOfPropertyChange(() => Name);
            }
        }

        public bool ValidateCurve
        {
            get { return Curve.ValidateCurve.Value; }
            set
            {
                Curve.ValidateCurve = value; 
                NotifyOfPropertyChange(() => ValidateCurve);
            }
        }

        //xmlns:sys="clr-namespace:System;assembly=mscorlib"
        //     cal:Message.Attach="[Event Loaded] = [Action OnLoaded($eventArgs)]">
        //public void OnLoaded(object s)
        //{
        //    //SelectedCurveIndex = 0;
        //    //slParam = 0;
        //    //Move = true;
        //    CurveMath.ShowMethods(typeof(CurveMath));
        //    //loaded = true;
        //}
        public BindableCollection<string> TypeCurves => new BindableCollection<string>(new string[] { "Linear", "CubicSpline", "Joy1", "Joy2"});

        public int SelectedCurveIndex
        {
            get { return Curve.selectedidx; }
            set
            {
                Curve.selectedidx = value;
                CurveMath.CurveIndex = value;
                Del = false;
                Add = false;
                Move = true;
                slParam = 0;
                NotifyOfPropertyChange(() => SelectedCurveIndex);
            }
        }
        public string SelectedTypeCurve
        {
            get{ return Curve.selectedcurve; }
            set
            {
                Curve.selectedcurve = value;
                NotifyOfPropertyChange(() => SelectedTypeCurve);
            }
        }

        private int _slparam;
        public int slParam
        {
            get { return _slparam; }
            set
            {
                if (SelectedCurveIndex == 1)
                {
                    _slparam = 0;
                    CurveMath.CurveParam = 0;
                    NotifyOfPropertyChange(() => slParam);
                    Points = CalculateNewPoints();
                    SetSelectablePoints();
                    return;
                }

                _slparam = value;
                CurveMath.CurveParam = value;
                NotifyOfPropertyChange(() => slParam);
                Points = CalculateNewPoints();
                if (value > 0 || SelectedCurveIndex > 1)
                    SetSelectablePoints(false);
                else
                    SetSelectablePoints();
            }
        }

        private bool _move { get; set; }
        private bool _add { get; set; }
        private bool _del { get; set; }
        public bool Move
        {
            get { return _move; }
            set
            {
                _move = value;
                BoolTag = 1;
                NotifyOfPropertyChange(() => Move);
            }
        }
        public bool Add
        {
            get { return _add; }
            set
            {
                if (slParam > 0 || SelectedCurveIndex > 1) return;
                _add = value;
                BoolTag = 4;
                NotifyOfPropertyChange(() => Add);
            }
        }
        public bool Del
        {
            get { return _del; }
            set
            {
                if (slParam > 0 || SelectedCurveIndex > 1) return;
                _del = value;
                BoolTag = 2;
                NotifyOfPropertyChange(() => Del);
            }
        }
        private int _booltag { get; set; }
        public int BoolTag
        {
            get { return _booltag; }
            set
            {
                _booltag = (Move ? 1 : 0) + (Del ? 2 : 0) + (Add ? 4 : 0);
                NotifyOfPropertyChange(() => BoolTag);
            }
        }
        public IEnumerable<IResult> Delete()
        {
            var message = resultFactory.ShowMessageBox($"Delete {Curve.Name}?", "Curve will be deleted, continue?", MessageBoxButton.OKCancel);
            yield return message;
            
            if(message.Result == System.Windows.MessageBoxResult.OK)
                eventAggregator.Publish(new DeleteCurveEvent(this));
        }

        public IEnumerable<IResult> Reset()
        {
            var dialog = resultFactory.ShowDialog<NewCurveViewModel>().Configure(m => m.Init(Curve));
            yield return dialog;

            var newCurve = dialog.Model.NewCurve;
            if (newCurve != null)
            {
                var message = resultFactory.ShowMessageBox($"Reset {Curve.Name}?", "Curve will be reset, continue?", MessageBoxButton.OKCancel);
                yield return message;

                if (message.Result == System.Windows.MessageBoxResult.OK)
                {
                    Curve.Reset(newCurve);
                    InitCurve();
                    Name = newCurve.Name;
                    ValidateCurve = true;
                }
            }
        }

        public bool HasSelectedPoint => selectedPointIndex.HasValue;

        //private bool canSetDefault;
        //public bool CanSetDefault
        //{
        //    get { return canSetDefault; }
        //    set
        //    {
        //        canSetDefault = value;
        //        NotifyOfPropertyChange(() => canSetDefault);
        //    }
        //}

        public void ApplyNewValuesToSelectedPoint()
        {
            if (Move)
                ApplyNewSelectedPoint(new Point(SelectedPointX, SelectedPointY));
            else
            {
                if (Del)
                    DeleteSelection(new Point(SelectedPointX, SelectedPointY));
            }
        }

        public void OnPointSelected(MovePointBehaviour.PointSelectedEventArgs e)
        {
            if (Add)
            {
                AddNewPoint(e);
                return;
            }

            selectedPointIndex = Curve.IndexOf(e.Point);

            UpdateSelectedPoint();
            
            //CanSetDefault = selectedPointIndex == Curve.Points.Count - 1;
            NotifyOfPropertyChange(() => HasSelectedPoint);
        }


        public void CurrentPoint(MovePointBehaviour.PointSelectedEventArgs e)
        {
            CurrentX = e.Point.X;
            CurrentY = e.Point.Y;
        }

        private double currentx;
        public double CurrentX
        {
            get { return currentx; }
            set
            {
                currentx = value;
                NotifyOfPropertyChange(() => CurrentX);
            }
        }

        private double currenty;
        public double CurrentY
        {
            get { return currenty; }
            set
            {
                currenty = value;
                NotifyOfPropertyChange(() => CurrentY);
            }
        }

        private void UpdateSelectedPoint()
        {
            SelectedPointX = GetSelectedPoint().X;
            SelectedPointY = GetSelectedPoint().Y;
        }

        private double selectedPointX;
        public double SelectedPointX
        {
            get { return selectedPointX; }
            set
            {
                selectedPointX = value;
                NotifyOfPropertyChange(() => SelectedPointX);
            }
        }

        private double selectedPointY;
        public double SelectedPointY
        {
            get { return selectedPointY; }
            set
            {
                selectedPointY = value;
                NotifyOfPropertyChange(() => SelectedPointY);
            }
        }

        private void ApplyNewSelectedPoint(Point newPoint)
        {
            var args = new MovePointBehaviour.PointMoveEventArgs
            {
                OldPoint = GetSelectedPoint(),
                NewPoint = newPoint
            };
            OnPointDragged(args);
            SetSelectablePoints();
        }
        public void DeleteSelection(Point point)
        {
            var index = Curve.IndexOf(point);
            Curve.Points.RemoveAt(index);
            selectedPointIndex = null;
            NotifyOfPropertyChange(() => HasSelectedPoint);
            Points = CalculateNewPoints();
            SetSelectablePoints();
        }

        private void AddNewPoint(MovePointBehaviour.PointSelectedEventArgs e)
        {
            foreach (var pt in points)
                if (pt == e.Point) return;
            Curve.Points.Add(e.Point);
            Curve.Points = Curve.Points.OrderBy(p => p.X).ToList();
            Points = CalculateNewPoints();
            selectedPointIndex = Curve.IndexOf(e.Point);
            UpdateSelectedPoint();
            NotifyOfPropertyChange(() => HasSelectedPoint);
            SetSelectablePoints();
        }
        private Point GetSelectedPoint()
        {
            if (selectedPointIndex.HasValue)
                return Curve.Points[selectedPointIndex.Value];

            return new Point();
        }

        public void OnPointDragged(MovePointBehaviour.PointMoveEventArgs e)
        {
            if (!Move || SelectedCurveIndex > 1) return;
            var oldPoint = e.OldPoint;
            var newPoint = e.NewPoint;
            
            var index = Curve.IndexOf(e.OldPoint);
            
            var newCurve = Curve.Points.GetRange(0, Curve.Points.Count);
            newCurve[index] = newPoint;

            var firstPoint = newCurve[0];
            var lastPoint = newCurve[newCurve.Count - 1];

            var biggestValueForY = double.MinValue;

            if(ValidateCurve)
                for (double x = firstPoint.X + 0.01; x < lastPoint.X - 0.01; x++)
                {
                    var y = CurveMath.T1curves[SelectedCurveIndex](newCurve, x);
                    if (y < biggestValueForY || newPoint.X >= lastPoint.X || newPoint.X <= firstPoint.X)
                    {
                        newPoint = oldPoint;
                        break;
                    }
                    
                    if (y > biggestValueForY)
                        biggestValueForY = y;
                }

            e.NewPoint = newPoint;
            Curve.Points[index] = e.NewPoint;

            Points = CalculateNewPoints();
            UpdateSelectedPoint();
        }

        private IEnumerable<Point> CalculateNewPoints()
        {
            return CurveMath.Txcurves[SelectedCurveIndex](Curve.Points);
        }

        private IEnumerable<Point> points;
        public IEnumerable<Point> Points
        {
            get { return points; }
            set
            {
                points = value;
                NotifyOfPropertyChange(() => Points);
            }
        }

        private IEnumerable<Point> selectablePoints;
        public IEnumerable<Point> SelectablePoints
        {
            get { return selectablePoints; }
            set
            {
                selectablePoints = value;
                NotifyOfPropertyChange(() => SelectablePoints);
            }
        }



        // -------------- test joy
        //        <Charts:LineSeries IsDisplayedOnLegend = "false" PointSize="10" ShowPoints="True" ShowLine="False" ShowArea="false">
        //    <Charts:LineSeries.DataSeries>
        //        <Charts:BindableDataSeries
        //            ItemsSource = "{ Binding Trace}"
        //            XValueBinding="{Binding Path=X}"
        //            YValueBinding="{Binding Path=Y}" />
        //    </Charts:LineSeries.DataSeries>
        //</Charts:LineSeries>

        //private IEnumerable<Point> _trace { get; set; }
        //public IEnumerable<Point> Trace
        //{
        //     get { return _trace; }
        //    set
        //    {
        //        _trace = value;
        //        NotifyOfPropertyChange(() => Trace);
        //    }
        //}



        ////Variable de contrôle.
        //private bool _quitter = false;
        ////Variables globales étant affectées par les threads.
        //private bool _launched = false;
        //private static int _denominateur;
        //System.Threading.Thread init;
        //public void Test()
        //{
        //    if (_launched)
        //    {
        //        _quitter = true;
        //        _launched = false;
        //    }
        //    else
        //    {
        //        _quitter = false;
        //        _launched = true;
        //        init = new System.Threading.Thread(Initialiser);
        //        init.Start();
        //    }
        //    //Puis on leur demande de quitter.

        //}
        //public void Initialiser()
        //{
        //    //Boucle infinie contrôlée.
        //    while (!_quitter)
        //    {
        //        if(CurveMath.testpoint != null)
        //            Trace = CurveMath.testpoint;
        //        System.Threading.Thread.Sleep(30);

        //    }

        //}
        private System.Threading.Thread init;
        public void Test()
        {
            if (NameofButton[2] == 'a')
            {
                NameofButton = "Stop Test";
                Enabled = false;
                _quitter = false;
                init = new System.Threading.Thread(startTest);
                init.Start();
            }
            else
            {
                NameofButton = "Start Test";
                Enabled = true;
                _quitter = true;
            }
        }

        private string _nameofbutton { get; set; }
        public string NameofButton
        {
            get { return _nameofbutton; }
            set
            {
                _nameofbutton = value;
                NotifyOfPropertyChange(() => NameofButton);
            }
        }
        private bool _enabled { get; set; }
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                NotifyOfPropertyChange(() => Enabled);
            }
        }

        private bool _quitter = false;
        public void startTest()
        {
            //Boucle infinie contrôlée.
            while (!_quitter)
            {
                Trace = new List<Point>() { CurveMath.testpoint };
                System.Threading.Thread.Sleep(30);

            }
            Trace = new List<Point>();
        }
        private IEnumerable<Point> _trace { get; set; }
        public IEnumerable<Point> Trace
        {
            get { return _trace; }
            set
            {
                _trace = value;
                NotifyOfPropertyChange(() => Trace);
            }
        }
    }
}
