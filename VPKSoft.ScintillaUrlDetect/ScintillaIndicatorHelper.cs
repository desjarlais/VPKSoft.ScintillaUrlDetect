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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ScintillaNET;

namespace VPKSoft.ScintillaUrlDetect
{
    /// <summary>
    /// Some helper methods for the <see cref="Scintilla"/> class.
    /// </summary>
    public static class ScintillaIndicatorHelper
    {
        /// <summary>
        /// Gets a value indicating whether the <see cref="Scintilla"/> has a given indicator at a given position.
        /// </summary>
        /// <param name="scintilla">The scintilla.</param>
        /// <param name="indicator">The indicator.</param>
        /// <param name="pos">The position.</param>
        // (C): https://github.com/jacobslusser/ScintillaNET/issues/146
        public static bool IndicatorOnFor(this Scintilla scintilla, int indicator, int pos)
        {
            var bitmap = scintilla.IndicatorAllOnFor(pos);
            var flag = (1 << indicator);

            if (flag < 0) // special case with the 31 indicator as we are dealing with 32 bit integers..
            {
                long flagLong = flag;
                flagLong *= -1;
                long compareLong = bitmap & flagLong;
                return flagLong == compareLong;
            }

            return ((bitmap & flag) == flag);
        }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once IdentifierTypo
        private const int SCI_SETCURSOR = 2386;

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once IdentifierTypo
        private const int SCI_GETCURSOR = 2387;

        /// <summary>
        /// Gets the cursor of this <see cref="Scintilla"/> control.
        /// </summary>
        /// <param name="scintilla">The scintilla.</param>
        /// <returns>The current cursor with this <see cref="Scintilla"/> control.</returns>
        public static Cursor GetCursor(this Scintilla scintilla)
        {
            try
            {
                var result = scintilla.DirectMessage(SCI_GETCURSOR, (IntPtr) 0, (IntPtr) 0);
                var value = result.ToInt32();

                if (value == -1)
                {
                    value = 0;
                }

                var cursor = ScintillaCursorMapping.FirstOrDefault(f => f.Key == value);

                // Debug.Print(cursor.ToString());

                return cursor.Value;
            }
            catch
            {
                return Cursors.Default;
            }
        }

        /// <summary>
        /// Sets the cursor of this <see cref="Scintilla"/> control.
        /// </summary>
        /// <param name="scintilla">The scintilla.</param>
        /// <param name="cursor">The cursor to set for this <see cref="Scintilla"/> control.</param>
        /// <returns><c>true</c> if the operation was successful, <c>false</c> otherwise.</returns>
        public static bool SetCursor(this Scintilla scintilla, Cursor cursor)
        {
            try
            {
                var currentCursor = scintilla.GetCursor();

                if (cursor == currentCursor)
                {
                    //Debug.Print("No change in cursors..");
                    return false;
                }

                // Debug.Print(cursor + " / " + currentCursor);

                var cursorValue = ScintillaCursorMapping.First(f => f.Value == cursor).Key;

                scintilla.DirectMessage(SCI_SETCURSOR, (IntPtr) cursorValue, (IntPtr) 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Mapping of the <see cref="Cursor"/> values to their corresponding <see cref="Scintilla"/> control cursors.
        /// </summary>
        public static List<KeyValuePair<int, Cursor>> ScintillaCursorMapping = 
            new List<KeyValuePair<int, Cursor>>(new[]
        {
            new KeyValuePair<int, Cursor>(0, Cursors.Default),
            new KeyValuePair<int, Cursor>(1, Cursors.IBeam),
            new KeyValuePair<int, Cursor>(2, Cursors.Arrow),
            new KeyValuePair<int, Cursor>(3, Cursors.UpArrow),
            new KeyValuePair<int, Cursor>(4, Cursors.WaitCursor),
            new KeyValuePair<int, Cursor>(5, Cursors.SizeWE),
            new KeyValuePair<int, Cursor>(6, Cursors.SizeNS),
            // new KeyValuePair<int, Cursor>(7, MirroredArrow),
            new KeyValuePair<int, Cursor>(8, Cursors.Hand),
        });
    }
}
