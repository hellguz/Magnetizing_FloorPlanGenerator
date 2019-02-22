using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Magnetizing_FPG
{
    public class Magnetizing_FPGInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Magnetizing_FPG";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("802849a2-befa-47bb-9c19-a2d1f328f105");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}
