using System;
using System.Drawing;

namespace SmartGoldbergEmu.Models
{
    public class WindowState
    {
        public Size Size { get; set; }

        public Point Location { get; set; }

        public System.Windows.Forms.FormWindowState State { get; set; }

        public WindowState()
        {
            Size = new Size(330, 450);
            Location = new Point(100, 100);
            State = System.Windows.Forms.FormWindowState.Normal;
        }
    }
}
