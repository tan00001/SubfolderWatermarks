// <copyright file="WaterMarkOutputPane.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System;
using System.Runtime.Remoting.Messaging;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using static Microsoft.VisualStudio.Threading.AsyncReaderWriterLock;

namespace SubfolderWatermarks
{
    public class WaterMarkOutputPane
    {
        private static Guid wxPaneGuid = new Guid("3AA8DE7D-AC32-408D-BF0D-28C0871F4EFA");

#pragma warning disable SA1309 // Field names should not begin with underscore
        private static WaterMarkOutputPane _instance;
#pragma warning restore SA1309 // Field names should not begin with underscore

        private readonly IVsOutputWindowPane wmPane;

        private WaterMarkOutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow)
            {
                outWindow.GetPane(ref wxPaneGuid, out this.wmPane);

                if (this.wmPane == null)
                {
                    outWindow.CreatePane(ref wxPaneGuid, "Water Mark", 1, 0);
                    outWindow.GetPane(ref wxPaneGuid, out this.wmPane);
                }
            }
        }

        public static WaterMarkOutputPane Instance => _instance ?? (_instance = new WaterMarkOutputPane());

        public static void Write(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Instance.wmPane.OutputStringThreadSafe(message);
        }

        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.wmPane.Activate();
        }
    }
}
