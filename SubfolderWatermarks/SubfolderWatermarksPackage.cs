// Copyright (c) Programount Inc. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// <copyright file="SubfolderWatermarksPackage.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SubfolderWatermarks
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(SubfolderWatermarksPackage.PackageGuidString)]
    [InstalledProductRegistration(Vsix.ProductName, Vsix.Description, Vsix.Version, IconResourceID = 400)] // Info on this package for Help/About
    [ProvideOptionPage(typeof(OptionPageGrid), Vsix.OptionGroupHeader, Vsix.OptionPageName, 106, 107, true)]
    [ProvideProfileAttribute(typeof(OptionPageGrid), Vsix.OptionGroupHeader, Vsix.ProfileObjectName, 106, 107, isToolsOptionPage: true, DescriptionResourceID = 108)]
    public sealed class SubfolderWatermarksPackage : AsyncPackage
    {
        public const string PackageGuidString = "a12983d0-a17b-436c-b132-076f82a6be17";

#pragma warning disable SA1401 // Fields should be private
        public static SubfolderWatermarksPackage Instance;
#pragma warning restore SA1401 // Fields should be private

#pragma warning disable SA1309 // Field names should not begin with underscore
        private DocumentEventHandlers _docHandlers = null;
        private IVsRunningDocumentTable _runningDocumentTable = null;
        private uint _cookie = 0;
#pragma warning restore SA1309 // Field names should not begin with underscore

        public OptionPageGrid Options
        {
            get
            {
                return (OptionPageGrid)this.GetDialogPage(typeof(OptionPageGrid));
            }
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_docHandlers == null && await this.GetServiceAsync(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable rdt)
            {
                try
                {
                    _docHandlers = new DocumentEventHandlers();

                    rdt.AdviseRunningDocTableEvents(_docHandlers, out _cookie);

                    if (_cookie != 0)
                    {
                        _runningDocumentTable = rdt;
                        SubfolderWatermarksPackage.Instance = this;
                    }
                    else
                    {
                        _docHandlers = null;
                        _runningDocumentTable = null;
                    }
                }
                catch
                {
                    _docHandlers = null;
                    _runningDocumentTable = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("InitializeAsync");
            Messenger.RequestUpdateAdornment();
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.Dispose(disposing);

            // Unregister event handler when the package is disposed
            if (_runningDocumentTable != null && _cookie != 0)
            {
                _ = _runningDocumentTable.UnadviseRunningDocTableEvents(_cookie);
                _cookie = 0; // Reset the cookie value to indicate that the events are unsubscribed.
            }
        }
    }
}
