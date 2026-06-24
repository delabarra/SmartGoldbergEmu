using System;
using System.Drawing;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the window state information for the application.
    /// </summary>
    public class WindowState
    {
        /// <summary>
        /// Gets or sets the window size.
        /// </summary>
        public Size Size { get; set; }

        /// <summary>
        /// Gets or sets the window position.
        /// </summary>
        public Point Location { get; set; }

        /// <summary>
        /// Gets or sets the window state (Normal, Minimized, Maximized).
        /// </summary>
        public System.Windows.Forms.FormWindowState State { get; set; }

        /// <summary>
        /// Initializes a new instance of the WindowState class with default values.
        /// </summary>
        public WindowState()
        {
            Size = new Size(330, 450);
            Location = new Point(100, 100);
            State = System.Windows.Forms.FormWindowState.Normal;
        }
    }
}
