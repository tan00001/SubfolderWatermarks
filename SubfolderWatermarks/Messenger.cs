// <copyright file="Messenger.cs" company="Programount Inc.">
// Copyright (c) Programount Inc.. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
namespace SubfolderWatermarks
{
    public static class Messenger
    {
        public delegate void UpdateAdornmentEventHandler();

        public static event UpdateAdornmentEventHandler UpdateAdornment;

        public static void RequestUpdateAdornment()
        {
            System.Diagnostics.Debug.WriteLine("RequestUpdateAdornment");
            UpdateAdornment?.Invoke();
        }
    }
}
