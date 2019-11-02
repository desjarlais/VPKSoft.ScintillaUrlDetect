namespace TestApp
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.scintillaTest = new ScintillaNET.Scintilla();
            this.msMain = new System.Windows.Forms.MenuStrip();
            this.mnuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuOpenFile = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuThreadingEnabled = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuManualStyling = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuClearStyling = new System.Windows.Forms.ToolStripMenuItem();
            this.odAnyFile = new System.Windows.Forms.OpenFileDialog();
            this.msMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // scintillaTest
            // 
            this.scintillaTest.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.scintillaTest.Location = new System.Drawing.Point(12, 27);
            this.scintillaTest.Name = "scintillaTest";
            this.scintillaTest.Size = new System.Drawing.Size(900, 522);
            this.scintillaTest.TabIndex = 0;
            // 
            // msMain
            // 
            this.msMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuFile});
            this.msMain.Location = new System.Drawing.Point(0, 0);
            this.msMain.Name = "msMain";
            this.msMain.Size = new System.Drawing.Size(924, 24);
            this.msMain.TabIndex = 1;
            this.msMain.Text = "menuStrip1";
            // 
            // mnuFile
            // 
            this.mnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuOpenFile,
            this.mnuThreadingEnabled,
            this.mnuManualStyling,
            this.mnuClearStyling});
            this.mnuFile.Name = "mnuFile";
            this.mnuFile.Size = new System.Drawing.Size(37, 20);
            this.mnuFile.Text = "File";
            // 
            // mnuOpenFile
            // 
            this.mnuOpenFile.Name = "mnuOpenFile";
            this.mnuOpenFile.Size = new System.Drawing.Size(172, 22);
            this.mnuOpenFile.Text = "Open";
            this.mnuOpenFile.Click += new System.EventHandler(this.mnuOpenFile_Click);
            // 
            // mnuThreadingEnabled
            // 
            this.mnuThreadingEnabled.CheckOnClick = true;
            this.mnuThreadingEnabled.Name = "mnuThreadingEnabled";
            this.mnuThreadingEnabled.Size = new System.Drawing.Size(172, 22);
            this.mnuThreadingEnabled.Text = "Threading enabled";
            this.mnuThreadingEnabled.CheckedChanged += new System.EventHandler(this.mnuThreadingEnabled_CheckedChanged);
            // 
            // mnuManualStyling
            // 
            this.mnuManualStyling.Name = "mnuManualStyling";
            this.mnuManualStyling.Size = new System.Drawing.Size(172, 22);
            this.mnuManualStyling.Text = "Manual styling";
            this.mnuManualStyling.Click += new System.EventHandler(this.mnuManualStyling_Click);
            // 
            // mnuClearStyling
            // 
            this.mnuClearStyling.Name = "mnuClearStyling";
            this.mnuClearStyling.Size = new System.Drawing.Size(172, 22);
            this.mnuClearStyling.Text = "Clear styling";
            this.mnuClearStyling.Click += new System.EventHandler(this.mnuClearStyling_Click);
            // 
            // odAnyFile
            // 
            this.odAnyFile.Filter = "All Files|*.*";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(924, 561);
            this.Controls.Add(this.scintillaTest);
            this.Controls.Add(this.msMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.msMain;
            this.Name = "FormMain";
            this.Text = "A test application for the VPKSoft.ScintillaUrlDetect class library";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.msMain.ResumeLayout(false);
            this.msMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ScintillaNET.Scintilla scintillaTest;
        private System.Windows.Forms.MenuStrip msMain;
        private System.Windows.Forms.ToolStripMenuItem mnuFile;
        private System.Windows.Forms.ToolStripMenuItem mnuOpenFile;
        private System.Windows.Forms.OpenFileDialog odAnyFile;
        private System.Windows.Forms.ToolStripMenuItem mnuThreadingEnabled;
        private System.Windows.Forms.ToolStripMenuItem mnuManualStyling;
        private System.Windows.Forms.ToolStripMenuItem mnuClearStyling;
    }
}

