﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Operations;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FlowAnalysis
{
    /// <summary>
    /// Control flow graph representation for a given executable code block <see cref="OriginalOperation"/>.
    /// This graph contains a set of <see cref="BasicBlock"/>s, with an entry block, zero
    /// or more intermediate basic blocks and an exit block.
    /// Each basic block contains zero or more <see cref="BasicBlock.Operations"/> and
    /// explicit <see cref="ControlFlowBranch"/>(s) to other basic block(s).
    /// </summary>
    public sealed partial class ControlFlowGraph
    {
        private readonly ControlFlowGraphBuilder.CaptureIdDispenser _captureIdDispenser;
        private readonly ImmutableDictionary<IMethodSymbol, (ControlFlowRegion region, ILocalFunctionOperation operation, int ordinal)> _localFunctionsMap;
        private ControlFlowGraph[] _lazyLocalFunctionsGraphs;
        private readonly ImmutableDictionary<IFlowAnonymousFunctionOperation, (ControlFlowRegion region, int ordinal)> _anonymousFunctionsMap;
        private ControlFlowGraph[] _lazyAnonymousFunctionsGraphs;

        internal ControlFlowGraph(IOperation originalOperation,
                                  ControlFlowGraphBuilder.CaptureIdDispenser captureIdDispenser,
                                  ImmutableArray<BasicBlock> blocks, ControlFlowRegion root,
                                  ImmutableArray<IMethodSymbol> localFunctions,
                                  ImmutableDictionary<IMethodSymbol, (ControlFlowRegion region, ILocalFunctionOperation operation, int ordinal)> localFunctionsMap,
                                  ImmutableDictionary<IFlowAnonymousFunctionOperation, (ControlFlowRegion region, int ordinal)> anonymousFunctionsMap)
        {
            Debug.Assert(captureIdDispenser != null);
            Debug.Assert(!blocks.IsDefault);
            Debug.Assert(blocks.First().Kind == BasicBlockKind.Entry);
            Debug.Assert(blocks.Last().Kind == BasicBlockKind.Exit);
            Debug.Assert(root != null);
            Debug.Assert(root.Kind == ControlFlowRegionKind.Root);
            Debug.Assert(root.FirstBlockOrdinal == 0);
            Debug.Assert(root.LastBlockOrdinal == blocks.Length - 1);
            Debug.Assert(!localFunctions.IsDefault);
            Debug.Assert(localFunctionsMap != null);
            Debug.Assert(localFunctionsMap.Count == localFunctions.Length);
            Debug.Assert(localFunctions.Distinct().Count() == localFunctions.Length);
            Debug.Assert(anonymousFunctionsMap != null);
#if DEBUG
            foreach (IMethodSymbol method in localFunctions)
            {
                Debug.Assert(method.MethodKind == MethodKind.LocalFunction);
                Debug.Assert(localFunctionsMap.ContainsKey(method));
            }
#endif 

            OriginalOperation = originalOperation;
            Blocks = blocks;
            Root = root;
            LocalFunctions = localFunctions;
            _localFunctionsMap = localFunctionsMap;
            _anonymousFunctionsMap = anonymousFunctionsMap;
            _captureIdDispenser = captureIdDispenser;
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="body"/>.
        /// </summary>
        /// <param name="body">Root operation block, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IBlockOperation body)
        {
            return CreateCore(body, nameof(body));
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="initializer"/>.
        /// </summary>
        /// <param name="initializer">Root field initializer operation, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IFieldInitializerOperation initializer)
        {
            return CreateCore(initializer, nameof(initializer));
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="initializer"/>.
        /// </summary>
        /// <param name="initializer">Root property initializer operation, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IPropertyInitializerOperation initializer)
        {
            return CreateCore(initializer, nameof(initializer));
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="initializer"/>.
        /// </summary>
        /// <param name="initializer">Root parameter initializer operation, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IParameterInitializerOperation initializer)
        {
            return CreateCore(initializer, nameof(initializer));
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="constructorBody"/>.
        /// </summary>
        /// <param name="constructorBody">Root constructor body operation, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IConstructorBodyOperation constructorBody)
        {
            return CreateCore(constructorBody, nameof(constructorBody));
        }

        /// <summary>
        /// Creates a <see cref="ControlFlowGraph"/> for the given executable code block <paramref name="methodBody"/>.
        /// </summary>
        /// <param name="methodBody">Root method body operation, which must have a null parent.</param>
        public static ControlFlowGraph Create(Operations.IMethodBodyOperation methodBody)
        {
            return CreateCore(methodBody, nameof(methodBody));
        }

        internal static ControlFlowGraph CreateCore(IOperation operation, string argumentNameForException)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(argumentNameForException);
            }

            if (operation.Parent != null)
            {
                throw new ArgumentException(CodeAnalysisResources.NotARootOperation, argumentNameForException);
            }

            if (((Operation)operation).SemanticModel == null)
            {
                throw new ArgumentException(CodeAnalysisResources.OperationHasNullSemanticModel, argumentNameForException);
            }

            if (!operation.Syntax.SyntaxTree.Options.Features.ContainsKey("flow-analysis"))
            {
                throw new InvalidOperationException(CodeAnalysisResources.FlowAnalysisFeatureDisabled);
            }

            try
            {
                ControlFlowGraph controlFlowGraph = ControlFlowGraphBuilder.Create(operation);
                Debug.Assert(controlFlowGraph.OriginalOperation == operation);
                return controlFlowGraph;
            }
            catch (Exception e) when (FatalError.ReportWithoutCrashUnlessCanceled(e))
            {
                // Log a Non-fatal-watson and then ignore the crash in the attempt of getting flow graph.
                Debug.Assert(false, "\n" + e.ToString());
            }

            return default;
        }

        /// <summary>
        /// Original operation, representing an executable code block, from which this control flow graph was generated.
        /// Note that <see cref="BasicBlock.Operations"/> in the control flow graph are not in the same operation tree as
        /// the original operation.
        /// </summary>
        public IOperation OriginalOperation { get; }

        /// <summary>
        /// Basic blocks for the control flow graph.
        /// </summary>
        public ImmutableArray<BasicBlock> Blocks { get; }

        /// <summary>
        /// Root (<see cref="ControlFlowRegionKind.Root"/>) region for the graph.
        /// </summary>
        public ControlFlowRegion Root { get; }

        /// <summary>
        /// Local functions declared within <see cref="OriginalOperation"/>.
        /// </summary>
        public ImmutableArray<IMethodSymbol> LocalFunctions { get; }

        /// <summary>
        /// Creates a control flow graph for the given <paramref name="localFunction"/>.
        /// </summary>
        public ControlFlowGraph GetLocalFunctionControlFlowGraph(IMethodSymbol localFunction)
        {
            if (localFunction is null)
            {
                throw new ArgumentNullException(nameof(localFunction));
            }

            if (!_localFunctionsMap.TryGetValue(localFunction, out (ControlFlowRegion enclosing, ILocalFunctionOperation operation, int ordinal) info))
            {
                throw new ArgumentOutOfRangeException(nameof(localFunction));
            }

            Debug.Assert(localFunction == LocalFunctions[info.ordinal]);

            if (_lazyLocalFunctionsGraphs == null)
            {
                Interlocked.CompareExchange(ref _lazyLocalFunctionsGraphs, new ControlFlowGraph[LocalFunctions.Length], null);
            }

            if (_lazyLocalFunctionsGraphs[info.ordinal] == null)
            {
                Debug.Assert(localFunction == info.operation.Symbol);
                ControlFlowGraph graph = ControlFlowGraphBuilder.Create(info.operation, info.enclosing, _captureIdDispenser);
                Debug.Assert(graph.OriginalOperation == info.operation);
                Interlocked.CompareExchange(ref _lazyLocalFunctionsGraphs[info.ordinal], graph, null);
            }

            return _lazyLocalFunctionsGraphs[info.ordinal];
        }

        /// <summary>
        /// Creates a control flow graph for the given <paramref name="anonymousFunction"/>.
        /// </summary>
        public ControlFlowGraph GetAnonymousFunctionControlFlowGraph(IFlowAnonymousFunctionOperation anonymousFunction)
        {
            if (anonymousFunction is null)
            {
                throw new ArgumentNullException(nameof(anonymousFunction));
            }

            if (!_anonymousFunctionsMap.TryGetValue(anonymousFunction, out (ControlFlowRegion enclosing, int ordinal) info))
            {
                throw new ArgumentOutOfRangeException(nameof(anonymousFunction));
            }

            if (_lazyAnonymousFunctionsGraphs == null)
            {
                Interlocked.CompareExchange(ref _lazyAnonymousFunctionsGraphs, new ControlFlowGraph[_anonymousFunctionsMap.Count], null);
            }

            if (_lazyAnonymousFunctionsGraphs[info.ordinal] == null)
            {
                var anonymous = (FlowAnonymousFunctionOperation)anonymousFunction;
                ControlFlowGraph graph = ControlFlowGraphBuilder.Create(anonymous.Original, info.enclosing, _captureIdDispenser, in anonymous.Context);
                Debug.Assert(graph.OriginalOperation == anonymous.Original);
                Interlocked.CompareExchange(ref _lazyAnonymousFunctionsGraphs[info.ordinal], graph, null);
            }

            return _lazyAnonymousFunctionsGraphs[info.ordinal];
        }
    }
}
