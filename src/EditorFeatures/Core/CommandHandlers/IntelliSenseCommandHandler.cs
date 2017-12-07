﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CommandHandlers
{
    [Export(typeof(VisualStudio.Commanding.ICommandHandler))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(PredefinedCommandHandlerNames.IntelliSense)]
    [HandlesCommand(typeof(EscapeKeyCommandArgs))]
    [HandlesCommand(typeof(UpKeyCommandArgs))]
    [HandlesCommand(typeof(DownKeyCommandArgs))]
    internal sealed class IntelliSenseCommandHandler : AbstractIntelliSenseCommandHandler
    {
        [ImportingConstructor]
        public IntelliSenseCommandHandler(
               CompletionCommandHandler completionCommandHandler,
               SignatureHelpCommandHandler signatureHelpCommandHandler,
               QuickInfoCommandHandlerAndSourceProvider quickInfoCommandHandler)
            : base(completionCommandHandler,
                   signatureHelpCommandHandler,
                   quickInfoCommandHandler)
        {
        }
    }
}
