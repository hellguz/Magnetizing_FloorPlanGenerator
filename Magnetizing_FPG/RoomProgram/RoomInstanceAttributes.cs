using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Linq;

namespace Magnetizing_FPG
{
    public interface IRoomStructure<T>
    {
        void AddAdjacence(T a);
        void RemoveAdjacence(T a);
    }

    public class RoomInstanceAttributes : GH_ComponentAttributes, IRoomStructure<IGH_DocumentObject>
    {

        public RoomInstanceAttributes(RoomInstance param) : base(param)
        {
            if (RoomArea == null) RoomArea = GH_Capsule.CreateTextCapsule(new Rectangle(), new Rectangle(), GH_Palette.Black, param.RoomArea.ToString());
            RoomName = GH_Capsule.CreateTextCapsule(new Rectangle(), new Rectangle(), GH_Palette.Black, param.RoomName);

            roomBrush = Brushes.Gray;


        }

        protected override void Layout()
        {
            Pivot = GH_Convert.ToPoint(Pivot);
            Bounds = new RectangleF(Pivot.X - OuterComponentRadius, Pivot.Y - OuterComponentRadius, 2 * OuterComponentRadius, 2 * OuterComponentRadius);

        }

        public GH_Capsule RoomArea;
        public GH_Capsule RoomName;


        Rectangle RoomAreaRectangle;
        Rectangle RoomNameRectangle;
        Rectangle RoomIdRectangle;

        bool haveReadTargetObjectsList = false;

        public Brush roomBrush;

        const int InflateAmount = 2; // Used to inflate all rectangles for producing outer rectangles for GH_TextCapsules
        const int InnerComponentRadius = 55; // Used to define the radius of the main circle
        const int OuterComponentRadius = 75; // Used to define the radius of the main circle

        public string[] writerTargetObjectsListString = new string[0];

        public HouseInstance AssignedHouseInstance;


        public List<IGH_DocumentObject> targetObjectList = new List<IGH_DocumentObject>();

        protected Rectangle InflateRect(Rectangle rect, int a = 5, int b = 5)
        {
            Rectangle rectOut = rect;
            rectOut.Inflate(-a, -b);
            return rectOut;
        }

        protected RectangleF InnerComponentBounds
        {
            get
            {
                RectangleF inner = Bounds;
                int inflation = OuterComponentRadius - InnerComponentRadius;
                inner.Inflate(-inflation, -inflation);
                return inner;
            }
        }

        public override bool IsPickRegion(PointF point)
        {
            return Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(Bounds, point);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (Owner is RoomInstance roomInstance)
                if (channel == GH_CanvasChannel.Objects)
                {
                    graphics.FillEllipse(roomBrush, Bounds);

                    if ((Owner as RoomInstance).hasMissingAdj)
                    {
                        graphics.FillEllipse(Brushes.Red, new RectangleF(Pivot.X - InnerComponentRadius - 9, Pivot.Y - InnerComponentRadius - 9
                            , 2 * InnerComponentRadius + 18, 2 * InnerComponentRadius + 18));
                    }

                    GH_Capsule capsule = GH_Capsule.CreateCapsule(InnerComponentBounds, GH_Palette.Normal, InnerComponentRadius - 5, 0);
                    capsule.Render(graphics, Selected, Owner.Locked, true);
                    capsule.Dispose();

                    RoomNameRectangle = new Rectangle(new System.Drawing.Point((int)Bounds.Location.X + 28, (int)Bounds.Location.Y + 55), new Size(94, 20));
                    RoomAreaRectangle = new Rectangle(new System.Drawing.Point((int)Bounds.Location.X + 65, (int)Bounds.Location.Y + 80), new Size(57, 20));
                    RoomIdRectangle = new Rectangle(new System.Drawing.Point((int)Bounds.Location.X + 35, (int)Bounds.Location.Y + 105), new Size(80, 40));

                    graphics.DrawString("m² :", SystemFonts.IconTitleFont, Brushes.Black, new RectangleF(new System.Drawing.Point((int)Bounds.Location.X + 35, (int)Bounds.Location.Y + 81), new Size(30, 20)));
                    if (!RoomInstance.entranceIds.Contains(roomInstance.RoomId))
                        graphics.DrawString("ID: " + roomInstance.RoomId, new Font(FontFamily.GenericSansSerif, 6f, FontStyle.Regular), Brushes.Black, RoomIdRectangle, new StringFormat() { Alignment = StringAlignment.Center });
                    else
                        graphics.DrawString("ID: " + roomInstance.RoomId + "\n(entrance)", new Font(FontFamily.GenericSansSerif, 6f, FontStyle.Bold), Brushes.Black, RoomIdRectangle, new StringFormat() { Alignment = StringAlignment.Center });


                    RoomName = GH_Capsule.CreateTextCapsule(RoomNameRectangle, InflateRect(RoomNameRectangle, InflateAmount, InflateAmount), GH_Palette.Pink, roomInstance.RoomName);
                    RoomName.Render(graphics, GH_Skin.palette_grey_standard);
                    RoomName.Dispose();

                    RoomArea = GH_Capsule.CreateTextCapsule(RoomAreaRectangle, InflateRect(RoomAreaRectangle, InflateAmount, InflateAmount), GH_Palette.Pink, roomInstance.RoomArea.ToString());
                    RoomArea.Render(graphics, GH_Skin.palette_white_standard);
                    RoomArea.Dispose();


                    for (int i = 0; i < RoomInstance.allAdjacencesList.Count; i++)
                        try
                        {
                            Owner.OnPingDocument().FindComponent(new Guid(RoomInstance.allAdjacencesList[i].b));
                            Owner.OnPingDocument().FindComponent(new Guid(RoomInstance.allAdjacencesList[i].a));

                            if (Owner.OnPingDocument().FindComponent(new Guid(RoomInstance.allAdjacencesList[i].b)) == null ||
                                Owner.OnPingDocument().FindComponent(new Guid(RoomInstance.allAdjacencesList[i].a)) == null)
                            {
                                RoomInstance.allAdjacencesList.RemoveAt(i); i--;
                            }
                        }
                        catch (Exception) { RoomInstance.allAdjacencesList.RemoveAt(i); i--; }


                    foreach (RoomInstance.IntPair intPair in RoomInstance.allAdjacencesList)
                    {
                        if (intPair.a == this.Owner.InstanceGuid.ToString())
                            try
                            {
                                if (Owner.OnPingDocument().FindComponent(new Guid(intPair.b)) != null)
                                    DrawTargetArrow(graphics, Owner.OnPingDocument().FindComponent(new Guid(intPair.b)).Attributes.Bounds);
                            }
                            catch (Exception) { }
                        if (intPair.b == this.Owner.InstanceGuid.ToString())
                            try
                            {
                                if (Owner.OnPingDocument().FindComponent(new Guid(intPair.b)) != null)
                                    DrawTargetArrow(graphics, Owner.OnPingDocument().FindComponent(new Guid(intPair.a)).Attributes.Bounds);
                            }
                            catch (Exception) { }
                    }

                    // if (obj != null)
                    //     DrawTargetArrow(graphics, obj.Attributes.Bounds);


                } else
                {
                    base.Render(canvas, graphics, channel);
                }
        
            if (RoomInstance.allAdjacencesList == null || RoomInstance.allAdjacencesList.Count == 0)

                if (writerTargetObjectsListString.Length > 0)// && targetObjectList.Count == 0)
                {
                    if (writerTargetObjectsListString.Length > 0)
                        foreach (string guidS in writerTargetObjectsListString)
                        {
                            try
                            {
                                RoomInstance.allAdjacencesList.Add(new RoomInstance.IntPair(guidS.Split('%')[0], guidS.Split('%')[1]));
                                //   Owner.OnPingDocument().FindComponent(new Guid(guidS.Split('!')[0]))

                                /*  if (!targetObjectList.Contains(Owner.OnPingDocument().FindComponent(
                                      new System.Drawing.Point(int.Parse(guidS.Split('!')[0]), int.Parse(guidS.Split('!')[1]))) as RoomInstance))
                                      targetObjectList.Add(Owner.OnPingDocument().FindComponent(
                                          new System.Drawing.Point(int.Parse(guidS.Split('!')[0]), int.Parse(guidS.Split('!')[1]))) as RoomInstance);*/
                            }
                            catch (Exception) { }
                        }
                }

        }

        /// <summary>
        /// Get the closest point on the circle
        /// </summary>
        /// <param name="point"></param>
        /// <param name="circle"></param>
        /// <returns></returns>
        public PointF CircleClosestPoint(PointF point, RectangleF circle)
        {
            Vector2d vec = new Vector2d(circle.X + circle.Width / 2 - point.X, circle.Y + circle.Width / 2 - point.Y);
            vec.Unitize();
            vec = new Vector2d(vec.X * circle.Width / 2, vec.Y * circle.Width / 2);
            return (new PointF((float)(circle.Location.X + circle.Width / 2 + -vec.X), (float)(circle.Location.Y + circle.Width / 2 - vec.Y)));
        }

        private void DrawTargetArrow(Graphics graphics, RectangleF target)
        {
            PointF cp = CircleClosestPoint(Pivot, target);

            double distance = Grasshopper.GUI.GH_GraphicsUtil.Distance(Pivot, cp);
            if (distance < OuterComponentRadius)
                return;

            Circle circle = new Circle(new Point3d(Pivot.X, Pivot.Y, 0.0), OuterComponentRadius - 2);
            PointF tp = GH_Convert.ToPointF(circle.ClosestPoint(new Point3d(cp.X, cp.Y, 0.0)));

            Pen arrowPen = new Pen(roomBrush, (OuterComponentRadius - InnerComponentRadius) / 2);
            arrowPen.EndCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
            arrowPen.StartCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
            graphics.DrawLine(arrowPen, tp, cp);
            arrowPen.Dispose();
        }


        private bool _drawing;
        private RectangleF _drawingBox;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
        {
            _drawing = false;
            _drawingBox = InnerComponentBounds;

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // If on outer disc, but not in inner disc, then start a wire drawing process.
                bool onOuterDisc = Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(Bounds, e.CanvasLocation);
                bool onInnerDisc = Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(InnerComponentBounds, e.CanvasLocation);
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
                        if (att is IRoomStructure<IGH_DocumentObject>)
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
                        if (att is IRoomStructure<IGH_DocumentObject> target)
                        {
                            Owner.RecordUndoEvent("Add Modifier");
                            if (att.DocObject is RoomInstance)
                            {
                                if ((att.DocObject as RoomInstance).InstanceGuid != (DocObject as RoomInstance).InstanceGuid)
                                {
                                    if (RoomInstance.allAdjacencesList.Find(i => i.a == att.DocObject.InstanceGuid.ToString() && i.b == DocObject.InstanceGuid.ToString()) == null &&
                                        RoomInstance.allAdjacencesList.Find(i => i.b == att.DocObject.InstanceGuid.ToString() && i.a == DocObject.InstanceGuid.ToString()) == null)
                                    {
                                        AddAdjacence(att.DocObject);
                                        target.AddAdjacence(this.DocObject as IGH_DocumentObject);
                                    }
                                    /*
                                    if (targetObjectList.Find(item => (item as RoomInstance).RoomId == (att.DocObject as RoomInstance).RoomId) == null)
                                    {
                                        AddAdjacence(att.DocObject);
                                        target.AddAdjacence(this.DocObject as IGH_DocumentObject);
                                    }*/
                                    else
                                    {
                                        RemoveAdjacence(att.DocObject);
                                        target.RemoveAdjacence(this.DocObject as IGH_DocumentObject);
                                    }

                                    RoomInstance.allAdjacencesList = RoomInstance.allAdjacencesList.Distinct().ToList();
                                }
                            }
                            else if (att.DocObject is HouseInstance houseInstance)
                            {
                                if ((att as HouseInstanceAttributes).roomInstancesGuidList.Find(item => item == (this.DocObject as RoomInstance).InstanceGuid.ToString()) == null)
                                    target.AddAdjacence(this.DocObject as IGH_DocumentObject);
                                else
                                {
                                    target.RemoveAdjacence(this.DocObject as IGH_DocumentObject);
                                }

                            }

                            IGH_ActiveObject obj = att.DocObject as IGH_ActiveObject;
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


        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (Owner is RoomInstance roomInstance)
            {
                string initial = string.Empty;


                var matrix = sender.Viewport.XFormMatrix(GH_Viewport.GH_DisplayMatrix.CanvasToControl);

                if (this.RoomArea.Contains(e.CanvasLocation))
                {
                    var field = new CapsuleInputBase(RoomArea, roomInstance, RoomInstanceVar.RoomArea)
                    {
                        Bounds = GH_Convert.ToRectangle(RoomArea.Box)
                    };

                    field.ShowTextInputBox(sender, RoomArea.Text, true, false, matrix);
                }


                if (this.RoomName.Contains(e.CanvasLocation))
                {
                    var field = new CapsuleInputBase(RoomName, roomInstance, RoomInstanceVar.RoomName)
                    {
                        Bounds = GH_Convert.ToRectangle(RoomName.Box)
                    };

                    field.ShowTextInputBox(sender, RoomName.Text, true, false, matrix);
                }
                roomInstance.ExpireSolution(false);

                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }



        public void AddAdjacence(IGH_DocumentObject a)
        {
            // int t = String.Compare(a.InstanceGuid.ToString(), this.Owner.InstanceGuid.ToString());
            // if (t >= 0)
            RoomInstance.allAdjacencesList.Add(new RoomInstance.IntPair(a.InstanceGuid.ToString(), this.Owner.InstanceGuid.ToString()));
            //  else
            RoomInstance.allAdjacencesList.Add(new RoomInstance.IntPair(this.Owner.InstanceGuid.ToString(), a.InstanceGuid.ToString()));

            //  if (!targetObjectList.Contains(a))
            //      targetObjectList.Add(a);

            if (AssignedHouseInstance != null)
                (AssignedHouseInstance.Attributes as HouseInstanceAttributes).AddAdjacence(a);
            else if ((a.Attributes as RoomInstanceAttributes).AssignedHouseInstance != null)
                ((a.Attributes as RoomInstanceAttributes).AssignedHouseInstance.Attributes as HouseInstanceAttributes).AddAdjacence(this.Owner as RoomInstance);
        }

        public void RemoveAdjacence(IGH_DocumentObject a)
        {
            RoomInstance.allAdjacencesList.RemoveAll(i => i.a == Owner.InstanceGuid.ToString() && i.b == a.InstanceGuid.ToString());
            RoomInstance.allAdjacencesList.RemoveAll(i => i.b == Owner.InstanceGuid.ToString() && i.a == a.InstanceGuid.ToString());


            //  while (targetObjectList.Contains(a))
            //   {
            //       targetObjectList.Remove(a);
            //  }



        }


        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            string roomInstancesListString = "";
            foreach (RoomInstance.IntPair intPair in RoomInstance.allAdjacencesList)
                roomInstancesListString += intPair.a + "%" + intPair.b + "@";

            /*   foreach (RoomInstance room in targetObjectList)
                   if (room != null)
                       roomInstancesListString += ((int)(room.Attributes.Pivot.X)).ToString() + "!" +
                       ((int)(room.Attributes.Pivot.Y)).ToString() + "@";
   */
            if (roomInstancesListString.Length > 0)
                roomInstancesListString = roomInstancesListString.Remove(roomInstancesListString.Length - 1);

            writer.SetString("TargetObjectList", roomInstancesListString);
            writer.SetString("RoomName", (Owner as RoomInstance).RoomName);
            //    writer.SetInt32("RoomId", (int)(Owner as RoomInstance).RoomId);
            writer.SetDouble("RoomArea", (Owner as RoomInstance).RoomArea);

            writer.SetBoolean("isHall", (Owner as RoomInstance).isHall);

            string temp = "";
            foreach (int a in RoomInstance.entranceIds)
                temp += a.ToString() + "&";
            if (temp.Length > 0)
                temp = temp.Remove(temp.Length - 1);

            writer.SetString("EntranceIds", temp);

            return base.Write(writer);
        }


        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            string roomInstancesListString = reader.GetString("TargetObjectList");
            writerTargetObjectsListString = roomInstancesListString.Split('@');

            // roomInstancesListString.Remove(roomInstancesListString.Length - 1);

            (Owner as RoomInstance).RoomName = reader.GetString("RoomName");//, (Owner as RoomInstance).RoomName);
                                                                            //    (Owner as RoomInstance).RoomId = (uint)reader.GetInt32("RoomId");//, (int)(Owner as RoomInstance).RoomId);
            (Owner as RoomInstance).RoomArea = (int)Math.Floor(reader.GetDouble("RoomArea"));//, (Owner as RoomInstance).RoomArea);

            try
            {
                (Owner as RoomInstance).isHall = reader.GetBoolean("isHall");
            }
            catch (Exception e) { }

            RoomInstance.entranceIds = new List<int>();
            string temp = "";
            try
            {
                temp = reader.GetString("EntranceIds");
            }
            catch (Exception) { }

            if (temp != null && temp.Length > 0)
            {
                string[] tempList = temp.Split('&');
                foreach (string s in tempList)
                    RoomInstance.entranceIds.Add(Int32.Parse(s));
            }

            Owner.ExpireSolution(false);

            return base.Read(reader);
        }
    }

    public enum RoomInstanceVar { RoomName, RoomArea };

    class CapsuleInputBase : Grasshopper.GUI.Base.GH_TextBoxInputBase
    {
        public GH_Capsule _input;
        public RoomInstance _roomInstance;
        private RoomInstanceVar _roomInstanceVar;

        public CapsuleInputBase(GH_Capsule input, RoomInstance roomInstance, RoomInstanceVar roomInstanceVar)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _roomInstance = roomInstance;
            _roomInstanceVar = roomInstanceVar;
        }

        protected override void HandleTextInputAccepted(string text)
        {
            _input.Text = text;

            switch (_roomInstanceVar)
            {
                case (RoomInstanceVar.RoomName):
                    _roomInstance.RoomName = text;
                    break;

                case (RoomInstanceVar.RoomArea):
                    _roomInstance.RoomArea = Int32.Parse(text);
                    break;
            }
            _roomInstance.ExpireSolution(false);
            if ((_roomInstance.Attributes as RoomInstanceAttributes).AssignedHouseInstance != null)
                (_roomInstance.Attributes as RoomInstanceAttributes).AssignedHouseInstance.ExpireSolution(false);
        }
    }


}
