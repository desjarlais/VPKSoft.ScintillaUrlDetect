#region License
/*
MIT License

Copyright(c) 2019 Petteri Kautonen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using VPKSoft.ScintillaUrlDetect;

namespace TestApp
{
    public partial class FormMain : Form
    {
        private ScintillaUrlDetect urlDetect;

        public FormMain()
        {
            InitializeComponent();

            ScintillaUrlDetect.UseThreadsOnUrlStyling = true;
            ScintillaUrlDetect.AutoEllipsisUrlLength = 50;

            urlDetect = new ScintillaUrlDetect(scintillaTest);
        }

        private void mnuOpenFile_Click(object sender, EventArgs e)
        {
            if (odAnyFile.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    scintillaTest.Text = File.ReadAllText(odAnyFile.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($@"Error occurred while opening file: '{odAnyFile.FileName}': '{ex.Message}'.",
                        @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }
            }
        }

        private void mnuThreadingEnabled_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                var menuItem = (ToolStripMenuItem) sender;
                ScintillaUrlDetect.UseThreadsOnUrlStyling = menuItem.Checked;
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"An exception occurred: '{ex.Message}'.",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
        }

        private void mnuManualStyling_Click(object sender, EventArgs e)
        {
            urlDetect.MarkUrls();
        }

        private void mnuClearStyling_Click(object sender, EventArgs e)
        {
            urlDetect.ClearIndicators();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // THIS IS VERY IMPORTANT..DO DISPOSE!!
            using (urlDetect)
            {
                urlDetect = null;
            }
        }

        private void mnuSpeedTest_Click(object sender, EventArgs e)
        {
            if (mnuThreadingEnabled.Checked)
            {
                mnuThreadingEnabled.Checked = false;
                ScintillaUrlDetect.UseThreadsOnUrlStyling = false;
            }

            double totalSeconds = 0;

            for (int i = 0; i < 100; i++)
            {
                DateTime dt1 = DateTime.Now;
                urlDetect.MarkUrls();

                var passed = (DateTime.Now - dt1).TotalSeconds;
                totalSeconds += passed;

                Debug.WriteLine((i + 1) + " / 100: " + passed);
            }

            totalSeconds /= 100.0;

            MessageBox.Show(@"Average time passed (seconds, 100 round): " + totalSeconds);
        }
    }
}
