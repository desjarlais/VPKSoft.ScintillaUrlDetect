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
            NeedsUrlStyling = true;
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
                    for (int i = 0; i < MyInstances.Count; i++)
                    {
                        MyInstances[i].CreateThread();
                    }
                }

                if (!value && useThreadsOnUrlStyling) // disable the treads..
                {
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
                // if URL styling is required and the launch time has been passed..
                if (threadSpentTimeMs > UrlCheckInterval && NeedsUrlStyling) 
                {
                    if (scintilla.InvokeRequired) // ..re-style the Scintilla control's URLs..
                    {
                        scintilla.Invoke(new MethodInvoker(MarkUrls));
                    }
                    else
                    {
                        MarkUrls(); // this shouldn't happen..
                    }

                    threadSpentTimeMs = 0; // zero the time counter..
                }

                Thread.Sleep(10); // some sleeping (zzz)..
                threadSpentTimeMs += 10; // increase the re-style launch counter..
                if (threadSpentTimeMs > 1000000) // avoid arithmetic overflow..
                {
                    // ..just set the re-style launch counter to the defined interval..
                    threadSpentTimeMs = UrlCheckInterval; 
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
            int charPosition = scintilla.CharPositionFromPoint(point.X, point.Y);

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

        // the text of the scintilla control changed, so set the NeedsUrlStyling flag to true..
        private void Scintilla_TextChanged(object sender, EventArgs e)
        {
            NeedsUrlStyling = true;
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
            if ((e.Modifiers & Keys.Control) == Keys.Control) // validate it was a CTRL+Click..
            {
                var match = GetUrlAtPosition(e.Position); // get the URL at the mouse position..
                if (match != null) // start a process if a link exists at the mouse position..
                {
                    try
                    {
                        Process.Start(match.ContentsTidy);
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
        // a field to hold the NeedsUrlStyling property value..
        private bool needUrlStyling;

        /// <summary>
        /// Gets or sets a value indicating whether the URL styling is required for the <see cref="Scintilla"/> control.
        /// </summary>
        [DoNotNotify] // this property notifies by it self..
        private bool NeedsUrlStyling 
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return needUrlStyling; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(NeedsUrlStyling), needUrlStyling, value);
                    needUrlStyling = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }

        // a field to hold the UrlCheckInterval property value..
        private int urlCheckInterval = 500;

        /// <summary>
        /// Gets or sets the URL check interval for the <see cref="Scintilla"/> control.
        /// </summary>
        [DoNotNotify]
        public int UrlCheckInterval
        {
            get
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    return urlCheckInterval; // return the value..
                }
            }

            set
            {
                lock (waitUrlStylingThreadLock) // lock the property value..
                {
                    // indicate that the property value was changed..
                    OnPropertyChanged(nameof(UrlCheckInterval), urlCheckInterval, value);
                    urlCheckInterval = value; // set the property value..
                }
                threadSpentTimeMs = 0; // re-set the URL styling thread's time counter..
            }
        }
        #endregion

        #region (C): http://www.regexguru.com/2008/11/detecting-urls-in-a-block-of-text/
        // Also very much thanks to: https://regex101.com, donate something to the site in case you have a dollar..

        /// <summary>
        /// The first URL match Regex.
        /// </summary>
        public Regex UrlMatchFirst { get; set; } =
            new Regex(@"(?:(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)[-A-Z0-9+&@#\/%?=~_|$!:,.;]*[-A-Z0-9+&@#\/%=~_|$])",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The second URL match Regex.
        /// </summary>
        public Regex UrlMatchSecond { get; set; } = new Regex(@"""(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)[^""\r\n]+""?",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The third URL match Regex.
        /// </summary>
        public Regex UrlMatchThird { get; set; } = new Regex(@"'(?:(?:https?|ftp|file):\/\/|www\.|ftp\.)[^'\r\n]+'?",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The first mailto match Regex.
        /// </summary>
        public Regex MailtoMatchFirst { get; set; } =
            new Regex(@"'((?:mailto:)?[A-Z0-9._%+-]+@[A-Z0-9._%-]+\.[A-Z]{2,4})'",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The second mailto match Regex.
        /// </summary>
        public Regex MailtoMatchSecond { get; set; } =
            new Regex(@"((?:mailto:)?[A-Z0-9._%+-]+@[A-Z0-9._%-]+\.[A-Z]{2,4})\b",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        #endregion

        #region ScintillaUrl
        /// <summary>
        /// Gets or sets the index of the scintilla URL indicator.
        /// </summary>
        public int ScintillaUrlIndicatorIndex { get; set; } = 29;

        /// <summary>
        /// Gets or sets the scintilla URL indicator style.
        /// </summary>
        public IndicatorStyle ScintillaUrlIndicatorStyle { get; set; } = IndicatorStyle.Plain;

        /// <summary>
        /// Gets or sets the color of the scintilla URL indicator.
        /// </summary>
        public Color ScintillaUrlIndicatorColor { get; set; } = Color.Blue;

        /// <summary>
        /// Gets the scintilla URL indicator.
        /// </summary>
        public Indicator ScintillaUrlIndicator => scintilla.Indicators[ScintillaUrlIndicatorIndex];
        #endregion

        #region ScintillaUrlTextIndicator
        /// <summary>
        /// Gets or sets the index of the scintilla URL text indicator.
        /// </summary>
        public int ScintillaUrlTextIndicatorIndex { get; set; } = 30;

        /// <summary>
        /// Gets or sets the scintilla URL text indicator style.
        /// </summary>
        public IndicatorStyle ScintillaUrlTextIndicatorStyle { get; set; } = IndicatorStyle.TextFore;

        /// <summary>
        /// Gets or sets the color of the scintilla URL text indicator.
        /// </summary>
        public Color ScintillaUrlTextIndicatorColor { get; set; } = Color.Blue;

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
                NeedsUrlStyling = true;
            }
        }

        /// <summary>
        /// Clears the appended additional indicator indices to be clear from the area of the URL indicators.
        /// </summary>
        public void ClearAppendIndicators()
        {
            StyleClearList.Clear();
            NeedsUrlStyling = true;
        }

        /// <summary>
        /// A list of <see cref="Scintilla"/> indicators to clear from the range of the URL styling.
        /// </summary>
        private static readonly List<int> StyleClearList = new List<int>();

        /// <summary>
        /// Marks URLs of the <see cref="Scintilla"/> control using compiled regular expression to match the words.
        /// </summary>
        public void MarkUrls()
        {
            NeedsUrlStyling = false;
            // clear the URL indicators..
            ClearIndicators();

            // clear the list of URL matches..
            UrlMatches.Clear();

            var urlMatches1 = UrlMatchFirst.Matches(scintilla.Text); // URL match regex NO.1, not numbered by how good these are..
            var urlMatches2 = UrlMatchSecond.Matches(scintilla.Text); // URL match regex NO.2, not numbered by how good these are..
            var urlMatches3 = UrlMatchThird.Matches(scintilla.Text); // URL match regex NO.3, not numbered by how good these are..

            var mailtoMatches1 = MailtoMatchFirst.Matches(scintilla.Text); // mailto: match regex NO.1, not numbered by how good these are..
            var mailtoMatches2 = MailtoMatchSecond.Matches(scintilla.Text); // mailto: match regex NO.2, not numbered by how good these are..

            // find matches to all the regex definitions..
            var matches = UnifyMatches(urlMatches1, urlMatches2, urlMatches3, mailtoMatches1, mailtoMatches2);

            // loop through the matches..
            foreach (var match in matches)
            {
                // if there are indicators list to be cleared under the URL indicators..
                foreach (var indicatorIndex in StyleClearList)
                {
                    // ..clear the indicators..
                    scintilla.IndicatorCurrent = indicatorIndex;
                    scintilla.IndicatorClearRange(match.Key.Index, match.Key.Length);
                }

                scintilla.IndicatorCurrent = ScintillaUrlIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Key.Index, match.Key.Length);
                scintilla.IndicatorCurrent = ScintillaUrlTextIndicatorIndex;
                // ..mark it with an indicator..
                scintilla.IndicatorFillRange(match.Key.Index, match.Key.Length);

                UrlMatches.Add(new UrlMatch // save the matches for process start on click..
                    {
                        StartIndex = match.Key.Index,
                        Contents = scintilla.Text.Substring(match.Key.Index, match.Key.Length),
                        IsMailToLink = match.Value > 2, // the indices 3 and 4 are mailto: links..
                    }
                );
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
            if (nameof(NeedsUrlStyling) == propertyName)
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
                    if (!result.Exists(f => f.Key.Index == match.Index && f.Key.Length == match.Length))
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
