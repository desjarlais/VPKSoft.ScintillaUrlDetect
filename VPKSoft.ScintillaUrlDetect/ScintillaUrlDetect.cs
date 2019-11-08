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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using PropertyChanged;
using ScintillaNET;

// Thanks to: https://github.com/jacobslusser/ScintillaNET/issues/111

namespace VPKSoft.ScintillaUrlDetect
{
    /// <summary>
    /// A class to help marking URLs within a <see cref="Scintilla"/> document.
    /// REMEMBER to dispose of this class when not required anymore; otherwise the application dead-locks in case the instance is using a thread.
    /// Implements the <see cref="VPKSoft.ScintillaUrlDetect.ErrorHandlingBase" />
    /// Implements the <see cref="System.ComponentModel.INotifyPropertyChanged" />
    /// Implements the <see cref="System.IDisposable" />
    /// </summary>
    /// <seealso cref="VPKSoft.ScintillaUrlDetect.ErrorHandlingBase" />
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    /// <seealso cref="System.IDisposable" />
    public class ScintillaUrlDetect : ErrorHandlingBase, INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:VPKSoft.ScintillaUrlDetect.ScintillaUrlDetect"/> class.
        /// </summary>
        /// <param name="scintilla">The <see cref="Scintilla"/> control which URLs to mark and to handle.</param>
        public ScintillaUrlDetect(Scintilla scintilla)
        {
            this.scintilla = scintilla; // set the scintilla document..

            // subscribe the required events..
            scintilla.TextChanged += Scintilla_TextChanged;
            scintilla.MouseMove += Scintilla_MouseMove;
            scintilla.IndicatorClick += Scintilla_IndicatorClick;
            scintilla.DwellStart += Scintilla_DwellStart;
            scintilla.DwellEnd += Scintilla_DwellEnd;
            scintilla.UpdateUI += Scintilla_UpdateUI;
            scintilla.SizeChanged += Scintilla_SizeChanged;

            // initialize the URL styling indicators..
            InitializeIndicators();

            // configure the URL mouse dwell tool tip..
            SetScintillaDwellToolTip();

            if (UseThreadsOnUrlStyling) // if the thread usage is allowed..
            {
                CreateThread(); // ..create an URL styling thread..
            }

            // add me to the list of instances..
            MyInstances.Add(this);
        }

        #region PrivateFields
        /// <summary>
        /// A field where the Scintilla instance is saved from the constructor.
        /// </summary>
        private readonly Scintilla scintilla;
        #endregion

        #region PrivateMethods
        /// <summary>
        /// Gets the URL at position (character position in the <see cref="Scintilla"/> control).
        /// </summary>
        /// <param name="position">The position of which URL to get.</param>
        /// <returns>An instance to a UrlMatch class if there is an URL at the given <paramref name="position"/>; otherwise null.</returns>
        private UrlMatch GetUrlAtPosition(int position)
        {
            return UrlMatches.FirstOrDefault(f => position >= f.StartIndex && position <= f.EndIndex);
        }

        /// <summary>
        /// Initializes the indicators.
        /// </summary>
        private void InitializeIndicators()
        {
            ScintillaUrlIndicator.ForeColor = ScintillaUrlIndicatorColor;
            ScintillaUrlIndicator.Style = ScintillaUrlIndicatorStyle;

            ScintillaUrlTextIndicator.ForeColor = ScintillaUrlTextIndicatorColor;
            ScintillaUrlTextIndicator.Style = ScintillaUrlTextIndicatorStyle;
            NeedsUrlStylingContent = true;
        }
        #endregion

        #region PrivateProperties
        /// <summary>
        /// Gets or sets the <see cref="Scintilla"/> control's default cursor.
        /// </summary>
        private Cursor ScintillaDefaultCursor { get; } = Cursors.IBeam;

        /// <summary>
        /// Gets or sets the current cursor of the <see cref="Scintilla"/> control.
        /// </summary>
        private Cursor CurrentCursor { get; set; } = Cursors.IBeam;

        /// <summary>
        /// Gets or sets the hyper link / URL cursor.
        /// </summary>
        public Cursor HyperLinkCursor { get; set; } = Cursors.Hand;

        /// <summary>
        /// A list of instances of this class. This is used for instance-wide static property notification.
        /// </summary>
        private static List<ScintillaUrlDetect> MyInstances { get; } = new List<ScintillaUrlDetect>();
        #endregion

        #region CheckThread
        /// <summary>
        /// The thread to append the URL styling for the Scintilla control.
        /// </summary>
        private Thread waitUrlStylingThread;

        /// <summary>
        /// The amount of time the thread has been running in milliseconds.
        /// </summary>
        private volatile int threadSpentTimeMs;

        /// <summary>
        /// A flag to indicate the URL styling thread should run the Scintilla URL styling loop.
        /// </summary>
        private volatile bool stopUrlStylingThread;

        /// <summary>
        /// A dummy object for thread locking.
        /// </summary>
        private readonly object waitUrlStylingThreadLock = new object();

        // a field to hold the UseThreadsOnUrlStyling property value..
        // ReSharper disable once InconsistentNaming
        private static bool useThreadsOnUrlStyling = true;

        // a field to hold the ThreadSuspended property value..
        private volatile bool threadSuspended;

        /// <summary>
        /// Gets or sets the value whether the URL checking thread should be suspended.
        /// </summary>
        public bool ThreadSuspended
        {
            get => threadSuspended;
            set => threadSuspended = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether use threads on the URL styling.
        /// If set to false a manual call to <see cref="MarkUrls"/> method is required with self-built logic with the <see cref="Scintilla"/> control.
        /// This property value affects all instances of the <see cref="ScintillaUrlDetect"/>.
        /// </summary>
        public static bool UseThreadsOnUrlStyling
        {
            get => useThreadsOnUrlStyling;

            set
            {
                if (value && !useThreadsOnUrlStyling) // enable the threads..
                {
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int i = 0; i < MyInstances.Count; i++)
                    {
                        MyInstances[i].CreateThread();
                    }
                }

                if (!value && useThreadsOnUrlStyling) // disable the treads..
                {
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int i = 0; i < MyInstances.Count; i++)
                    {
                        MyInstances[i].CancelThread();
                    }
                }

                // save the value..
                useThreadsOnUrlStyling = value;
            }
        }

        /// <summary>
        /// Forces the URL styling thread to stop by using the <see cref="System.Threading.Thread.Abort()"/> method.
        /// This method is to be used only if the calls to the <see cref="System.Threading.Thread.Join()"/> calls fail.
        /// </summary>
        public void ForceDestroyThread()
        {
            try
            {
                if (waitUrlStylingThread != null)
                {
                    if (!waitUrlStylingThread.Join(2000)) // give it 2 seconds to stop..
                    {
                        waitUrlStylingThread.Abort(); // ..on fail raise an exception within the thread..
                    }

                    waitUrlStylingThread = null;
                }
            }
            catch (Exception ex)
            {
                // report the exception..
                ExceptionLogAction?.Invoke(ex);
            }
        }

        /// <summary>
        /// Creates a thread for the URL indicator styling.
        /// </summary>
        private void CreateThread()
        {
            // in case there is already a thread, which should not happen; for safety..
            CancelThread();

            //lock (waitUrlStylingThreadLock) NOT NEEDED?
            {
                // the thread method "exits" if this is set to true..
                stopUrlStylingThread = false;

                // create the styling thread..
                waitUrlStylingThread = new Thread(UrlStylingThreadFunction);

                // start the styling thread..
                waitUrlStylingThread.Start();
            }
        }

        /// <summary>
        /// Destroys the thread for the URL indicator styling.
        /// </summary>
        private void CancelThread()
        {
            if (waitUrlStylingThread != null)
            {
                //lock (waitUrlStylingThreadLock) NOT NEEDED?
                {
                    stopUrlStylingThread = true;
                    int cycleCount = 0;
                    bool joinSuccess = false;

                    while (!waitUrlStylingThread.Join(100) && cycleCount < 300)
                    {
                        Application.DoEvents();
                        cycleCount++;
                        joinSuccess = true;
                    }

                    if (!joinSuccess)
                    {
                        ForceDestroyThread();
                    }

                    waitUrlStylingThread = null;
                }
            }
        }

        /// <summary>
        /// The URL styling thread function.
        /// </summary>
        private void UrlStylingThreadFunction()
        {
            while (!stopUrlStylingThread) // run while you can..
            {
                while (threadSuspended && !stopUrlStylingThread) // the thread can now be suspended..
                {
                    Thread.Sleep(5);
                }

                // if URL styling is required and the launch time has been passed..
                if (threadSpentTimeMs > UrlCheckIntervalContentsChange && NeedsUrlStylingContent ||
                    threadSpentTimeMs > UrlCheckIntervalUiUpdate && NeedUrlStylingUiUpdate && !StyleEntireDocument)
                {
                    // ..re-style the Scintilla control's URLs..
                    if (StyleEntireDocument)
                    {
                        MarkUrls();
                    }
                    else
                    {
                        MarkVisibleUrls(); 
                    }

                    threadSpentTimeMs = 0; // zero the time counter..
                    continue;
                }

                Thread.Sleep(5); // some sleeping (zzz)..
                threadSpentTimeMs += 5; // increase the re-style launch counter..
                if (threadSpentTimeMs > 1000000) // avoid arithmetic overflow..
                {
                    // ..just set the re-style launch counter to the defined interval..
                    threadSpentTimeMs = UrlCheckIntervalContentsChange; 
                }
            }
        }
        #endregion

        #region EventHandlers
        // handle the cursor set in case the mouse is on an URL..
        private void Scintilla_MouseMove(object sender, MouseEventArgs e)
        {
            var point = e.Location;

            // get the character index at the click location..
            int charPosition = scintilla.CharPositionFromPointClose(point.X, point.Y);

            // validate the character index and the indicator style at the location..
            if (charPosition != -1 && (scintilla.IndicatorOnFor(ScintillaUrlIndicatorIndex, charPosition) ||
                                       scintilla.IndicatorOnFor(ScintillaUrlTextIndicatorIndex, charPosition)))
            {
                if (CurrentCursor != HyperLinkCursor) // avoid excess message pumping to the Scintilla control..
                {
                    scintilla.SetCursor(HyperLinkCursor);
                    CurrentCursor = HyperLinkCursor;
                }
            }
            else
            {
                if (CurrentCursor != ScintillaDefaultCursor) // avoid excess message pumping to the Scintilla control..
                {
                    scintilla.SetCursor(ScintillaDefaultCursor);
                    CurrentCursor = ScintillaDefaultCursor;
                }
            }
        }

        // the text of the scintilla control changed, so set the NeedsUrlStylingContent flag to true..
        private void Scintilla_TextChanged(object sender, EventArgs e)
        {
            NeedsUrlStylingContent = true;
        }
        
        // the view area has been changed..
        private void Scintilla_UpdateUI(object sender, UpdateUIEventArgs e)
        {
            // the selection or content change doesn't matter with the URL detection..
            if (e.Change.HasFlag(UpdateChange.HScroll) |
                e.Change.HasFlag(UpdateChange.VScroll))
            {
                NeedUrlStylingUiUpdate = true;
            }
        }

        // the view area has been changed..
        private void Scintilla_SizeChanged(object sender, EventArgs e)
        {
            NeedUrlStylingUiUpdate = true;
        }

        // the mouse is no longer dwelling..
        private void Scintilla_DwellEnd(object sender, DwellEventArgs e)
        {
            scintilla.CallTipCancel(); // cancel the tool tip..
        }

        // the mouse started dwelling..
        private void Scintilla_DwellStart(object sender, DwellEventArgs e)
        {
            if (!UseDwellToolTip) // this shouldn't happen, but just in case..
            {
                return;
            }

            // get the URL at the mouse position..
            var match = GetUrlAtPosition(e.Position);
            if (match != null)
            {
                try
                {
                    // ..and if one exists set the tool tip text based on whether the link is an URL or a mailto link..
                    var callTip = match.IsMailToLink
                        ? string.Format(DwellToolTipTextMailTo, match.ContentsHumanReadable)
                        : string.Format(DwellToolTipTextUrl, match.ContentsHumanReadable);

                    // display the tip..
                    scintilla.CallTipShow(e.Position, callTip);
                }
                catch (Exception ex)
                {
                    // report the exception..
                    ExceptionLogAction?.Invoke(ex);
                }
            }
        }

        // a user clicked an indicator..
        private void Scintilla_IndicatorClick(object sender, IndicatorClickEventArgs e)
        {
            if (!AllowProcessStartOnUrlClick) // only start processes if allowed..
            {
                return;
            }

            if ((e.Modifiers & Keys.Control) == Keys.Control) // validate it was a CTRL+Click..
            {
                // https://github.com/VPKSoft/VPKSoft.ScintillaUrlDetect/issues/3#issuecomment-551234408
                var point = scintilla.PointToClient(Cursor.Position);
                if (scintilla.CharPositionFromPointClose(point.X, point.Y) == -1)
                {
                    return;
                }

                var match = GetUrlAtPosition(e.Position); // get the URL at the mouse position..
                if (match != null) // start a process if a link exists at the mouse position..
                {
                    try
                    {
                        Process.Start(match.ContentsTidy);
                        scintilla.Focus(); // without this some weird flickering occurred..
                    }
                    catch (Exception ex)
                    {
                        // report the exception..
                        ExceptionLogAction?.Invoke(ex);
                    }
                }
            }
        }
        #endregion

        #region ThreadSafeProperties
        // a field to hold the NeedsUrlStylingContent property value..
        private bool needUrlStylingContent;

        /// <summary>
        /// Gets or sets a value indicating whether the URL styling is required for the <see cref="Scintilla"/> control via contents change.
        /// </summary>
        [DoNotNotify] // this property notifies by it self..
        private bool NeedsUrlStylingContent 
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return needUrlStylingContent; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(NeedsUrlStylingContent), needUrlStylingContent, value);
                    needUrlStylingContent = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }

        /// <summary>
        /// Gets or sets the value whether to use to whole document styling with the <see cref="Scintilla"/> control.
        /// This is very slow on large text documents.
        /// </summary>
        public static bool StyleEntireDocument { get; set; }

        // a field to hold the NeedUrlStylingUIUpdate property value..
        private bool needUrlStylingUiUpdate;

        /// <summary>
        /// Gets or sets a value indicating whether the URL styling is required for the <see cref="Scintilla"/> control via the visible area change.
        /// </summary>
        [DoNotNotify] // this property notifies by it self..
        private bool NeedUrlStylingUiUpdate 
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return needUrlStylingUiUpdate; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(needUrlStylingUiUpdate), needUrlStylingUiUpdate, value);
                    needUrlStylingUiUpdate = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }

        // a field to hold the UrlCheckIntervalContentsChange property value..
        private int urlCheckIntervalContentsChange = 500;

        /// <summary>
        /// Gets or sets the URL check interval for the <see cref="Scintilla"/> control when the contents change.
        /// </summary>
        [DoNotNotify]
        public int UrlCheckIntervalContentsChange
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return urlCheckIntervalContentsChange; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(UrlCheckIntervalContentsChange), urlCheckIntervalContentsChange, value);
                    urlCheckIntervalContentsChange = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }

        /// <summary>
        /// Gets or sets the URL check interval for the <see cref="Scintilla"/> control.
        /// </summary>
        [DoNotNotify]
        public int UrlCheckInterval
        {
            get => UrlCheckIntervalContentsChange;
            set => UrlCheckIntervalContentsChange = value;
        }

        // a field to hold the UrlCheckIntervalUiUpdate property value..
        private int urlCheckIntervalUiUpdate = 285;

        /// <summary>
        /// Gets or sets the URL check interval for the <see cref="Scintilla"/> control.
        /// </summary>
        [DoNotNotify]
        public int UrlCheckIntervalUiUpdate
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return urlCheckIntervalUiUpdate; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(UrlCheckIntervalUiUpdate), urlCheckIntervalUiUpdate, value);
                    urlCheckIntervalUiUpdate = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }
        #endregion

        #region (C): http://www.regexguru.com/2008/11/detecting-urls-in-a-block-of-text/
        /// <summary>
        /// A regex for URL or mailto links.
        /// </summary>
        public static Regex UrlOrMailTo { get; set; } =
            
            new Regex(
                //@"((?:mailto:)?[A-Z0-9._%+-]+@[A-Z0-9._%-]+\.[A-Z]{2,4})|\b(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)(?:\([-A-Z0-9+&@#\/%=~_|$?!:,.]*\)|[-A-Z0-9+&@#\/%=~_|$?!:,.])*(?:\([-A-Z0-9+&@#\/%=~_|$?!:,.]*\)|[A-Z0-9+&@#\/%=~_|$])",
                @"(\b(?:mailto:)?[A-Z0-9._%+-]+@[A-Z0-9._%-]+\.[A-Z]{2,4})|(\b(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)(?:\([-A-Z0-9+&@#\/%=~_|$?!:,.]*\)|[-A-Z0-9+&@#\/%=~_|$?!:,.])*(?:\([-A-Z0-9+&@#\/%=~_|$?!:,.]*\)|[A-Z0-9+&@#\/%=~_|$]))",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        #endregion

        #region ScintillaUrl
        /// <summary>
        /// Gets or set the maximum URL length to use auto-ellipsis with it's display value.
        /// </summary>
        public static int AutoEllipsisUrlLength { get; set; } = -1;


        /// <summary>
        /// Gets or sets the index of the scintilla URL indicator.
        /// </summary>
        public int ScintillaUrlIndicatorIndex { get; set; } = 29;

        /// <summary>
        /// Gets or sets the color of the scintilla URL indicator.
        /// </summary>
        public Color ScintillaUrlIndicatorColor { get; set; } = Color.Blue;

        /// <summary>
        /// Gets or sets the scintilla URL indicator style.
        /// </summary>
        public IndicatorStyle ScintillaUrlIndicatorStyle { get; set; } = IndicatorStyle.Plain;

        /// <summary>
        /// Gets the scintilla URL indicator.
        /// </summary>
        public Indicator ScintillaUrlIndicator => scintilla.Indicators[ScintillaUrlIndicatorIndex];

        /// <summary>
        /// Gets or sets the value whether a process is started when an URL is clicked by the user.
        /// </summary>
        public static bool AllowProcessStartOnUrlClick { get; set; } = true;
        #endregion

        #region ScintillaUrlTextIndicator
        /// <summary>
        /// Gets or sets the index of the scintilla URL text indicator.
        /// </summary>
        public int ScintillaUrlTextIndicatorIndex { get; set; } = 30;

        /// <summary>
        /// Gets or sets the color of the scintilla URL text indicator.
        /// </summary>
        public Color ScintillaUrlTextIndicatorColor { get; set; } = Color.Blue;

        /// <summary>
        /// Gets or sets the scintilla URL text indicator style.
        /// </summary>
        public IndicatorStyle ScintillaUrlTextIndicatorStyle { get; set; } = IndicatorStyle.TextFore;

        /// <summary>
        /// Gets the scintilla URL text indicator.
        /// </summary>
        public Indicator ScintillaUrlTextIndicator => scintilla.Indicators[ScintillaUrlTextIndicatorIndex];
        #endregion

        #region ScintillaDwell                
        /// <summary>
        /// Sets the scintilla dwell tool tip to enabled or to disabled state.
        /// </summary>
        private void SetScintillaDwellToolTip()
        {
            scintilla.MouseDwellTime = UseDwellToolTip ? DwellToolTipTime : 10000000; // a value of then million means disabled..
            scintilla.CallTipCancel(); // cancel the previous tool tip..

            if (UseDwellToolTip) // only set if the URL dwell tool tip is enabled..
            {
                scintilla.Styles[Style.CallTip].SizeF = DwellToolTipFontSize;
                scintilla.Styles[Style.CallTip].ForeColor = DwellToolTipForegroundColor;
                scintilla.Styles[Style.CallTip].BackColor = DwellToolTipBackgroundColor;
            }
        }

        /// <summary>
        /// Gets or sets the dwell tool tip text for an URL. Use this for localization. The string must have one parameter for the URL ({0}) to avoid <see cref="FormatException"/> being thrown.
        /// </summary>
        public static string DwellToolTipTextUrl { get; set; } = "Use CTRL + Click to follow the link: {0}";

        /// <summary>
        /// Gets or sets the dwell tool tip text for a mailto link. Use this for localization. The string must have one parameter for the URL ({0}) to avoid <see cref="FormatException"/> being thrown.
        /// </summary>
        public static string DwellToolTipTextMailTo { get; set; } = "Use CTRL + Click to sent email to: {0}";

        /// <summary>
        /// Gets or sets the time in milliseconds before a tool tip on the URL is shown.
        /// </summary>
        public static int DwellToolTipTime { get; set; } = 400;

        /// <summary>
        /// Gets or sets the size of the dwell tool tip font.
        /// </summary>
        public float DwellToolTipFontSize { get; set; } = 8.25f;

        /// <summary>
        /// Gets or sets the color of the dwell tool tip foreground.
        /// </summary>
        public Color DwellToolTipForegroundColor { get; set; } = SystemColors.InfoText;

        /// <summary>
        /// Gets or sets the color of the dwell tool tip background.
        /// </summary>
        public Color DwellToolTipBackgroundColor { get; set; } = SystemColors.Info;

        /// <summary>
        /// Gets or sets a value indicating whether to use a dwell tool tip on the URLs.
        /// </summary>
        public bool UseDwellToolTip { get; set; } = true;
        #endregion

        #region ScintillaIndicator
        /// <summary>
        /// Clears the indicators used by the URL detection.
        /// </summary>
        public void ClearIndicators()
        {
            scintilla.IndicatorCurrent = ScintillaUrlIndicatorIndex;
            scintilla.IndicatorClearRange(0, scintilla.TextLength);
            scintilla.IndicatorCurrent = ScintillaUrlTextIndicatorIndex;
            scintilla.IndicatorClearRange(0, scintilla.TextLength);
        }

        /// <summary>
        /// Appends an indicator index to clear from the area of URL indicator.
        /// </summary>
        /// <param name="indicatorIndex">Index of the indicator.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">The indicator index must be between 0 and 31.</exception>
        public void AppendIndicatorClear(int indicatorIndex)
        {
            if (indicatorIndex < 0 || indicatorIndex > 31) // validate the range..
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentOutOfRangeException(@"The indicator index must be between 0 and 31.");
            }

            if (!StyleClearList.Contains(indicatorIndex)) // if the indicator isn't already on the clear list, add it..
            {
                StyleClearList.Add(indicatorIndex);
                NeedsUrlStylingContent = true;
            }
        }

        /// <summary>
        /// Clears the appended additional indicator indices to be clear from the area of the URL indicators.
        /// </summary>
        public void ClearAppendIndicators()
        {
            StyleClearList.Clear();
            NeedsUrlStylingContent = true;
        }

        /// <summary>
        /// A list of <see cref="Scintilla"/> indicators to clear from the range of the URL styling.
        /// </summary>
        private static readonly List<int> StyleClearList = new List<int>();

        /// <summary>
        /// Gets the <see cref="Scintilla"/> control visible text area start position and its contents as a named tuple.
        /// </summary>
        private (int startIndex, string areaText) ScintillaVisibleArea
        {
            get
            {
                var startIndex = scintilla.Lines[scintilla.FirstVisibleLine].Position;
                var lastLineIndex = scintilla.FirstVisibleLine + scintilla.LinesOnScreen;
                var endPosition = scintilla.Lines[lastLineIndex].Position +
                                  scintilla.Lines[lastLineIndex].Length;
                var areaText = scintilla.Text.Substring(startIndex, endPosition - startIndex);

                return (startIndex, areaText);
            }
        }

        /// <summary>
        /// Gets the <see cref="Scintilla"/> control text.
        /// </summary>
        private string ScintillaArea => scintilla.Text;

        /// <summary>
        /// Marks the <see cref="Scintilla"/> control visible area with a given <see cref="MatchCollection"/> from a given <paramref name="startPosition"/>,
        /// </summary>
        /// <param name="matches">A <see cref="MatchCollection"/> containing the regexp matches for the URLs to be indicated.</param>
        /// <param name="startPosition">The starting position index to within the while <see cref="Scintilla"/> control to mark the URLs.</param>
        private void MarkScintillaVisibleArea(MatchCollection matches, int startPosition)
        {
            // loop through the matches..
            foreach (Match match in matches)
            {
                // if there are indicators list to be cleared under the URL indicators..
                foreach (var indicatorIndex in StyleClearList)
                {
                    // ..clear the indicators..
                    scintilla.IndicatorCurrent = indicatorIndex;
                    scintilla.IndicatorClearRange(match.Index + startPosition, match.Length);
                }

                scintilla.IndicatorCurrent = ScintillaUrlIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Index + startPosition, match.Length);
                scintilla.IndicatorCurrent = ScintillaUrlTextIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Index + startPosition, match.Length);

                UrlMatches.Add(new UrlMatch // save the matches for process start on click..
                    {
                        StartIndex = match.Index + startPosition,
                        Contents = scintilla.Text.Substring(match.Index + startPosition, match.Length),
                        AutoEllipsisUrlLength = AutoEllipsisUrlLength,
                    }
                );
            }
        }

        /// <summary>
        /// Marks the <see cref="Scintilla"/> control text with a given <see cref="MatchCollection"/>.
        /// </summary>
        /// <param name="matches">A <see cref="MatchCollection"/> containing the regexp matches for the URLs to be indicated.</param>
        private void MarkScintillaArea(MatchCollection matches)
        {
            // loop through the matches..
            foreach (Match match in matches)
            {
                // if there are indicators list to be cleared under the URL indicators..
                foreach (var indicatorIndex in StyleClearList)
                {
                    // ..clear the indicators..
                    scintilla.IndicatorCurrent = indicatorIndex;
                    scintilla.IndicatorClearRange(match.Index, match.Length);
                }

                scintilla.IndicatorCurrent = ScintillaUrlIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Index, match.Length);
                scintilla.IndicatorCurrent = ScintillaUrlTextIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Index, match.Length);

                UrlMatches.Add(new UrlMatch // save the matches for process start on click..
                    {
                        StartIndex = match.Index,
                        Contents = scintilla.Text.Substring(match.Index, match.Length),
                        AutoEllipsisUrlLength = AutoEllipsisUrlLength,
                    }
                );
            }
        }

        /// <summary>
        /// Marks URLs of the <see cref="Scintilla"/> control within the visible area using compiled regular expression to match the words.
        /// </summary>
        public void MarkVisibleUrls()
        {
            // re-set the styling required flags..
            NeedsUrlStylingContent = false;
            NeedUrlStylingUiUpdate = false;

            // clear the list of URL matches..
            UrlMatches.Clear();

            (int startIndex, string areaText) visibleArea = (0, string.Empty);

            if (scintilla.InvokeRequired)
            {
                scintilla.Invoke(new MethodInvoker(() => { visibleArea = ScintillaVisibleArea; }));
            }
            else
            {
                visibleArea = ScintillaVisibleArea;
            }

            var startPosition = visibleArea.startIndex;

            var area = visibleArea.areaText;

            // get the regexp matches..
            var matches = UrlOrMailTo.Matches(area);

            // mark the visible area..
            if (scintilla.InvokeRequired)
            {
                // clear the previous URL indicators..
                scintilla.Invoke(new MethodInvoker(ClearIndicators));
                scintilla.Invoke(new MethodInvoker(() => { MarkScintillaVisibleArea(matches, startPosition); }));
            }
            else
            {
                // clear the previous URL indicators..
                ClearIndicators();
                MarkScintillaVisibleArea(matches, startPosition);
            }
        }

        /// <summary>
        /// Marks URLs of the <see cref="Scintilla"/> control using compiled regular expression to match the words.
        /// </summary>
        public void MarkUrls()
        {
            // re-set the styling required flags..
            NeedsUrlStylingContent = false;
            NeedUrlStylingUiUpdate = false;

            // clear the URL indicators..
            if (scintilla.InvokeRequired)
            {
                scintilla.Invoke(new MethodInvoker(ClearIndicators));
            }
            else
            {
                ClearIndicators();
            }

            // clear the list of URL matches..
            UrlMatches.Clear();

            string text = string.Empty;

            if (scintilla.InvokeRequired)
            {
                scintilla.Invoke(new MethodInvoker(() => { text = ScintillaArea; }));
            }
            else
            {
                text = ScintillaArea;
            }

            // get the regexp matches..
            var matches = UrlOrMailTo.Matches(text);

            // mark the whole Scintilla text area..
            if (scintilla.InvokeRequired)
            {
                scintilla.Invoke(new MethodInvoker(() => { MarkScintillaArea(matches); }));
            }
            else
            {
                MarkScintillaArea(matches);
            }
        }

        /// <summary>
        /// Gets or sets of the URL matches within a <see cref="Scintilla"/> control.
        /// </summary>
        private List<UrlMatch> UrlMatches { get; } = new List<UrlMatch>();
        #endregion

        #region PropertyChanged
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when a property value changes.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="before">The before value of the property.</param>
        /// <param name="after">The after value of the property.</param>
        // ReSharper disable once UnusedMember.Global
#pragma warning disable IDE0060 // this is used via the PropertyChanged.Fody..
        public void OnPropertyChanged(string propertyName, object before, object after) // this is used via the PropertyChanged.Fody..
#pragma warning restore IDE0060
        {
            if (nameof(ScintillaUrlIndicatorIndex) == propertyName ||
                nameof(ScintillaUrlTextIndicatorIndex) == propertyName)
            {
                scintilla.IndicatorCurrent = (int) before;
                // clear the previous spell check markings..
                scintilla.IndicatorClearRange(0, scintilla.TextLength);

                MarkUrls();
            }

            bool reStyle = false; // a flag indicating whether URL re-styling is required..

            if (nameof(ScintillaUrlIndicatorStyle) == propertyName) 
            {
                // the URL indicator style changed..
                ScintillaUrlIndicator.Style = ScintillaUrlIndicatorStyle;
                reStyle = true; // set the flag to re-style the Scintilla document..
            }

            if (nameof(ScintillaUrlIndicatorColor) == propertyName)
            {
                // the URL indicator foreground color changed..
                ScintillaUrlIndicator.ForeColor = ScintillaUrlIndicatorColor;
                reStyle = true; // set the flag to re-style the Scintilla document..
            }

            if (nameof(ScintillaUrlTextIndicatorStyle) == propertyName)
            {
                // the URL indicator text style changed..
                ScintillaUrlTextIndicator.Style = ScintillaUrlTextIndicatorStyle;
                reStyle = true; // set the flag to re-style the Scintilla document..
            }

            if (nameof(ScintillaUrlTextIndicatorColor) == propertyName)
            {
                // the URL indicator text foreground color changed..
                ScintillaUrlTextIndicator.ForeColor = ScintillaUrlTextIndicatorColor;
                reStyle = true; // set the flag to re-style the Scintilla document..
            }

            // re-set the URL styling timer for the thread..
            if (nameof(NeedsUrlStylingContent) == propertyName)
            {
                threadSpentTimeMs = 0;
            }

            // all these property changes requires the dwell tool tip to be re-configured..
            if (nameof(UseDwellToolTip) == propertyName || 
                nameof(DwellToolTipBackgroundColor) == propertyName ||
                nameof(DwellToolTipFontSize) == propertyName ||
                nameof(DwellToolTipForegroundColor) == propertyName)
            {
                SetScintillaDwellToolTip(); // ..re-configure the dwell tool tip..
            }

            if (reStyle) // if a property value was changed that would affect the URL indicators..
            {
                MarkUrls(); // ..re-style the Scintilla document..
            }

            // raise the property changed event if subscribed..
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region PublicMethods
        /// <summary>
        /// Unifies the given Regex <see cref="MatchCollection"/> into a single distinct list.
        /// </summary>
        /// <param name="matchCollections">A list of <see cref="MatchCollection"/> class instances.</param>
        /// <returns>A list of <see cref="Match"/> class instances with in indices the match belongs to in the <paramref name="matchCollections"/>.</returns>
        public static List<KeyValuePair<Match, int>> UnifyMatches(params MatchCollection[] matchCollections)
        {
            List<KeyValuePair<Match, int>> result = new List<KeyValuePair<Match, int>>();

            for (int i = 0; i < matchCollections.Length; i++)
            {
                foreach (Match match in matchCollections[i])
                {
                    if (!result.Exists(f => f.Key.Index == match.Index && f.Key.Length == match.Length) &&
                        !result.Exists(f => f.Key.Index <= match.Index && f.Key.Length + f.Key.Index >= match.Length + match.Index))
                    {
                        result.Add(new KeyValuePair<Match, int>(match, i));
                    }
                }
            }

            // sort the values by their starting positions..
            result.Sort((x, y) => x.Key.Index.CompareTo(y.Key.Index)); 

            return result;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // un-subscribe the events handlers..
            scintilla.TextChanged -= Scintilla_TextChanged;
            scintilla.MouseMove -= Scintilla_MouseMove;
            scintilla.IndicatorClick -= Scintilla_IndicatorClick;
            scintilla.DwellStart -= Scintilla_DwellStart;
            scintilla.DwellEnd -= Scintilla_DwellEnd;
            scintilla.UpdateUI -= Scintilla_UpdateUI;
            scintilla.SizeChanged -= Scintilla_SizeChanged;

            // stop the thread..
            CancelThread();

            // destruction is happening..
            MyInstances.Remove(this);

            ClearIndicators();

            // a nice call to the garbage collector (==GC)..
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
