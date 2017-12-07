﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename
{
    internal partial class RenameCommandHandler : IChainedCommandHandler<ReturnKeyCommandArgs>
    {
        public VisualStudio.Commanding.CommandState GetCommandState(ReturnKeyCommandArgs args, Func<VisualStudio.Commanding.CommandState> nextHandler)
        {
            return GetCommandState(nextHandler);
        }

        public void ExecuteCommand(ReturnKeyCommandArgs args, Action nextHandler, CommandExecutionContext context)
        {
            if (_renameService.ActiveSession != null)
            {
                _renameService.ActiveSession.Commit();
                (args.TextView as IWpfTextView).VisualElement.Focus();
            }
            else
            {
                nextHandler();
            }
        }
    }
}
