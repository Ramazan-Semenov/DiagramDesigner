﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DiagramDesigner
{
    public class Connection : Control, ISelectable, INotifyPropertyChanged
    {
        private Adorner connectionAdorner;

        #region Properties

        public Guid ID { get; set; }

        // source connector
        private Connector source;
        public Connector Source
        {
            get
            {
                return source;
            }
            set
            {
                if (source != value)
                {
                    if (source != null)
                    {
                        source.PropertyChanged -= new PropertyChangedEventHandler(OnConnectorPositionChanged);
                        source.Connections.Remove(this);
                    }

                    source = value;

                    if (source != null)
                    {
                        source.Connections.Add(this);
                        source.PropertyChanged += new PropertyChangedEventHandler(OnConnectorPositionChanged);
                    }

                    UpdatePathGeometry();
                }
            }
        }

        // sink connector
        private Connector sink;
        public Connector Sink
        {
            get { return sink; }
            set
            {
                if (sink != value)
                {
                    if (sink != null)
                    {
                        sink.PropertyChanged -= new PropertyChangedEventHandler(OnConnectorPositionChanged);
                        sink.Connections.Remove(this);
                    }

                    sink = value;

                    if (sink != null)
                    {
                        sink.Connections.Add(this);
                        sink.PropertyChanged += new PropertyChangedEventHandler(OnConnectorPositionChanged);
                    }
                    UpdatePathGeometry();
                }
            }
        }

        // connection path geometry
        private PathGeometry pathGeometry;
        public PathGeometry PathGeometry
        {
            get { return pathGeometry; }
            set
            {
                if (pathGeometry != value)
                {
                    pathGeometry = value;
                    UpdateAnchorPosition();
                    OnPropertyChanged("PathGeometry");
                }
            }
        }

        // between source connector position and the beginning 
        // of the path geometry we leave some space for visual reasons; 
        // so the anchor position source really marks the beginning 
        // of the path geometry on the source side
        private Point anchorPositionSource;
        public Point AnchorPositionSource
        {
            get { return anchorPositionSource; }
            set
            {
                if (anchorPositionSource != value)
                {
                    anchorPositionSource = value;
                    OnPropertyChanged("AnchorPositionSource");
                }
            }
        }

        // slope of the path at the anchor position
        // needed for the rotation angle of the arrow
        private double anchorAngleSource = 0;
        public double AnchorAngleSource
        {
            get { return anchorAngleSource; }
            set
            {
                if (anchorAngleSource != value)
                {
                    anchorAngleSource = value;
                    OnPropertyChanged("AnchorAngleSource");
                }
            }
        }

        // analogue to source side
        private Point anchorPositionSink;
        public Point AnchorPositionSink
        {
            get { return anchorPositionSink; }
            set
            {
                if (anchorPositionSink != value)
                {
                    anchorPositionSink = value;
                    OnPropertyChanged("AnchorPositionSink");
                }
            }
        }
        // analogue to source side
        private double anchorAngleSink = 0;
        public double AnchorAngleSink
        {
            get { return anchorAngleSink; }
            set
            {
                if (anchorAngleSink != value)
                {
                    anchorAngleSink = value;
                    OnPropertyChanged("AnchorAngleSink");
                }
            }
        }

        private ArrowSymbol sourceArrowSymbol = ArrowSymbol.None;
        public ArrowSymbol SourceArrowSymbol
        {
            get { return sourceArrowSymbol; }
            set
            {
                if (sourceArrowSymbol != value)
                {
                    sourceArrowSymbol = value;
                    OnPropertyChanged("SourceArrowSymbol");
                }
            }
        }

        public ArrowSymbol sinkArrowSymbol = ArrowSymbol.Arrow;
        public ArrowSymbol SinkArrowSymbol
        {
            get { return sinkArrowSymbol; }
            set
            {
                if (sinkArrowSymbol != value)
                {
                    sinkArrowSymbol = value;
                    OnPropertyChanged("SinkArrowSymbol");
                }
            }
        }

        // specifies a point at half path length
        private Point labelPosition;
        public Point LabelPosition
        {
            get { return labelPosition; }
            set
            {
                if (labelPosition != value)
                {
                    labelPosition = value;
                    OnPropertyChanged("LabelPosition");
                }
            }
        }

        // pattern of dashes and gaps that is used to outline the connection path
        private DoubleCollection strokeDashArray;
        public DoubleCollection StrokeDashArray
        {
            get
            {
                return strokeDashArray;
            }
            set
            {
                if (strokeDashArray != value)
                {
                    strokeDashArray = value;
                    OnPropertyChanged("StrokeDashArray");
                }
            }
        }
        // if connected, the ConnectionAdorner becomes visible
        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged("IsSelected");
                    if (isSelected)
                        ShowAdorner();
                    else
                        HideAdorner();
                }
            }
        }

        #endregion

        public Connection(Connector source, Connector sink)
        {
            this.ID = Guid.NewGuid();
            this.Source = source;
            this.Sink = sink;
            base.Unloaded += new RoutedEventHandler(Connection_Unloaded);
        }


        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // usual selection business
            DesignerCanvas designer = VisualTreeHelper.GetParent(this) as DesignerCanvas;
            if (designer != null)
            {
                if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != ModifierKeys.None)
                    if (this.IsSelected)
                    {
                        designer.SelectionService.RemoveFromSelection(this);
                    }
                    else
                    {
                        designer.SelectionService.AddToSelection(this);
                    }
                else if (!this.IsSelected)
                {
                    designer.SelectionService.SelectItem(this);
                }

                Focus();
            }
            e.Handled = false;
        }

        void OnConnectorPositionChanged(object sender, PropertyChangedEventArgs e)
        {
            // whenever the 'Position' property of the source or sink Connector 
            // changes we must update the connection path geometry
            if (e.PropertyName.Equals("Position"))
            {
                UpdatePathGeometry();
            }
        }

        private void UpdatePathGeometry()
        {
            if (Source != null && Sink != null)
            {
                PathGeometry geometry = new PathGeometry();
                List<Point> linePoints = PathFinder.GetConnectionLine(Source.GetInfo(), Sink.GetInfo(), true);
                if (linePoints.Count > 0)
                {
                    //PathFigure figure = new PathFigure();
                    PathFigure figure = new PathFigure();
                    figure.IsClosed = false;
                    linePoints[0] = new Point(linePoints[0].X, linePoints[0].Y);
                    figure.StartPoint = linePoints[0];

                   int mdl= linePoints.Count / 2;
                    //linePoints[1] = new Point(linePoints[1].X/1.1, linePoints[1].Y/1.1);
                    //linePoints[2] = new Point(linePoints[2].X*1.1, linePoints[2].Y*1.1);
                    //linePoints[3] = new Point(linePoints[3].X*1.1, linePoints[3].Y*1.1);
                    //linePoints[4] = new Point(linePoints[4].X*1.1, linePoints[4].Y*1.1);
                    //linePoints[mdl]=new Point( linePoints[mdl].X*2, linePoints[mdl].Y);
                    // linePoints.Remove(linePoints[0]);
                    //int c = linePoints.Count;
                    //for (int i = 1; i < c; i++)
                    //{
                    //    linePoints.Add(new Point(linePoints[i].X , linePoints[i].Y ));
                    //}

                    PolyBezierSegment polyBeziekkkr = new PolyBezierSegment(linePoints,true);
                    var startPoint =new Point( linePoints[0].X, linePoints[0].Y);
                    //Кривая Безье
                    Vector vector = linePoints.Last() - startPoint;
                    //Point point1 = new Point(startPoint.X + 3 * vector.X / 8, startPoint.Y + 1 * vector.Y / 8);
                    //Point point2 = new Point(startPoint.X + 5 * vector.X / 8, startPoint.Y + 7 * vector.Y / 8);

                    Point point1 = new Point(startPoint.X + vector.X / 2, startPoint.Y);
                    Point point2 = new Point(startPoint.X + vector.X / 1.5, startPoint.Y + vector.Y / 0.95);


                    BezierSegment polyBezier = new BezierSegment(point1, point2, linePoints.Last(), true);
                    PolyLineSegment arrow = DrawArrow(point2, startPoint);
                    arrow.IsStroked = false;
                    //BezierSegment polyBezier = new BezierSegment() {  };

                    // figure.Segments.Add(polyBeziekkkr);
                    figure.Segments.Add(arrow);
                    figure.Segments.Add(polyBezier);
                  
                    
                    // figure.IsClosed = true;
                   // figure.Segments.Add(new BezierSegment() {Point1= linePoints[0],  Point2= linePoints[mdl], Point3= linePoints.Last() });
                   // figure.Segments.Add(new ArcSegment() { Point= linePoints.Last() });

                     figure.Segments.Add(new PolyLineSegment(linePoints, false));
                    geometry.Figures.Add(figure);

                    this.PathGeometry = geometry;
                }
            }
        }
        public PolyLineSegment DrawArrow(Point a, Point b)
        {
            double HeadWidth = 10; // Ширина между ребрами стрелки
            double HeadHeight = 5; // Длина ребер стрелки

            double X1 = a.X;
            double Y1 = a.Y;

            double X2 = b.X;
            double Y2 = b.Y;

            double theta = Math.Atan2(Y1 - Y2, X1 - X2)-2;
            double sint = Math.Sin(theta);
            double cost = Math.Cos(theta);

           /* Point pt3 = new Point(
                X2 + (HeadWidth * cost - HeadHeight * sint),
                Y2 + (HeadWidth * sint + HeadHeight * cost));

            Point pt4 = new Point(
                X2 + (HeadWidth * cost + HeadHeight * sint),
                Y2 - (HeadHeight * cost - HeadWidth * sint));*/
            
            Point pt3 = new Point(
                X2 + (HeadWidth),
                Y2 + (HeadWidth));

            Point pt4 = new Point(
                X2 + (HeadWidth),
                Y2 - (HeadHeight));

            PolyLineSegment arrow = new PolyLineSegment();
            arrow.Points.Add(b);
            arrow.Points.Add(pt3);
            arrow.Points.Add(pt4);
            arrow.Points.Add(b);

            return arrow;
        }
        private void UpdateAnchorPosition()
        {
            Point pathStartPoint, pathTangentAtStartPoint;
            Point pathEndPoint, pathTangentAtEndPoint;
            Point pathMidPoint, pathTangentAtMidPoint;

            // the PathGeometry.GetPointAtFractionLength method gets the point and a tangent vector 
            // on PathGeometry at the specified fraction of its length
            this.PathGeometry.GetPointAtFractionLength(0, out pathStartPoint, out pathTangentAtStartPoint);
            this.PathGeometry.GetPointAtFractionLength(1, out pathEndPoint, out pathTangentAtEndPoint);
            this.PathGeometry.GetPointAtFractionLength(0.5, out pathMidPoint, out pathTangentAtMidPoint);

            // get angle from tangent vector
            this.AnchorAngleSource = Math.Atan2(-pathTangentAtStartPoint.Y, -pathTangentAtStartPoint.X) * (180 / Math.PI);
            this.AnchorAngleSink = Math.Atan2(pathTangentAtEndPoint.Y, pathTangentAtEndPoint.X) * (180 / Math.PI);

            // add some margin on source and sink side for visual reasons only
            pathStartPoint.Offset(-pathTangentAtStartPoint.X * 5, -pathTangentAtStartPoint.Y * 5);
            pathEndPoint.Offset(pathTangentAtEndPoint.X * 5, pathTangentAtEndPoint.Y * 5);

            this.AnchorPositionSource = pathStartPoint;
            this.AnchorPositionSink = pathEndPoint;
            this.LabelPosition = pathMidPoint;
        }

        private void ShowAdorner()
        {
            // the ConnectionAdorner is created once for each Connection
            if (this.connectionAdorner == null)
            {
                DesignerCanvas designer = VisualTreeHelper.GetParent(this) as DesignerCanvas;

                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    this.connectionAdorner = new ConnectionAdorner(designer, this);
                    adornerLayer.Add(this.connectionAdorner);
                }
            }
            this.connectionAdorner.Visibility = Visibility.Visible;
        }

        internal void HideAdorner()
        {
            if (this.connectionAdorner != null)
                this.connectionAdorner.Visibility = Visibility.Collapsed;
        }

        void Connection_Unloaded(object sender, RoutedEventArgs e)
        {
            // do some housekeeping when Connection is unloaded

            // remove event handler
            this.Source = null;
            this.Sink = null;

            // remove adorner
            if (this.connectionAdorner != null)
            {
                DesignerCanvas designer = VisualTreeHelper.GetParent(this) as DesignerCanvas;

                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    adornerLayer.Remove(this.connectionAdorner);
                    this.connectionAdorner = null;
                }
            }
        }

        #region INotifyPropertyChanged Members

        // we could use DependencyProperties as well to inform others of property changes
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion
    }

    public enum ArrowSymbol
    {
        None,
        Arrow,
        Diamond
    }
}
