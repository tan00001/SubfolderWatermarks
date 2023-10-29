// <copyright file="AdornmentFactory.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace SubfolderWatermarks
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class AdornmentFactory : IWpfTextViewCreationListener
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(nameof(WaterMarkAdornment))]
        [Order(After = PredefinedAdornmentLayers.Text)]
#pragma warning disable SA1401 // Fields should be private - made public for MEF
        public AdornmentLayerDefinition EditorAdornmentLayer = null;
#pragma warning restore SA1401 // Fields should be private

        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(() => new WaterMarkAdornment(textView));
        }
    }
}
