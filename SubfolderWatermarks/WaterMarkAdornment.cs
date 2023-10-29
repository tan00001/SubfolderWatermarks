// <copyright file="WaterMarkAdornment.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EnvDTE;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

using static Nerdbank.Streams.MultiplexingStream;

namespace SubfolderWatermarks
{
    public class WaterMarkAdornment
    {
#pragma warning disable SA1309 // Field names should not begin with underscore
        private readonly WaterMarkControl _root;
        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _adornmentLayer;
        private string _fileName = null;
#pragma warning restore SA1309 // Field names should not begin with underscore

        public WaterMarkAdornment(IWpfTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _view = view;
            _root = new WaterMarkControl();

            // Grab a reference to the adornment layer that this adornment should be added to
            _adornmentLayer = view.GetAdornmentLayer(nameof(WaterMarkAdornment));

            // Reposition the adornment whenever the editor window is resized
            _view.ViewportHeightChanged += (sender, e) => OnSizeChanged();
            _view.ViewportWidthChanged += (sender, e) => OnSizeChanged();

            _view.Closed += (s, e) => OnViewClosed();

            Messenger.UpdateAdornment += new Messenger.UpdateAdornmentEventHandler(OnUpdateRequested);

            RefreshAdornment();
        }

        public void RefreshAdornment()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // clear the adornment layer of previous adornments
            _adornmentLayer.RemoveAdornment(_root);

            if (TryLoadOptions())
            {
                try
                {
                    // add the image to the adornment layer and make it relative to the viewports
                    _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _root, null);
                }
                catch (InvalidOperationException ioe)
                {
                    OutputError("Unable to display the water mark at this time due to layout issues.", ioe);
                }
                catch (ArgumentException argexc)
                {
                    // This started happening when document is first loading in ~vs.17.3
                    if (!argexc.StackTrace.Contains("System.Windows.Media.VisualCollection.Add("))
                    {
                        OutputError($"Unable to display the watermark{Environment.NewLine}{argexc}", argexc);
                    }
                }
                catch (Exception exc)
                {
                    OutputError($"Unable to display the water mark{Environment.NewLine}{exc}", exc);
                }
            }
        }

        private static string GetProjectPathFromMiscFile(Solution solution, string folderPath, string curFilePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (Project proj in solution.Projects)
            {
                // Misc. project has empty FullName
                if (string.IsNullOrEmpty(proj.FullName))
                {
                    continue;
                }

                var projectPath = Path.GetDirectoryName(proj.FullName);
                if (curFilePath.StartsWith(Path.Combine(projectPath, folderPath), StringComparison.InvariantCultureIgnoreCase))
                {
                    return projectPath;
                }
            }

            return null;
        }

        private void OnUpdateRequested()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            RefreshAdornment();
        }

        private void OnSizeChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            RefreshAdornment();
        }

        private void OnViewClosed()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _view.ViewportHeightChanged -= (sender, e) => OnSizeChanged();
            _view.ViewportWidthChanged -= (sender, e) => OnSizeChanged();

            _view.Closed -= (s, e) => OnViewClosed();

            Messenger.UpdateAdornment -= new Messenger.UpdateAdornmentEventHandler(OnUpdateRequested);
        }

        private string GetFileName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_fileName == null)
            {
                _view.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer buffer);

                if (buffer is IPersistFileFormat pff)
                {
                    pff.GetCurFile(out _fileName, out _);
                }
            }

            return _fileName;
        }

        private bool TryLoadOptions()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (SubfolderWatermarksPackage.Instance != null)
                {
                    var options = SubfolderWatermarksPackage.Instance.Options;

                    if (options?.IsEnabled != true)
                    {
                        System.Diagnostics.Debug.WriteLine("options not enabled");
                        return false;
                    }

                    if (!CheckFoldersOption(options))
                    {
                        return false;
                    }

                    try
                    {
                        if (options.IsUsingImage())
                        {
                            if (!SetImage(options))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!SetText(options))
                            {
                                return false;
                            }
                        }

                        SetAdornmentPosition(options);

                        return true;
                    }
                    catch (Exception exc)
                    {
                        OutputError($"Unable to set the text to {options.DisplayedText}", exc);
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine("Package not loaded");

                // Try and load the package so it's there the next time try to access it.
                if (ServiceProvider.GlobalProvider.GetService(typeof(SVsShell)) is IVsShell shell)
                {
                    Guid packageToBeLoadedGuid = new Guid(SubfolderWatermarksPackage.PackageGuidString);
                    shell.LoadPackage(ref packageToBeLoadedGuid, out _);
                }
            }
            catch (Exception exc)
            {
                OutputError($"Unable to load options", exc);
            }

            return false;
        }

        private void LoadTextSettings(OptionPageGrid options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var optionsHash = options.GetSettingsHash();

            if (_root.WaterMarkText.Tag is int hash && hash == optionsHash)
            {
                return;
            }

            bool settingsUpdated = true;

            try
            {
                _root.WaterMarkText.FontSize = options.TextSize;
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the FontSize to {options.TextSize}", exc);
            }

            try
            {
                _root.WaterMarkText.FontFamily = new FontFamily(options.FontFamilyName);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the FontFamily to {options.FontFamilyName}", exc);
            }

            try
            {
                _root.WaterMarkText.FontWeight = options.IsFontBold ? FontWeights.Bold : FontWeights.Normal;
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError("Unable to set the FontWeight", exc);
            }

            try
            {
                _root.WaterMarkText.FontStyle = options.IsFontItalic ? FontStyles.Italic : FontStyles.Normal;
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError("Unable to set the FontStyle", exc);
            }

            try
            {
                if ((options.IsFontUnderline || options.IsFontStrikeThrough)
                    && _root.WaterMarkText.Content is string textContent)
                {
                    var textBlock = new TextBlock
                    {
                        Text = textContent,
                        TextDecorations = new TextDecorationCollection(),
                    };

                    if (options.IsFontUnderline)
                    {
                        textBlock.TextDecorations.Add(TextDecorations.Underline);
                    }

                    if (options.IsFontStrikeThrough)
                    {
                        textBlock.TextDecorations.Add(TextDecorations.Strikethrough);
                    }

                    _root.WaterMarkText.Content = textBlock;
                }
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError("Unable to set the FontStyle", exc);
            }

            try
            {
                _root.WaterMarkText.Foreground = GetColorBrush(options.TextColor);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Text Color to {options.TextColor}", exc);
            }

            try
            {
                _root.WaterMarkBorder.Background = GetColorBrush(options.BackgroundColor);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Background to {options.BackgroundColor}", exc);
            }

            try
            {
                _root.WaterMarkBorder.BorderBrush = GetColorBrush(options.BorderColor);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Border Color to {options.BorderColor}", exc);
            }

            try
            {
                _root.WaterMarkBorder.Padding = new Thickness(options.BorderPadding);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Padding to {options.BorderPadding}", exc);
            }

            try
            {
                _root.WaterMarkBorder.Margin = new Thickness(options.BorderMargin);
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Margin to {options.BorderMargin}", exc);
            }

            try
            {
                _root.WaterMarkBorder.Opacity = options.BorderOpacity;
            }
            catch (Exception exc)
            {
                settingsUpdated = false;
                OutputError($"Unable to set the Opacity to {options.BorderOpacity}", exc);
            }

            if (settingsUpdated)
            {
                try
                {
                    _root.WaterMarkText.Tag = optionsHash;
                }
                catch (Exception exc)
                {
                    OutputError($"Unable to set the Opacity to {options.BorderOpacity}", exc);
                }
            }
        }

        private bool SetText(OptionPageGrid options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _root.WaterMarkImage.Visibility = Visibility.Hidden;
            _root.WaterMarkText.Visibility = Visibility.Visible;

            string displayedText = options.UsingReplacements() ? MakeReplacements(options) : options.DisplayedText;

            if (_root.WaterMarkText.Content is string waterMarktext && !waterMarktext.Equals(displayedText))
            {
                _root.WaterMarkText.Content = displayedText;
            }
            else if (_root.WaterMarkText.Content is TextBlock waterMarkTextBlock && !waterMarkTextBlock.Text.Equals(displayedText))
            {
                waterMarkTextBlock.Text = displayedText;
            }

            if (!string.IsNullOrWhiteSpace(displayedText))
            {
                LoadTextSettings(options);
                return true;
            }

            return false;
        }

        private string MakeReplacements(OptionPageGrid options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var curFile = GetFileName();

            if (string.IsNullOrWhiteSpace(curFile))
            {
                // We try and refresh/reload/reposition the adornment any time somethign relevant happens.
                // This includes when documents are initially opened and resized but before the file name is available.
                // Avoid filling the output window with these messages when there's nothing the user can do about them.
                // OutputError("Unable to get name of the current file.");
                System.Diagnostics.Debug.WriteLine("Unable to get name of the current file.");
                return string.Empty;
            }

            // We know we have replacements to make before coming in here
            var displayedText = options.GetDisplayTextFormat();

            if (options.UseCurrentFileName())
            {
                try
                {
                    // Occurances of replacement in displayedText were already made lower case when the options was
                    // initialized. This simplifies the replacements
                    displayedText = displayedText.Replace(OptionPageGrid.CurrentFileName, Path.GetFileName(curFile));
                }
                catch (Exception exc)
                {
                    OutputError($"Unable to get the name of the file from the path '{curFile}'.", exc);
                    return string.Empty;
                }
            }

            if (options.UseCurrentDirectoryName())
            {
                try
                {
                    // Occurances of replacement in displayedText were already made lower case when the options was
                    // initialized. This simplifies the replacements
                    displayedText = displayedText.Replace(OptionPageGrid.CurrentDirectoryName, Path.GetFileName(Path.GetDirectoryName(curFile)));
                }
                catch (Exception exc)
                {
                    OutputError($"Unable to get the name of the dirctory from the current file path '{curFile}'.", exc);
                    return string.Empty;
                }
            }

            if (!options.UseCurrentProjectName() && !options.UseCurrentFilePathInProject())
            {
                return displayedText;
            }

            var solution = ProjectHelpers.Dte2.Solution;
            var projItem = solution.FindProjectItem(curFile);

            if (options.UseCurrentProjectName() && projItem != null)
            {
                try
                {
                    displayedText = displayedText.Replace(OptionPageGrid.CurrentProjectName, projItem.ContainingProject.Name);
                }
                catch (Exception exc)
                {
                    OutputError("Unable to get the name of the project the current file is in.", exc);
                    return string.Empty;
                }
            }

            if (options.UseCurrentFilePathInProject())
            {
                try
                {
                    if (!MakeReplacementWithRelativePaths())
                    {
                        if (!MakeReplacementWithAbsolutePaths())
                        {
                            displayedText = displayedText.Replace(OptionPageGrid.CurrentFilePathInProject, string.Empty);
                        }
                    }
                }
                catch (Exception exc)
                {
                    OutputError($"Unable to get the name of the dirctory from the current file path '{curFile}'.", exc);
                    return string.Empty;
                }
            }

            return displayedText;

            bool MakeReplacementWithRelativePaths()
            {
                foreach (var folderPath in options.GetRelativeContainingFolders())
                {
                    string projectFolderPath;

                    if (!string.IsNullOrEmpty(projItem?.ContainingProject.FullName))
                    {
                        projectFolderPath = Path.GetDirectoryName(projItem.ContainingProject.FullName);
                        if (curFile.StartsWith(projectFolderPath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            displayedText = displayedText.Replace(OptionPageGrid.CurrentFilePathInProject, curFile.Substring(projectFolderPath.Length));
                            return true;
                        }
                    }
                    else
                    {
                        projectFolderPath = GetProjectPathFromMiscFile(solution, folderPath, curFile);
                        if (!string.IsNullOrEmpty(projectFolderPath))
                        {
                            displayedText = displayedText.Replace(OptionPageGrid.CurrentFilePathInProject, curFile.Substring(projectFolderPath.Length));
                            return true;
                        }
                    }
                }

                return false;
            }

            bool MakeReplacementWithAbsolutePaths()
            {
                foreach (var folderPath in options.GetAbsoluteContainingFolders())
                {
                    if (curFile.StartsWith(folderPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        displayedText = displayedText.Replace(OptionPageGrid.CurrentFilePathInProject, curFile.Substring(folderPath.Length));
                        return true;
                    }
                }

                return false;
            }
        }

        private void SetAdornmentPosition(OptionPageGrid options)
        {
            // TODO: If right-aligned, need to remeasure the width appropriately | See  #9
            ////_ = System.Threading.Tasks.Task.Delay(200).ConfigureAwait(true);
            ////_root.Measure((_view as FrameworkElement).RenderSize);

            // Need to force a reshresh after the content has been changed to ensure it gets aligned correctly.
            ////ThreadHelper.JoinableTaskFactory.Run(async () =>
            ////{
            ////    // A small pause for the adornment to be drawn at the new size and then request update to pick up new width.
            ////    await System.Threading.Tasks.Task.Delay(200);

            ////    ////Messenger.RequestUpdateAdornment();
            ////    ///
            ////    RefreshAdornment();
            ////});
            if (options.PositionTop)
            {
                Canvas.SetTop(_root, _view.ViewportTop);
            }
            else
            {
                Canvas.SetTop(_root, _view.ViewportBottom - _root.ActualHeight);
            }

            if (options.PositionLeft)
            {
                Canvas.SetLeft(_root, _view.ViewportLeft);
            }
            else
            {
                Canvas.SetLeft(_root, _view.ViewportRight - _root.ActualWidth);
            }
        }

        private bool SetImage(OptionPageGrid options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                _root.WaterMarkImage.Visibility = Visibility.Visible;
                _root.WaterMarkText.Visibility = Visibility.Hidden;

                var imagePath = options.GetImagePath();

                if (File.Exists(imagePath))
                {
                    _root.WaterMarkImage.Source = new BitmapImage(new Uri(imagePath));
                    return true;
                }
                else
                {
                    OutputError($"Specified image not found: '{imagePath}'");
                    return false;
                }
            }
            catch (Exception exc)
            {
                OutputError($"Unable to set image.", exc);
                return false;
            }
        }

        private bool CheckFoldersOption(OptionPageGrid option)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var absoluteFolders = option.GetAbsoluteContainingFolders();
            var relativeFolders = option.GetRelativeContainingFolders();
            if (absoluteFolders.Count == 0 && relativeFolders.Count == 0)
            {
                return true;
            }

            var curFile = GetFileName();
            if (string.IsNullOrEmpty(curFile))
            {
                return false;
            }

            foreach (var folderPath in absoluteFolders)
            {
                if (curFile.StartsWith(folderPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var folderPath in relativeFolders)
            {
                string fullFolderPath;

                var solution = ProjectHelpers.Dte2.Solution;
                var projItem = solution.FindProjectItem(curFile);

                if (!string.IsNullOrEmpty(projItem?.ContainingProject.FullName))
                {
                    var projectFolderPath = Path.GetDirectoryName(projItem.ContainingProject.FullName);
                    fullFolderPath = Path.Combine(projectFolderPath, folderPath);

                    if (curFile.StartsWith(fullFolderPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(GetProjectPathFromMiscFile(solution, folderPath, curFile)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private SolidColorBrush GetColorBrush(string color)
        {
            if (!color.TrimStart().StartsWith("#"))
            {
                color = this.GetHexForNamedColor(color.Trim());
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Trim()));
        }

        private void OutputError(string message, Exception exc = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (exc != null)
            {
                System.Diagnostics.Debug.WriteLine(exc);
            }

            if (SubfolderWatermarksPackage.Instance.Options.ShowDebugOutput)
            {
                WaterMarkOutputPane.Write($"{message}: {exc?.Message ?? "Exception is null"}{Environment.NewLine}");
            }
        }

        private string GetHexForNamedColor(string colorName)
        {
            switch (colorName.ToLowerInvariant())
            {
                case "aliceblue": return "#F0F8FF";
                case "antiquewhite": return "#FAEBD7";
                case "aqua": return "#00FFFF";
                case "aquamarine": return "#7FFFD4";
                case "azure": return "#F0FFFF";
                case "beige": return "#F5F5DC";
                case "bisque": return "#FFE4C4";
                case "black": return "#000000";
                case "blanchedalmond": return "#FFEBCD";
                case "blue": return "#0000FF";
                case "blueviolet": return "#8A2BE2";
                case "brown": return "#A52A2A";
                case "burgendy": return "#FF6347";
                case "burlywood": return "#DEB887";
                case "cadetblue": return "#5F9EA0";
                case "chartreuse": return "#7FFF00";
                case "chocolate": return "#D2691E";
                case "coral": return "#FF7F50";
                case "cornflowerblue": return "#6495ED";
                case "cornsilk": return "#FFF8DC";
                case "crimson": return "#DC143C";
                case "cyan": return "#00FFFF";
                case "darkblue": return "#00008B";
                case "darkcyan": return "#008B8B";
                case "darkgoldenrod": return "#B8860B";
                case "darkgray": return "#A9A9A9";
                case "darkgreen": return "#006400";
                case "darkgrey": return "#A9A9A9";
                case "darkkhaki": return "#BDB76B";
                case "darkmagenta": return "#8B008B";
                case "darkolivegreen": return "#556B2F";
                case "darkorange": return "#FF8C00";
                case "darkorchid": return "#9932CC";
                case "darkred": return "#8B0000";
                case "darksalmon": return "#E9967A";
                case "darkseagreen": return "#8FBC8B";
                case "darkslateblue": return "#483D8B";
                case "darkslategray": return "#2F4F4F";
                case "darkslategrey": return "#2F4F4F";
                case "darkturquoise": return "#00CED1";
                case "darkviolet": return "#9400D3";
                case "darkyellow": return "#D7C32A";
                case "deeppink": return "#FF1493";
                case "deepskyblue": return "#00BFFF";
                case "dimgray": return "#696969";
                case "dimgrey": return "#696969";
                case "dodgerblue": return "#1E90FF";
                case "firebrick": return "#B22222";
                case "floralwhite": return "#FFFAF0";
                case "forestgreen": return "#228B22";
                case "fuchsia": return "#FF00FF";
                case "gainsboro": return "#DCDCDC";
                case "ghostwhite": return "#F8F8FF";
                case "gold": return "#FFD700";
                case "goldenrod": return "#DAA520";
                case "gray": return "#808080";
                case "green": return "#008000";
                case "greenyellow": return "#ADFF2F";
                case "grey": return "#808080";
                case "honeydew": return "#F0FFF0";
                case "hotpink": return "#FF69B4";
                case "indianred": return "#CD5C5C";
                case "indigo": return "#4B0082";
                case "ivory": return "#FFFFF0";
                case "khaki": return "#F0E68C";
                case "lavender": return "#E6E6FA";
                case "lavenderblush": return "#FFF0F5";
                case "lawngreen": return "#7CFC00";
                case "lemonchiffon": return "#FFFACD";
                case "lightblue": return "#ADD8E6";
                case "lightcoral": return "#F08080";
                case "lightcyan": return "#E0FFFF";
                case "lightgoldenrodyellow": return "#FAFAD2";
                case "lightgray": return "#D3D3D3";
                case "lightgreen": return "#90EE90";
                case "lightgrey": return "#d3d3d3";
                case "lightpink": return "#FFB6C1";
                case "lightsalmon": return "#FFA07A";
                case "lightseagreen": return "#20B2AA";
                case "lightskyblue": return "#87CEFA";
                case "lightslategray": return "#778899";
                case "lightslategrey": return "#778899";
                case "lightsteelblue": return "#B0C4DE";
                case "lightyellow": return "#FFFFE0";
                case "lime": return "#00FF00";
                case "limegreen": return "#32CD32";
                case "linen": return "#FAF0E6";
                case "magenta": return "#FF00FF";
                case "maroon": return "#800000";
                case "mediumaquamarine": return "#66CDAA";
                case "mediumblue": return "#0000CD";
                case "mediumorchid": return "#BA55D3";
                case "mediumpurple": return "#9370DB";
                case "mediumseagreen": return "#3CB371";
                case "mediumslateblue": return "#7B68EE";
                case "mediumspringgreen": return "#00FA9A";
                case "mediumturquoise": return "#48D1CC";
                case "mediumvioletred": return "#C71585";
                case "midnightblue": return "#191970";
                case "mint": return "#66CDAA";
                case "mintcream": return "#F5FFFA";
                case "mistyrose": return "#FFE4E1";
                case "moccasin": return "#FFE4B5";
                case "navajowhite": return "#FFDEAD";
                case "navy": return "#000080";
                case "ochre": return "#D7C32A";
                case "oldlace": return "#FDF5E6";
                case "olive": return "#808000";
                case "olivedrab": return "#6B8E23";
                case "orange": return "#FFA500";
                case "orangered": return "#FF4500";
                case "orchid": return "#DA70D6";
                case "palegoldenrod": return "#EEE8AA";
                case "palegreen": return "#98FB98";
                case "paleturquoise": return "#AFEEEE";
                case "palevioletred": return "#DB7093";
                case "papayawhip": return "#FFEFD5";
                case "peachpuff": return "#FFDAB9";
                case "peru": return "#CD853F";
                case "pink": return "#FFC0CB";
                case "plum": return "#DDA0DD";
                case "powderblue": return "#B0E0E6";
                case "purple": return "#800080";
                case "pumpkin": return "#FF4500";
                case "rebeccapurple": return "#663399";
                case "red": return "#FF0000";
                case "rosybrown": return "#BC8F8F";
                case "royalblue": return "#4169E1";
                case "saddlebrown": return "#8B4513";
                case "salmon": return "#FA8072";
                case "sandybrown": return "#F4A460";
                case "seagreen": return "#2E8B57";
                case "seashell": return "#FFF5EE";
                case "sienna": return "#A0522D";
                case "silver": return "#C0C0C0";
                case "skyblue": return "#87CEEB";
                case "slateblue": return "#6A5ACD";
                case "slategray": return "#708090";
                case "slategrey": return "#708090";
                case "snow": return "#FFFAFA";
                case "springgreen": return "#00FF7F";
                case "steelblue": return "#4682B4";
                case "tan": return "#D2B48C";
                case "teal": return "#008080";
                case "thistle": return "#D8BFD8";
                case "tomato": return "#FF6347";
                case "turquoise": return "#40E0D0";
                case "violet": return "#EE82EE";
                case "volt": return "#CEFF00";
                case "wheat": return "#F5DEB3";
                case "white": return "#FFFFFF";
                case "whitesmoke": return "#F5F5F5";
                case "yellow": return "#FFFF00";
                case "yellowgreen": return "#9ACD32";
                default: return colorName;
            }
        }
    }
}
