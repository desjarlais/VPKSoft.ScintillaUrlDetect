#region License
/*
MIT License

Copyright(c) 2020 Petteri Kautonen

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

using System.Diagnostics;
using System.Text.RegularExpressions;
using ScintillaNET;

namespace VPKSoft.ScintillaUrlDetect
{
    /// <summary>
    /// A class to indicate an Url or a mailto match within a <see cref="Scintilla"/> control.
    /// </summary>
    public class UrlMatch
    {
        /// <summary>
        /// Gets or sets the start index of the Url match.
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Gets the length of the Url match.
        /// </summary>
        public int Length => Contents.Length;

        /// <summary>
        /// Gets the end index of the Url match.
        /// </summary>
        public int EndIndex => StartIndex + Length;

        /// <summary>
        /// Gets or sets the contents of the Url match.
        /// </summary>
        public string Contents { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is a mail to link.
        /// </summary>
        public bool IsMailToLink => RegexMailTo.IsMatch(Contents);

        /// <summary>
        /// Gets the contents as a human readable string.
        /// </summary>
        public string ContentsHumanReadable
        {
            get
            {
                var result = Contents.Trim().Trim('\"', '\'').Replace("mailto:", string.Empty);
                try
                {
                    if (AutoEllipsisUrlLength != -1 && result.Length >= AutoEllipsisUrlLength + 3)
                    {
                        var partLength = (AutoEllipsisUrlLength - 3) / 2;
                        var part1 = result.Substring(0, partLength);
                        var part2 = result.Substring(result.Length - partLength);
                        result = string.Concat(part1,
                            @"...",
                            part2);
                    }
                }
                catch
                {
                    // the auto-ellipsis failed..
                }

                return result;
            }
        } 

        /// <summary>
        /// Gets or sets the URL maximum length to use auto-ellipsis on it.
        /// </summary>
        public int AutoEllipsisUrlLength { get; set; } = -1;

        /// <summary>
        /// A regex for mailto links.
        /// </summary>
        public static Regex RegexMailTo { get; set; } =
            new Regex(
                @"((?:mailto:)?[A-Z0-9._%+-]+@[A-Z0-9._%-]+\.[A-Z]{2,4})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Gets the contents tidied so that the Url can be used to start a <see cref="Process"/>.
        /// </summary>
        public string ContentsTidy
        {
            get
            {
                var tidyContents = Contents.Trim().Trim('\"', '\'');

                if (IsMailToLink)
                {
                    if (!tidyContents.StartsWith("mailto:"))
                    {
                        tidyContents = @"mailto:" + tidyContents;
                    }
                }

                return tidyContents;
            }
        } 
    }
}
