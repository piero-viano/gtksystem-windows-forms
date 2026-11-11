// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace System.Windows.Forms
{
    /// <summary>
    ///  Provides data for the <c>TreeView.OnBeforeCheck</c>,
    ///  <c>TreeView.OnBeforeCollapse</c>,
    ///  <c>TreeView.OnBeforeExpand</c>,
    ///  or <c>TreeView.OnBeforeSelect</c> event.
    /// </summary>
    public class TreeViewCancelEventArgs : CancelEventArgs
    {
        private readonly TreeNode node;
        private readonly TreeViewAction action;

        public TreeViewCancelEventArgs(TreeNode node, bool cancel, TreeViewAction action)
        : base(cancel)
        {
            this.node = node;
            this.action = action;
        }

        public TreeNode Node
        {
            get
            {
                return node;
            }
        }

        public TreeViewAction Action
        {
            get
            {
                return action;
            }
        }
    }
}
