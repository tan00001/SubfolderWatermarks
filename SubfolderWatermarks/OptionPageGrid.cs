// <copyright file="OptionPageGrid.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

using Microsoft.VisualStudio.Shell;

namespace SubfolderWatermarks
{
    public class OptionPageGrid : DialogPage
    {
        public const string CurrentFileName = "${currentfilename}";
        public const string CurrentDirectoryName = "${currentdirectoryname}";
        public const string CurrentProjectName = "${currentprojectname}";
        public const string CurrentFilePathInProject = "${currentfilepathinproject}";

#pragma warning disable SA1309 // Field names should not begin with underscore
        private readonly HashSet<string> _relativeFolders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { @"obj", @"bin" };
        private readonly HashSet<string> _absoluteFolders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private int _hashValue;
        private double _textSize = 16.0D;
        private string _fontFamilyName = "Consolas";
        private bool _isFontBold = false;
        private bool _isFontItalic = false;
        private bool _isFontUnderline = false;
        private bool _isFontStrikeThrough = false;
        private bool _usingImage = false;
        private string _displayedText = CurrentFilePathInProject;
        private string _displayedTextFormat = string.Empty;
        private bool _useCurrentFileName = false;
        private bool _useCurrentDirectoryName = false;
        private bool _useCurrentProjectName = false;
        private bool _useCurrentFilePathInProject = true;
        private bool _usingReplacements = false;
        private string _imagePath = string.Empty;
        private string _textColor = "Red";
        private string _borderColor = "Gray";
        private string _backgroundColor = "White";
        private double _borderMargin = 10D;
        private double _borderPadding = 3D;
        private double _borderOpacity = 0.7D;
#pragma warning restore SA1309 // Field names should not begin with underscore

        [Category(Vsix.OptionSectionWatermarkHeader)]
        [DisplayName("Enabled")]
        [Description("Show the watermark.")]
        public bool IsEnabled { get; set; } = false;

        [Category(Vsix.OptionSectionWatermarkHeader)]
        [DisplayName("Folders")]
        [Description("Show the watermark in these folders.")]
        public string Folders
        {
            get
            {
                return string.Join(";", _relativeFolders.Select(f => @".\" + f).Union(_absoluteFolders));
            }

            set
            {
                _relativeFolders.Clear();
                _absoluteFolders.Clear();
                var folders = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                _relativeFolders.UnionWith(folders.Where(f => f.StartsWith(@".\", StringComparison.InvariantCultureIgnoreCase)
                    || f.StartsWith(@"./", StringComparison.InvariantCultureIgnoreCase))
                    .Select(f => f.Substring(2)));
                _absoluteFolders.UnionWith(folders.Where(f => !f.StartsWith(@".\", StringComparison.InvariantCultureIgnoreCase)
                    && !f.StartsWith(@"./", StringComparison.InvariantCultureIgnoreCase)));
            }
        }

        [Category(Vsix.OptionSectionWatermarkHeader)]
        [DisplayName("Top")]
        [Description("Show the watermark at the top.")]
        public bool PositionTop
        {
            get; set;
        }

        [Category(Vsix.OptionSectionWatermarkHeader)]
        [DisplayName("Left")]
        [Description("Show the watermark on the left.")]
        public bool PositionLeft
        {
            get; set;
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Displayed text")]
        [Description("The text to show in the watermark.")]
        public string DisplayedText
        {
            get
            {
                return _displayedText;
            }

            set
            {
                _displayedText = value ?? string.Empty;
                _usingImage = value.StartsWith("IMG:") == true;
                if (_usingImage)
                {
                    _imagePath = _displayedText.Substring(4);
                }
                else
                {
                    _imagePath = string.Empty;
                }

                var lowerCaseDisplayedText = _displayedText.ToLowerInvariant();
                (_useCurrentFileName, _displayedTextFormat) = Replace(_displayedText, lowerCaseDisplayedText, 0, CurrentFileName);
                (_useCurrentDirectoryName, _displayedTextFormat) = Replace(_displayedTextFormat, lowerCaseDisplayedText, 0, CurrentDirectoryName);
                (_useCurrentProjectName, _displayedTextFormat) = Replace(_displayedTextFormat, lowerCaseDisplayedText, 0, CurrentProjectName);
                (_useCurrentFilePathInProject, _displayedTextFormat) = Replace(_displayedTextFormat, lowerCaseDisplayedText, 0, CurrentFilePathInProject);
                _usingReplacements = _useCurrentFileName
                    || _useCurrentDirectoryName
                    || _useCurrentProjectName
                    || _useCurrentFilePathInProject;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Text size")]
        [Description("The size of the text in the watermark.")]
        public double TextSize
        {
            get
            {
                return _textSize;
            }

            set
            {
                _textSize = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Font family")]
        [Description("The name of the font to use.")]
        public string FontFamilyName
        {
            get
            {
                return _fontFamilyName;
            }

            set
            {
                _fontFamilyName = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Bold")]
        [Description("Should the text be displayed in bold.")]
        public bool IsFontBold
        {
            get
            {
                return _isFontBold;
            }

            set
            {
                _isFontBold = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Italic")]
        [Description("Should the text be displayed in italic.")]
        public bool IsFontItalic
        {
            get
            {
                return _isFontItalic;
            }

            set
            {
                _isFontItalic = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Underline")]
        [Description("Should the text be displayed with underline.")]
        public bool IsFontUnderline
        {
            get
            {
                return _isFontUnderline;
            }

            set
            {
                _isFontUnderline = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Strike Through")]
        [Description("Should the text be displayed with strike through.")]
        public bool IsFontStrikeThrough
        {
            get
            {
                return _isFontStrikeThrough;
            }

            set
            {
                _isFontStrikeThrough = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionTextHeader)]
        [DisplayName("Color")]
        [Description("The color to use for the text. Can be a named value or Hex (e.g. '#FF00FF')")]
        public string TextColor
        {
            get
            {
                return _textColor;
            }

            set
            {
                _textColor = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionBackgroundHeader)]
        [DisplayName("Border")]
        [Description("The color to use for the border. Can be a named value or Hex (e.g. '#FF00FF')")]
        public string BorderColor
        {
            get
            {
                return _borderColor;
            }

            set
            {
                _borderColor = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionBackgroundHeader)]
        [DisplayName("Background")]
        [Description("The color to use for the background. Can be a named value or Hex (e.g. '#FF00FF')")]
        public string BackgroundColor
        {
            get
            {
                return _backgroundColor;
            }

            set
            {
                _backgroundColor = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionBackgroundHeader)]
        [DisplayName("Margin")]
        [Description("Number of pixels between the border and the edge of the editor.")]
        public double BorderMargin
        {
            get
            {
                return _borderMargin;
            }

            set
            {
                _borderMargin = value;
            }
        }

        [Category(Vsix.OptionSectionBackgroundHeader)]
        [DisplayName("Padding")]
        [Description("Number of pixels between the text and the border.")]
        public double BorderPadding
        {
            get
            {
                return _borderPadding;
            }

            set
            {
                _borderPadding = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionBackgroundHeader)]
        [DisplayName("Opacity")]
        [Description("Strength of the background opacity.")]
        public double BorderOpacity
        {
            get
            {
                return _borderOpacity;
            }

            set
            {
                _borderOpacity = value;
                _hashValue = 0;
            }
        }

        [Category(Vsix.OptionSectionMiscHeader)]
        [DisplayName("Show Debug Output")]
        [Description("Display an output pane when debug messages are present.")]
        public bool ShowDebugOutput
        {
            get; set;
        }

        public IReadOnlyCollection<string> GetAbsoluteContainingFolders()
        {
            return _absoluteFolders;
        }

        public IReadOnlyCollection<string> GetRelativeContainingFolders()
        {
            return _relativeFolders;
        }

        public string GetImagePath() => _imagePath;

        public string GetDisplayTextFormat() => _displayedTextFormat;

        public int GetSettingsHash()
        {
            if (_hashValue != 0)
            {
                return _hashValue;
            }

            _hashValue = CombineHashCodes(
                _textSize.GetHashCode(),
                _fontFamilyName.GetHashCode(),
                _isFontBold.GetHashCode(),
                _isFontItalic.GetHashCode(),
                _isFontUnderline.GetHashCode(),
                _isFontStrikeThrough.GetHashCode(),
                _textColor?.GetHashCode() ?? 0,
                _borderColor?.GetHashCode() ?? 0,
                _backgroundColor?.GetHashCode() ?? 0,
                _borderMargin.GetHashCode(),
                _borderPadding.GetHashCode(),
                _borderOpacity.GetHashCode());

            return _hashValue;
        }

        public bool IsUsingImage() => _usingImage;

        public bool UsingReplacements() => _usingReplacements;

        public bool UseCurrentFileName() => _useCurrentFileName;

        public bool UseCurrentDirectoryName() => _useCurrentDirectoryName;

        public bool UseCurrentProjectName() => _useCurrentProjectName;

        public bool UseCurrentFilePathInProject() => _useCurrentFilePathInProject;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            System.Diagnostics.Debug.WriteLine("OnClosed");

            Messenger.RequestUpdateAdornment();
        }

        private static int CombineHashCodes(params int[] hashCodes)
        {
            int combinedHash = 17;

            foreach (int hashCode in hashCodes)
            {
                combinedHash = (combinedHash * 31) + hashCode;
            }

            return combinedHash;
        }

        private static (bool, string) Replace(string text, string lowerCaseText, int startIndex, string lowerCaseReplacement)
        {
            var index = lowerCaseText.IndexOf(lowerCaseReplacement, startIndex);
            if (index < 0)
            {
                return (false, text.Substring(startIndex));
            }

            var tailIndex = index + lowerCaseReplacement.Length;

            (_, string textTail) = Replace(
                text,
                lowerCaseText,
                tailIndex,
                lowerCaseReplacement);

            text = text.Substring(startIndex, index) + lowerCaseReplacement + textTail;

            return (true, text);
        }
    }
}
