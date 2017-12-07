﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.Completion
{
    internal partial class Controller
    {
        // Cut and Paste should always dismiss completion

        VisualStudio.Commanding.CommandState IChainedCommandHandler<CutCommandArgs>.GetCommandState(CutCommandArgs args, System.Func<VisualStudio.Commanding.CommandState> nextHandler)
        {
            AssertIsForeground();
            return nextHandler();
        }

        void IChainedCommandHandler<CutCommandArgs>.ExecuteCommand(CutCommandArgs args, Action nextHandler, CommandExecutionContext context)
        {
            AssertIsForeground();
            DismissSessionIfActive();
            nextHandler();
        }

        VisualStudio.Commanding.CommandState IChainedCommandHandler<PasteCommandArgs>.GetCommandState(PasteCommandArgs args, System.Func<VisualStudio.Commanding.CommandState> nextHandler)
        {
            AssertIsForeground();
            return nextHandler();
        }

        void IChainedCommandHandler<PasteCommandArgs>.ExecuteCommand(PasteCommandArgs args, Action nextHandler, CommandExecutionContext context)
        {
            AssertIsForeground();
            DismissSessionIfActive();
            nextHandler();
        }
    }
}
