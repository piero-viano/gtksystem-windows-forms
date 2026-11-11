// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Windows.Forms
{
    /// <summary>
    ///  Represents a method that will handle the <c>TreeView.OnBeforeCheck</c>,
    /// <c>TreeView.OnBeforeCollapse</c>, <c>TreeView.BeforeExpand</c>,
    ///  or <see cref='TreeView.BeforeSelect'/> event of a <see cref='TreeView'/>
    /// </summary>
    public delegate void TreeViewCancelEventHandler(object sender, TreeViewCancelEventArgs e);
}
