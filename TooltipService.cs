using System;
using System.Drawing;
using System.Windows.Forms;

namespace PeekThrough
{
    /// <summary>
    /// Shows and hides the "Ghost Mode" tooltip
    /// </summary>
    internal class TooltipService : IDisposable
    {
        private Form _tooltipForm;
        private Label _tooltipLabel;
        private bool _disposed = false;

        private const int TOOLTIP_WIDTH = 140;
        private const int TOOLTIP_HEIGHT = 40;
        private const int TOOLTIP_OFFSET_X = 20;
        private const int TOOLTIP_OFFSET_Y = 20;

        public TooltipService()
        {
            InitializeTooltip();
        }

        private void InitializeTooltip()
        {
            _tooltipForm = new Form();
            _tooltipForm.FormBorderStyle = FormBorderStyle.None;
            _tooltipForm.ShowInTaskbar = false;
            _tooltipForm.TopMost = true;
            _tooltipForm.BackColor = Color.FromArgb(255, 255, 225); // LightYellow
            _tooltipForm.Size = new Size(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
            _tooltipForm.StartPosition = FormStartPosition.Manual;
            _tooltipForm.Opacity = 0.95;

            // Disable interaction: window without focus and input
            _tooltipForm.Enabled = false;
            _tooltipForm.ShowIcon = false;
            _tooltipForm.ControlBox = false;

            _tooltipLabel = new Label();
            _tooltipLabel.Text = "Ghost Mode";
            _tooltipLabel.AutoSize = true;
            _tooltipLabel.Location = new Point(5, 5);
            _tooltipLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _tooltipForm.Controls.Add(_tooltipLabel);
            _tooltipForm.AutoSize = true;
            _tooltipLabel.AutoSize = true;

            // Set window style after handle creation for click-through
            _tooltipForm.Load += (s, e) =>
            {
                int exStyle = NativeMethods.GetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE).ToInt32();
                NativeMethods.SetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE,
                    new IntPtr(exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE));
            };
        }

        public void Show(Point location, string text = null)
        {
            if (_disposed) return;

            if (text != null)
                _tooltipLabel.Text = text;

            _tooltipForm.Location = new Point(location.X + TOOLTIP_OFFSET_X, location.Y + TOOLTIP_OFFSET_Y);
            if (!_tooltipForm.Visible)
                _tooltipForm.Show();
        }

        public void Hide()
        {
            if (_disposed) return;
            _tooltipForm.Hide();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_tooltipForm != null)
            {
                _tooltipForm.Dispose();
                _tooltipForm = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
