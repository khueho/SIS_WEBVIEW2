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
            lblIds = new Label();
            txtPatientIds = new TextBox();
            btnStart = new Button();
            btnStop = new Button();
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
            panelControl.Height = 80;
            panelControl.Padding = new Padding(8, 6, 8, 6);
            panelControl.Controls.Add(lblIds);
            panelControl.Controls.Add(txtPatientIds);
            panelControl.Controls.Add(lblDelay);
            panelControl.Controls.Add(numDelay);
            panelControl.Controls.Add(btnStart);
            panelControl.Controls.Add(btnStop);
            panelControl.Controls.Add(lblStatus);

            //
            // lblIds
            //
            lblIds.AutoSize = true;
            lblIds.ForeColor = Color.Silver;
            lblIds.Font = new Font("Segoe UI", 8.5f);
            lblIds.Text = "Danh sách Patient ID\n(mỗi ID một dòng):";
            lblIds.Location = new Point(8, 10);

            //
            // txtPatientIds
            //
            txtPatientIds.Multiline = true;
            txtPatientIds.ScrollBars = ScrollBars.Vertical;
            txtPatientIds.Font = new Font("Consolas", 9f);
            txtPatientIds.BackColor = Color.FromArgb(50, 50, 65);
            txtPatientIds.ForeColor = Color.White;
            txtPatientIds.BorderStyle = BorderStyle.FixedSingle;
            txtPatientIds.Location = new Point(160, 8);
            txtPatientIds.Size = new Size(300, 64);
            txtPatientIds.PlaceholderText = "VD: ID001\nID002\nID003";

            //
            // lblDelay
            //
            lblDelay.AutoSize = true;
            lblDelay.ForeColor = Color.Silver;
            lblDelay.Font = new Font("Segoe UI", 8.5f);
            lblDelay.Text = "Delay (ms):";
            lblDelay.Location = new Point(470, 10);

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
            numDelay.Location = new Point(470, 28);
            numDelay.Width = 90;

            //
            // btnStart
            //
            btnStart.Text = "▶ Bắt đầu";
            btnStart.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnStart.BackColor = Color.FromArgb(50, 180, 100);
            btnStart.ForeColor = Color.White;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Size = new Size(100, 30);
            btnStart.Location = new Point(575, 8);
            btnStart.Cursor = Cursors.Hand;
            btnStart.Click += BtnStart_Click;

            //
            // btnStop
            //
            btnStop.Text = "■ Dừng";
            btnStop.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnStop.BackColor = Color.FromArgb(200, 60, 60);
            btnStop.ForeColor = Color.White;
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Size = new Size(100, 30);
            btnStop.Location = new Point(575, 42);
            btnStop.Cursor = Cursors.Hand;
            btnStop.Enabled = false;
            btnStop.Click += BtnStop_Click;

            //
            // lblStatus
            //
            lblStatus.AutoSize = false;
            lblStatus.Size = new Size(250, 64);
            lblStatus.Location = new Point(685, 8);
            lblStatus.ForeColor = Color.LightGreen;
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            lblStatus.Text = "Sẵn sàng";
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
        private Label lblIds;
        private TextBox txtPatientIds;
        private Button btnStart;
        private Button btnStop;
        private Label lblStatus;
        private NumericUpDown numDelay;
        private Label lblDelay;
    }
}
