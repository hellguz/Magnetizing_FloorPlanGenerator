using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace FloorPlan_Generator.Study4
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

    internal class AttributesNumberMultiplier : GH_Attributes<RoomInstance_00>
    {
        public AttributesNumberMultiplier(RoomInstance_00 owner)
          : base(owner)
        {

        }

        public override bool AllowMessageBalloon
        {
            get { return true; }
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
        private const int OuterRadius = 60;

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
                    graphics.FillEllipse(Brushes.Bisque, Bounds);
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

                    string text = string.Format("{0:0.00}", "FUCK YOURSELF");
                    Grasshopper.GUI.GH_GraphicsUtil.RenderCenteredText(graphics, text, GH_FontServer.Large, Color.Black, Pivot);
                    //Grasshopper.GUI.GH_GraphicsUtil.RenderObjectOverlay(graphics, , Bounds);
                    //GH_Capsule.CreateTextCapsule
                    RectangleF capsuleRect = new RectangleF( 
                        new PointF(InnerBounds.Location.X + 50, InnerBounds.Location.Y + 50), new SizeF(30, 30));
                    GH_Capsule capsuleBlue = GH_Capsule.CreateCapsule(capsuleRect, GH_Palette.Blue);
                    capsuleBlue.Render(graphics, Selected, Owner.Locked, true);
                    capsuleBlue.Dispose();

               /*     var field = new NumberInputTextField(new Param_Number())
                    {
                        Bounds = GH_Convert.ToRectangle(Bounds)
                    };
                    var matrix = sender.Viewport.XFormMatrix(GH_Viewport.GH_DisplayMatrix.CanvasToControl);

                    field.ShowTextInputBox(sender, initial, true, true, matrix);
                    */

                    break;
            }
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
          

            return base.RespondToMouseDoubleClick(sender, e);
        }

        internal class NumberInputTextField : Grasshopper.GUI.Base.GH_TextBoxInputBase
        {
            private readonly Param_Number _input;

            public NumberInputTextField(Param_Number input)
            {
                _input = input ?? throw new ArgumentNullException(nameof(input));
            }

            protected override void HandleTextInputAccepted(string text)
            {
                if (GH_Convert.ToDouble(text, out var number, GH_Conversion.Both))
                {
                    _input.PersistentData.Clear();
                    _input.PersistentData.Append(new GH_Number(number), new GH_Path(0));
                    _input.ExpireSolution(true);
                }
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


    public class RoomInstance_00 : GH_Component, IModifiable
    {
        /// <summary>
        /// Initializes a new instance of the RoomInstance class.
        /// </summary>
        public RoomInstance_00()
          : base("RoomInstance", "RoomCircle",
              "RoomCircle",
              "FloorPlanGen", "House Program Representation")
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new AttributesNumberMultiplier(this);
        }

        private readonly List<Guid> _targetIds = new List<Guid>();
        private readonly List<IModifiable> _targetObjs = new List<IModifiable>();

        public event EventHandler<ModifiableEventArgs> ModifyValue;


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

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, _targetObjs.Count.ToString());
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{2af1028d-fb80-4940-9d97-72dfea7ec789}"); }
        }
    }
}