namespace SIS_WEBVIEW2
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            webView21 = new Microsoft.Web.WebView2.WinForms.WebView2();
            panelControl = new Panel();
            lblStatus = new Label();
            numDelay = new NumericUpDown();
            lblDelay = new Label();

            ((System.ComponentModel.ISupportInitialize)webView21).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numDelay).BeginInit();
            panelControl.SuspendLayout();
            SuspendLayout();

            // 
            // panelControl
            // 
            panelControl.BackColor = Color.FromArgb(30, 30, 40);
            panelControl.Dock = DockStyle.Top;
            panelControl.Height = 65;
            panelControl.Padding = new Padding(8, 6, 8, 6);
            panelControl.Controls.Add(lblDelay);
            panelControl.Controls.Add(numDelay);
            panelControl.Controls.Add(lblStatus);

            //
            // lblDelay
            //
            lblDelay.AutoSize = true;
            lblDelay.ForeColor = Color.Silver;
            lblDelay.Font = new Font("Segoe UI", 8.5f);
            lblDelay.Text = "Delay (ms):";
            lblDelay.Location = new Point(8, 10);

            //
            // numDelay
            //
            numDelay.Minimum = 200;
            numDelay.Maximum = 10000;
            numDelay.Value = 1000;
            numDelay.Increment = 100;
            numDelay.Font = new Font("Segoe UI", 9f);
            numDelay.BackColor = Color.FromArgb(50, 50, 65);
            numDelay.ForeColor = Color.White;
            numDelay.Location = new Point(8, 28);
            numDelay.Width = 90;

            //
            // lblStatus
            //
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(850, 50);
            lblStatus.Location = new Point(115, 8);
            lblStatus.ForeColor = Color.LightGreen;
            lblStatus.Font = new Font("Segoe UI", 9f);
            lblStatus.Text = "Đang khởi động app...";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // 
            // webView21
            // 
            webView21.AllowExternalDrop = true;
            webView21.CreationProperties = null;
            webView21.DefaultBackgroundColor = Color.White;
            webView21.Dock = DockStyle.Fill;
            webView21.Name = "webView21";
            webView21.TabIndex = 0;
            webView21.ZoomFactor = 1D;

            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 650);
            Controls.Add(webView21);
            Controls.Add(panelControl);
            Name = "Form1";
            Text = "SIS WebView2 - Auto PatientID";
            Load += Form1_Load;

            panelControl.ResumeLayout(false);
            panelControl.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)webView21).EndInit();
            ((System.ComponentModel.ISupportInitialize)numDelay).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webView21;
        private Panel panelControl;
        private Label lblStatus;
        private NumericUpDown numDelay;
        private Label lblDelay;
    }
}
