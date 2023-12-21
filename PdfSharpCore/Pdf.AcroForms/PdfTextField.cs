#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharpCore.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Internal;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.Annotations;
using PdfSharpCore.Pdf.Internal;
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Emit;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents the text field.
    /// </summary>
    public sealed class PdfTextField : PdfAcroField
    {
        /// <summary>
        /// Initializes a new instance of PdfTextField.
        /// </summary>
        internal PdfTextField(PdfDocument document)
            : base(document)
        { }

        internal PdfTextField(PdfDictionary dict)
            : base(dict)
        { }

        /// <summary>
        /// Gets or sets the text value of the text field.
        /// </summary>
        public string Text
        {
            get { return Elements.GetString(Keys.V); }
            set { Elements.SetString(Keys.V, value); RenderAppearance(); } //HACK in PdfTextField
        }

        /// <summary>
        /// Gets or sets the font used to draw the text of the field.
        /// </summary>
        public XFont Font
        {
            get { return _font; }
            set { _font = value; }
        }
        XFont _font = new XFont(GlobalFontSettings.FontResolver.DefaultFontName, 10);

        /// <summary>
        /// Gets or sets the foreground color of the field.
        /// </summary>
        public XColor ForeColor
        {
            get { return _foreColor; }
            set { _foreColor = value; }
        }
        XColor _foreColor = XColors.Black;

        /// <summary>
        /// Gets or sets the background color of the field.
        /// </summary>
        public XColor BackColor
        {
            get { return _backColor; }
            set { _backColor = value; }
        }
        XColor _backColor = XColor.Empty;

        /// <summary>
        /// Gets or sets the maximum length of the field.
        /// </summary>
        /// <value>The length of the max.</value>
        public int MaxLength
        {
            get { return Elements.GetInteger(Keys.MaxLen); }
            set { Elements.SetInteger(Keys.MaxLen, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the field has multiple lines.
        /// </summary>
        public bool MultiLine
        {
            get { return (Flags & PdfAcroFieldFlags.Multiline) != 0; }
            set
            {
                if (value)
                    SetFlags |= PdfAcroFieldFlags.Multiline;
                else
                    SetFlags &= ~PdfAcroFieldFlags.Multiline;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this field is used for passwords.
        /// </summary>
        public bool Password
        {
            get { return (Flags & PdfAcroFieldFlags.Password) != 0; }
            set
            {
                if (value)
                    SetFlags |= PdfAcroFieldFlags.Password;
                else
                    SetFlags &= ~PdfAcroFieldFlags.Password;
            }
        }

        /// <summary>
        /// Creates the normal appearance form X object for the annotation that represents
        /// this acro form text field.
        /// </summary>
        public void RenderAppearance()
        {
            PdfRectangle rect = Elements.GetRectangle(PdfAnnotation.Keys.Rect);
            var box = rect.ToXRect() - rect.Location;
            XForm form = new XForm(Owner, rect.Size);
            var gfx = XGraphics.FromForm(form);

            XColor bgcolor = XColor.Empty;
            int bordersize = 0;
            XColor bordercolor = XColor.Empty;

            var style = Elements.GetDictionary("/MK");
            if (style != null)
            {
                var bgdic = style.Elements.GetArray("/BG");
                if (bgdic != null)
                {
                    double[] elements = bgdic.Elements.Items.Select(x => (x as PdfReal)?.Value).Where(x => x != null).Cast<double>().ToArray();
                    bgcolor = XColor.FromValues(elements);
                }

                var borderdic = style.Elements.GetArray("/BC");
                if (borderdic != null)
                {
                    double[] elements = borderdic.Elements.Items.Select(x => (x as PdfReal)?.Value).Where(x => x != null).Cast<double>().ToArray();
                    bordercolor = XColor.FromValues(elements);
                }
            }

            var bs = Elements.GetDictionary("/BS")?.Elements?.GetInteger("/W");
            if (bs != null)
            {
                bordersize = bs ?? 0;
            }

            gfx.DrawRectangle(new XSolidBrush(bgcolor), box);

            if (bordersize > 0)
            {

                for (int i=0;i<=bordersize; i++)
                {
                    var x = i / 2;
                    //laterales
                    var (a, b) = (new XPoint(box.Width - i, 0), new XPoint(box.Width - i, box.Height));
                    var (c, d) = (new XPoint(i, 0), new XPoint(i, box.Height));

                    //inferior
                    var (e, f) = (new XPoint(0, box.Height - i), new XPoint(box.Width, box.Height - i));

                    //superior
                    var (g, h) = (new XPoint(0, i), new XPoint(box.Width, i));

                    gfx.DrawLine(new XPen(bordercolor), a, b);
                    gfx.DrawLine(new XPen(bordercolor), c, d);
                    gfx.DrawLine(new XPen(bordercolor), e, f);
                    gfx.DrawLine(new XPen(bordercolor), g, h);
                }

            }
            
            form.DrawingFinished();
            form.PdfForm.Elements.Add("/FormType", new PdfLiteral("1"));

            PdfDictionary ap = Elements[PdfAnnotation.Keys.AP] as PdfDictionary;
            if (ap == null)
            {
                ap = new PdfDictionary(_document);
                Elements[PdfAnnotation.Keys.AP] = ap;
            }

            PdfFormXObject xobj = form.PdfForm;
            string s = xobj.Stream.ToString();

            var (p1, p2, p3, p4) = (bordersize, bordersize, box.Width - (bordersize * 2), box.Height - (bordersize * 2));
            var txsizestring = $"q\nn\nq\n{p1} {p2} {p3} {p4} re\nW\nn";

            // Thank you Adobe: Without putting the content in 'EMC brackets'
            // the text is not rendered by PDF Reader 9 or higher.
            s = $"{s}\n/Tx BMC\n{txsizestring}\nEMC";
            xobj.Stream.Value = new RawEncoding().GetBytes(s);

            ap.Elements["/N"] = form.PdfForm.Reference;
        }

        internal override void PrepareForSave()
        {
            base.PrepareForSave();
            RenderAppearance();
        }

        /// <summary>
        /// Predefined keys of this dictionary. 
        /// The description comes from PDF 1.4 Reference.
        /// </summary>
        public new class Keys : PdfAcroField.Keys
        {
            /// <summary>
            /// (Optional; inheritable) The maximum length of the field’s text, in characters.
            /// </summary>
            [KeyInfo(KeyType.Integer | KeyType.Optional)]
            public const string MaxLen = "/MaxLen";

            /// <summary>
            /// Gets the KeysMeta for these keys.
            /// </summary>
            internal static DictionaryMeta Meta
            {
                get { return _meta ?? (_meta = CreateMeta(typeof(Keys))); }
            }
            static DictionaryMeta _meta;
        }

        /// <summary>
        /// Gets the KeysMeta of this dictionary type.
        /// </summary>
        internal override DictionaryMeta Meta
        {
            get { return Keys.Meta; }
        }
    }
}
