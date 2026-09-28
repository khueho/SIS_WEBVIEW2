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
            panelTopTools = new FlowLayoutPanel();
            chkAuto15m = new CheckBox();
            lblTimer = new Label();
            lblSummary = new Label();

            ((System.ComponentModel.ISupportInitialize)webView21).BeginInit();
            panelControl.SuspendLayout();
            panelTopTools.SuspendLayout();
            SuspendLayout();

            // 
            // panelControl
            // 
            panelControl.BackColor = Color.FromArgb(30, 30, 40);
            panelControl.Dock = DockStyle.Top;
            panelControl.Height = 76;
            panelControl.Padding = new Padding(10, 4, 10, 4);
            panelControl.Controls.Add(lblStatus);
            panelControl.Controls.Add(panelTopTools);

            // 
            // panelTopTools
            // 
            panelTopTools.Dock = DockStyle.Top;
            panelTopTools.Height = 36;
            panelTopTools.FlowDirection = FlowDirection.LeftToRight;
            panelTopTools.WrapContents = false;
            panelTopTools.Controls.Add(chkAuto15m);
            panelTopTools.Controls.Add(lblTimer);
            panelTopTools.Controls.Add(lblSummary);

            // 
            // chkAuto15m
            // 
            chkAuto15m.Text = "Tự động quét mỗi 15 phút";
            chkAuto15m.Checked = true;
            chkAuto15m.AutoSize = true;
            chkAuto15m.Height = 30;
            chkAuto15m.ForeColor = Color.White;
            chkAuto15m.Font = new Font("Segoe UI", 9f);
            chkAuto15m.Margin = new Padding(0, 5, 16, 2);
            chkAuto15m.CheckedChanged += ChkAuto15m_CheckedChanged;

            // 
            // lblTimer
            // 
            lblTimer.Text = "⏳ Tiếp theo: 15:00";
            lblTimer.AutoSize = true;
            lblTimer.Height = 30;
            lblTimer.TextAlign = ContentAlignment.MiddleLeft;
            lblTimer.ForeColor = Color.Gold;
            lblTimer.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblTimer.Margin = new Padding(0, 6, 20, 2);

            // 
            // lblSummary
            // 
            lblSummary.Text = "📊 Đang tải thống kê...";
            lblSummary.AutoSize = true;
            lblSummary.Height = 30;
            lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            lblSummary.ForeColor = Color.Cyan;
            lblSummary.Font = new Font("Segoe UI", 9f);
            lblSummary.Margin = new Padding(0, 6, 0, 2);

            //
            // lblStatus
            //
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = Color.LightGreen;
            lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
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
            ClientSize = new Size(1100, 700);
            Controls.Add(webView21);
            Controls.Add(panelControl);
            Name = "Form1";
            Text = "SIS Auto - Quản Lý & Kích Hoạt Bệnh Nhân";
            Load += Form1_Load;

            panelTopTools.ResumeLayout(false);
            panelTopTools.PerformLayout();
            panelControl.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)webView21).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webView21;
        private Panel panelControl;
        private FlowLayoutPanel panelTopTools;
        private CheckBox chkAuto15m;
        private Label lblTimer;
        private Label lblSummary;
        private Label lblStatus;
    }
}
