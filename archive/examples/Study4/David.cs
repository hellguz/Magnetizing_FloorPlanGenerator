using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace TestComponent
{
    public class ModifiableEventArgs : EventArgs
    {
        public ModifiableEventArgs(double value)
        {
            Value = value;
        }
        public double Value { get; set; }
    }
    public interface IModifiable
    {
        event EventHandler<ModifiableEventArgs> ModifyValue;
    }
    public class ComponentAddNumbers : GH_Component, IModifiable
    {
        public ComponentAddNumbers()
          : base("GetHouseProgram", "GetHouseProgram",
              "GetHouseProgram",
              "FloorPlanGen", "House Program Representation")
        { }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("First number", "A", "First number in addition", GH_ParamAccess.item, 2.0);
            pManager.AddNumberParameter("Second number", "B", "Second number in addition", GH_ParamAccess.item, 5.0);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Result", "C", "Added result", GH_ParamAccess.item);
        }

        public event EventHandler<ModifiableEventArgs> ModifyValue;
        protected override void SolveInstance(IGH_DataAccess da)
        {
            double a = 0.0;
            double b = 0.0;
            if (!da.GetData(0, ref a)) return;
            if (!da.GetData(1, ref b)) return;

            double result = a + b;

            if (ModifyValue != null)
            {
                ModifiableEventArgs e = new ModifiableEventArgs(result);
                ModifyValue(this, e);
                result = e.Value;
            }

            da.SetData(0, result);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{6E7FB9B7-38CE-48F1-BE1D-FA8C1728C984}"); }
        }
    }

    internal class AttributesNumberMultiplier : GH_Attributes<ObjectNumberMultiplier>
    {
        public AttributesNumberMultiplier(ObjectNumberMultiplier owner)
          : base(owner)
        {

        }

        public override bool AllowMessageBalloon
        {
            get { return false; }
        }
        public override bool HasInputGrip
        {
            get { return false; }
        }
        public override bool HasOutputGrip
        {
            get { return false; }
        }

        private const int InnerRadius = 30;
        private const int OuterRadius = 90;

        public override bool IsPickRegion(PointF point)
        {
            return Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(Bounds, point);
        }
        protected override void Layout()
        {
            Pivot = GH_Convert.ToPoint(Pivot);
            Bounds = new RectangleF(Pivot.X - OuterRadius, Pivot.Y - OuterRadius, 2 * OuterRadius, 2 * OuterRadius);
        }

        protected RectangleF InnerBounds
        {
            get
            {
                RectangleF inner = Bounds;
                int inflation = OuterRadius - InnerRadius;
                inner.Inflate(-inflation, -inflation);
                return inner;
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            switch (channel)
            {
                case GH_CanvasChannel.Wires:
                    graphics.FillEllipse(Brushes.HotPink, Bounds);
                    foreach (IModifiable mod in Owner.TargetObjects())
                    {
                        if (mod == null)
                            continue;

                        IGH_DocumentObject obj = mod as IGH_DocumentObject;
                        if (obj == null)
                            continue;

                        DrawTargetArrow(graphics, obj.Attributes.Bounds);
                    }
                    break;

                case GH_CanvasChannel.Objects:
                    GH_Capsule capsule = GH_Capsule.CreateCapsule(InnerBounds, GH_Palette.Normal, InnerRadius, 0);
                    capsule.Render(graphics, Selected, Owner.Locked, true);
                    capsule.Dispose();

                    string text = string.Format("{0:0.00}", Owner.Factor);
                    Grasshopper.GUI.GH_GraphicsUtil.RenderCenteredText(graphics, text, GH_FontServer.Large, Color.Black, Pivot);
                    break;
            }
        }

        private void DrawTargetArrow(Graphics graphics, RectangleF target)
        {
            PointF cp = Grasshopper.GUI.GH_GraphicsUtil.BoxClosestPoint(Pivot, target);
            double distance = Grasshopper.GUI.GH_GraphicsUtil.Distance(Pivot, cp);
            if (distance < OuterRadius)
                return;

            Circle circle = new Circle(new Point3d(Pivot.X, Pivot.Y, 0.0), OuterRadius - 2);
            PointF tp = GH_Convert.ToPointF(circle.ClosestPoint(new Point3d(cp.X, cp.Y, 0.0)));

            Pen arrowPen = new Pen(Color.HotPink, OuterRadius - InnerRadius);
            arrowPen.EndCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
            graphics.DrawLine(arrowPen, tp, cp);
            arrowPen.Dispose();
        }

        private bool _drawing;
        private RectangleF _drawingBox;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
        {
            _drawing = false;
            _drawingBox = InnerBounds;

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // If on outer disc, but not in inner disc.. then start a wire drawing process.
                bool onOuterDisc = Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(Bounds, e.CanvasLocation);
                bool onInnerDisc = Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(InnerBounds, e.CanvasLocation);
                if (onOuterDisc && !onInnerDisc)
                {
                    // Begin arrow drawing behaviour.
                    _drawing = true;
                    sender.CanvasPostPaintObjects += CanvasPostPaintObjects;
                    return GH_ObjectResponse.Capture;
                }
            }

            // Otherwise revert to default behaviour.
            return base.RespondToMouseDown(sender, e);
        }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
        {
            if (_drawing)
            {
                _drawingBox = new RectangleF(e.CanvasLocation, new SizeF(0, 0));

                GH_Document doc = sender.Document;
                if (doc != null)
                {
                    IGH_Attributes att = doc.FindAttribute(e.CanvasLocation, true);
                    if (att != null)
                    {
                        if (att.DocObject is IModifiable)
                            _drawingBox = att.Bounds;
                    }
                }
                sender.Invalidate();
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseMove(sender, e);
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
        {
            if (_drawing)
            {
                _drawing = false;
                sender.CanvasPostPaintObjects -= CanvasPostPaintObjects;

                GH_Document doc = sender.Document;
                if (doc != null)
                {
                    IGH_Attributes att = doc.FindAttribute(e.CanvasLocation, true);
                    if (att != null)
                        if (att.DocObject is IModifiable)
                        {
                            Owner.RecordUndoEvent("Add Modifier");
                            Owner.AddTarget(att.DocObject.InstanceGuid);
                            IGH_ActiveObject obj = att.DocObject as IGH_ActiveObject;
                            if (obj != null)
                                obj.ExpireSolution(true);
                        }
                }

                sender.Invalidate();
                return GH_ObjectResponse.Release;
            }

            return base.RespondToMouseUp(sender, e);
        }
        void CanvasPostPaintObjects(GH_Canvas sender)
        {
            if (!_drawing) return;
            DrawTargetArrow(sender.Graphics, _drawingBox);
        }
    }


    public class ObjectNumberMultiplier : GH_ActiveObject, IGH_InstanceGuidDependent
    {
        public ObjectNumberMultiplier()
          : base("RoomCircle", "RoomCircle",
              "RoomCircle",
              "FloorPlanGen", "House Program Representation")
        {
            Random random = new Random();
            Factor = Math.Round(random.NextDouble() * 5, 1);
        }
        public override void CreateAttributes()
        {
            m_attributes = new AttributesNumberMultiplier(this);
        }
        public override Guid ComponentGuid
        {
            get { return new Guid("{65268634-7FE9-405C-BA36-BD9806F410EA}"); }
        }

        private readonly List<Guid> _targetIds = new List<Guid>();
        private readonly List<IModifiable> _targetObjs = new List<IModifiable>();

        public void AddTarget(Guid target)
        {
            if (_targetIds.Contains(target))
                return;
            _targetIds.Add(target);
            _targetObjs.Clear();
        }
        public void RemoveTarget(Guid target)
        {
            _targetIds.Remove(target);
            _targetObjs.Clear();
        }

        public IEnumerable<IModifiable> TargetObjects()
        {
            if (_targetIds.Count != _targetObjs.Count)
            {
                GH_Document doc = OnPingDocument();
                if (doc == null)
                    return new IModifiable[] { };

                _targetObjs.Clear();
                foreach (Guid id in _targetIds)
                {
                    IGH_DocumentObject obj = doc.FindObject(id, true);
                    if (obj == null)
                    {
                        _targetObjs.Add(null);
                        continue;
                    }
                    _targetObjs.Add(obj as IModifiable);
                }
            }

            return _targetObjs;
        }

        public override void AddedToDocument(GH_Document document)
        {
            document.SolutionStart += DocumentSolutionStart;
        }
        public override void RemovedFromDocument(GH_Document document)
        {
            document.SolutionStart -= DocumentSolutionStart;
        }
        void DocumentSolutionStart(object sender, GH_SolutionEventArgs e)
        {
            foreach (IModifiable mod in TargetObjects())
            {
                mod.ModifyValue -= ModifyValue;
                mod.ModifyValue += ModifyValue;
            }
        }
        void ModifyValue(object sender, ModifiableEventArgs e)
        {
            // First make sure the target object is still in the same document.
            IGH_DocumentObject obj = sender as IGH_DocumentObject;
            if (obj == null) return;
            if (obj.OnPingDocument().RuntimeID != OnPingDocument().RuntimeID)
                return;

            // If everything is hunky dory, multiply the value.
            if (!Locked)
                e.Value *= Factor;
        }

        public override void ComputeData()
        {
            Factor = Math.Round((new Random(Guid.NewGuid().GetHashCode()).NextDouble() * 10), 1);

            foreach (IModifiable mod in TargetObjects())
            {
                mod.ModifyValue -= ModifyValue;
                mod.ModifyValue += ModifyValue;
            }
            base.ComputeData();
        }

        public double Factor { get; set; }
        public override bool DependsOn(IGH_ActiveObject potentialSource)
        {
            return false;
        }
        public override bool IsDataProvider
        {
            get { return false; }
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetDouble("MultiplierFactor", Factor);
            writer.SetInt32("TargetCount", _targetIds.Count);
            for (int i = 0; i < _targetIds.Count; i++)
                writer.SetGuid("TargetID", i, _targetIds[i]);

            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            Factor = reader.GetDouble("MultiplierFactor");

            _targetIds.Clear();
            _targetObjs.Clear();
            int targetCount = reader.GetInt32("TargetCount");
            for (int i = 0; i < targetCount; i++)
            {
                Guid id = Guid.Empty;
                if (reader.TryGetGuid("TargetID", i, ref id))
                    AddTarget(id);
            }

            return base.Read(reader);
        }

        void IGH_InstanceGuidDependent.InstanceGuidsChanged(SortedDictionary<Guid, Guid> map)
        {
            _targetObjs.Clear();
            for (int i = 0; i < _targetIds.Count; i++)
            {
                Guid id = _targetIds[i];
                if (map.ContainsKey(id))
                    _targetIds[i] = map[id];
            }
        }
    }
}