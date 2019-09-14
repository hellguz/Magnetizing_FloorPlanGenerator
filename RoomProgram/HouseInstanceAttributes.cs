using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;

namespace FloorPlan_Generator
{
    public class HouseInstanceAttributes : GH_ComponentAttributes, IRoomStructure<IGH_DocumentObject>
    {

        public HouseInstanceAttributes(HouseInstance param) : base(param)
        {
            random = new Random(Guid.NewGuid().GetHashCode());
            houseBrush = PickBrush();

        }

        private Brush PickBrush()
        {
            List<Brush> brushes = new List<Brush>();

            brushes.Add(Brushes.Aqua);
            brushes.Add(Brushes.Bisque);
            brushes.Add(Brushes.BlanchedAlmond);
            brushes.Add(Brushes.DarkViolet);
            brushes.Add(Brushes.LemonChiffon);
            brushes.Add(Brushes.LightBlue);
            brushes.Add(Brushes.LightCyan);
            brushes.Add(Brushes.LightGreen);
            brushes.Add(Brushes.LightSkyBlue);
            brushes.Add(Brushes.Olive);
            brushes.Add(Brushes.Orange);
            brushes.Add(Brushes.PaleGreen);
            brushes.Add(Brushes.Peru);
            brushes.Add(Brushes.PeachPuff);
            brushes.Add(Brushes.Purple);
            brushes.Add(Brushes.Salmon);
            brushes.Add(Brushes.SlateBlue);
            brushes.Add(Brushes.Yellow);
            brushes.Add(Brushes.YellowGreen);

            /*Brush result = Brushes.Transparent;

            Random rnd = new Random();

            Type brushesType = typeof(Brushes);

            PropertyInfo[] properties = brushesType.GetProperties();

            int random = rnd.Next(properties.Length);
            result = (Brush)properties[random].GetValue(null, null);*/

            return brushes[random.Next(brushes.Count)];
        }

        public GH_Capsule FloorName;//= GH_Capsule.CreateTextCapsule(new Rectangle(), new Rectangle(), GH_Palette.Black, p);
        public GH_Capsule HouseName;//= GH_Capsule.CreateTextCapsule(new Rectangle(), new Rectangle(), GH_Palette.Black, "RoomName");

        Rectangle FloorNameRectangle;
        Rectangle HouseNameRectangle;

        const int InflateAmount = 2; // Used to inflate all rectangles for producing outer rectangles for GH_TextCapsules
        const int InnerComponentRadius = 50; // Used to define the radius of the main circle
        const int OuterComponentRadius = 60; // Used to define the radius of the main circle

        public static Random random = new Random();
        public Brush houseBrush;


        public List<RoomInstance> roomInstancesList = new List<RoomInstance>(); // List that contains all room instances that are to be in that house


        protected override void Layout()
        {

            base.Layout();

            Pivot = GH_Convert.ToPoint(Pivot);
            Rectangle rec0 = GH_Convert.ToRectangle(Bounds);
            rec0.Height += 50;
            Bounds = rec0;


            HouseNameRectangle = new Rectangle(new System.Drawing.Point((int)Bounds.Location.X - 50 + 25, (int)Bounds.Location.Y + 50), new Size(80, 20));
            FloorNameRectangle = new Rectangle(new System.Drawing.Point((int)Bounds.Location.X - 50 + 50, (int)Bounds.Location.Y + 75), new Size(60, 20));

            //   Bounds = new RectangleF(Pivot.X - OuterComponentRadius, Pivot.Y - OuterComponentRadius, 2 * OuterComponentRadius, 2 * OuterComponentRadius);
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

        protected RectangleF OuterComponentBounds
        {
            get
            {
                RectangleF outer = Bounds;
                int inflation = OuterComponentRadius - InnerComponentRadius;
                outer.Inflate(inflation, inflation);
                return outer;
            }
        }

        /*  public override bool IsPickRegion(PointF point)
          {
              return Grasshopper.GUI.GH_GraphicsUtil.IsPointInEllipse(Bounds, point);
          }*/
        protected Rectangle InflateRect(Rectangle rect, int a = 5, int b = 5)
        {
            Rectangle rectOut = rect;
            rectOut.Inflate(-a, -b);
            return rectOut;
        }


        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            //Bounds = new RectangleF(Pivot.X - OuterComponentRadius, Pivot.Y - OuterComponentRadius, 2 * OuterComponentRadius, 2 * OuterComponentRadius);
            //  Bounds = InnerComponentBounds;
            //         base.Render(canvas, graphics, channel);

            if (Owner is HouseInstance houseInstance)
                if (channel == GH_CanvasChannel.Objects)
                {
                    graphics.FillRectangle(houseBrush, OuterComponentBounds);
                    base.Render(canvas, graphics, channel);

                    //       Bounds = InnerComponentBounds;
                    //    GH_Capsule.CreateCapsule(Bounds, GH_Palette.Grey).Render(graphics, Color.Gray);

                    //       Bounds = new RectangleF(Pivot.X - OuterComponentRadius, Pivot.Y - 2 * OuterComponentRadius, 2 * OuterComponentRadius, OuterComponentRadius);
                    //     Layout();
                    //   GH_ComponentAttributes.RenderComponentParameters(canvas, graphics, Owner, new GH_PaletteStyle(Color.Brown));

                    //  Bounds = new RectangleF(Pivot.X - OuterComponentRadius, Pivot.Y - OuterComponentRadius, 2 * OuterComponentRadius, 2 * OuterComponentRadius);


                    //    foreach (IGH_DocumentObject obj in roomInstancesList)
                    //        DrawTargetArrow(graphics, obj.Attributes.Bounds);


                    //    GH_Capsule capsule = GH_Capsule.CreateCapsule(InnerComponentBounds, GH_Palette.Normal, InnerComponentRadius, 0);
                    //    capsule.Render(graphics, Selected, Owner.Locked, true);
                    //    capsule.Dispose();


                    graphics.DrawString("A:", SystemFonts.IconTitleFont, Brushes.Black, new RectangleF(new System.Drawing.Point((int)Bounds.Location.X + 30, (int)Bounds.Location.Y + 77), new Size(20, 20)));

                    HouseName = GH_Capsule.CreateTextCapsule(HouseNameRectangle, InflateRect(HouseNameRectangle, InflateAmount, InflateAmount), GH_Palette.Pink, houseInstance.HouseName);
                    HouseName.Render(graphics, GH_Skin.palette_grey_standard);
                    HouseName.Dispose();

                    FloorName = GH_Capsule.CreateTextCapsule(FloorNameRectangle, InflateRect(FloorNameRectangle, InflateAmount, InflateAmount), GH_Palette.Pink, houseInstance.FloorName.ToString());
                    FloorName.Render(graphics, GH_Skin.palette_white_standard);
                    FloorName.Dispose();



                }
                else
                {
                    //  foreach (IGH_DocumentObject obj in roomInstancesList)
                    //     DrawTargetArrow(graphics, obj.Attributes.Bounds);

                    base.Render(canvas, graphics, channel);

                }
        }

        public PointF CircleClosesPoint(PointF point, RectangleF circle)
        {
            Vector2d vec = new Vector2d(circle.X + circle.Width / 2 - point.X, circle.Y + circle.Width / 2 - point.Y);
            vec.Unitize();
            vec = new Vector2d(vec.X * circle.Width / 2, vec.Y * circle.Width / 2);
            return (new PointF((float)(circle.Location.X + circle.Width / 2 + -vec.X), (float)(circle.Location.Y + circle.Width / 2 - vec.Y)));
        }

        private void DrawTargetArrow(Graphics graphics, RectangleF target)
        {
            //  double distance = Grasshopper.GUI.GH_GraphicsUtil.Distance(Pivot, cp);
            //  if (distance < OuterComponentRadius)
            //      return;

            Circle circle = new Circle(new Point3d(Pivot.X, Pivot.Y, 0.0), OuterComponentRadius - 2);
            PointF tp;// = GH_Convert.ToPointF(circle.ClosestPoint(new Point3d(cp.X, cp.Y, 0.0)));
            tp = Grasshopper.GUI.GH_GraphicsUtil.BoxClosestPoint(new PointF(target.Location.X + target.Width / 2, target.Location.Y + target.Height / 2), Bounds);

            PointF cp = CircleClosesPoint(tp, target);

            Pen arrowPen = new Pen(houseBrush, (OuterComponentRadius - InnerComponentRadius) / 8);
            arrowPen.EndCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
            arrowPen.StartCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
            graphics.DrawLine(arrowPen, tp, cp);
            arrowPen.Dispose();
        }

        public void UpdateRoomInstancesColors()
        {
            foreach (RoomInstance childRoom in roomInstancesList)
            {
                if ((childRoom.Attributes as RoomInstanceAttributes).roomBrush != houseBrush)
                {
                    (childRoom.Attributes as RoomInstanceAttributes).roomBrush = houseBrush;
                    childRoom.ExpireSolution(true);
                }
            }

        }


        public void AddAdjacence(IGH_DocumentObject a)
        {

            if (roomInstancesList.Find(item => item.RoomId == (a as RoomInstance).RoomId) == null)
            {
                roomInstancesList.Add(a as RoomInstance);

                if (((a as RoomInstance).Attributes as RoomInstanceAttributes).AssignedHouseInstance != null &&
                    ((a as RoomInstance).Attributes as RoomInstanceAttributes).AssignedHouseInstance != this.Owner as HouseInstance)
                    (((a as RoomInstance).Attributes as RoomInstanceAttributes).AssignedHouseInstance.Attributes as HouseInstanceAttributes).RemoveAdjacence(a as RoomInstance);

                if (((a as RoomInstance).Attributes as RoomInstanceAttributes).AssignedHouseInstance != this.Owner as HouseInstance)
                    ((a as RoomInstance).Attributes as RoomInstanceAttributes).AssignedHouseInstance = this.Owner as HouseInstance;


                foreach (RoomInstance childRoom in (a as RoomInstance).AdjacentRoomsList)
                    if (roomInstancesList.Find(item => item.RoomId == childRoom.RoomId) == null)
                        this.AddAdjacence(childRoom);
            }

            UpdateRoomInstancesColors();
        }


        // Removes the RoomInstance and all connected to it RoomInstances
        public void RemoveAdjacence(IGH_DocumentObject a)
        {
            roomInstancesList.Remove(a as RoomInstance);
            ((a as RoomInstance).Attributes as RoomInstanceAttributes).roomBrush = Brushes.Gray;
            a.OnDisplayExpired(true);

            foreach (RoomInstance childRoom in (a as RoomInstance).AdjacentRoomsList)
                if (roomInstancesList.Find(item => item.RoomId == childRoom.RoomId) != null)
                    RemoveAdjacence(childRoom);

            //  throw new NotImplementedException();
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            string roomInstancesListString = "";
            foreach (RoomInstance room in roomInstancesList)
                roomInstancesListString += ((int)(room.Attributes.Pivot.X + room.Attributes.Bounds.Width / 2)).ToString() + "!" +
                    ((int)(room.Attributes.Pivot.Y + room.Attributes.Bounds.Height / 2)).ToString() + "@";

            if (roomInstancesListString.Length > 0)
                roomInstancesListString.Remove(roomInstancesListString.Length - 1);

            writer.SetString("RoomInstancesListString", roomInstancesListString);
            writer.SetString("HouseName", (Owner as HouseInstance).HouseName);
            writer.SetString("FloorName", (Owner as HouseInstance).FloorName);
            writer.SetBoolean("TryRotateBoundary", (Owner as HouseInstance).tryRotateBoundary);

            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            string roomInstancesListString = reader.GetString("RoomInstancesListString");
            string[] strArray = roomInstancesListString.Split('@');

            foreach (string guidS in strArray) try
                {
                    roomInstancesList.Add(Owner.OnPingDocument().FindComponent(
                        new System.Drawing.Point(int.Parse(guidS.Split('!')[0]), int.Parse(guidS.Split('!')[1]))) as RoomInstance);
                }
                catch (Exception e) { }
            //  roomInstancesListString.Remove(roomInstancesListString.Length - 1);

            (Owner as HouseInstance).HouseName = reader.GetString("HouseName");
            (Owner as HouseInstance).FloorName = reader.GetString("FloorName");
            (Owner as HouseInstance).tryRotateBoundary = reader.GetBoolean("TryRotateBoundary");

            Owner.ExpireSolution(true);

            return base.Read(reader);
        }

    }
}
