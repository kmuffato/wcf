// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MS.Internal.Xml.XPath
{
    using System;
    using Microsoft.Xml;
    using Microsoft.Xml.XPath;
    using System.Diagnostics;
    using System.Collections.Generic;
    using StackNav = ClonableStack<Microsoft.Xml.XPath.XPathNavigator>;

    // Algorithm:
    // Input assumption: qyInput is in DocOrder.
    // Preceding of a sequence of nodes will be preceding of last node in DocOrder in that sequence.
    // Because qyInput is in DO last input is last node in DO. -- "last"
    // If last node is attribute or namespace move last to it element.
    // Push this last node and all its ancestors into the ancestorStk. The root node will be the top-most element on the stack.
    // Create descendent iterator from the root. -- "workIterator"
    // Advancing workIterator we meet all nodes from the ancestorStk in stack order. Nodes in ancestorStk do no belong to the
    // the 'preceding' axis and must be ignored.
    // Last node in ancestorStk is a centinel node; when we pop it from ancestorStk, we should stop iterations.

    internal sealed class PrecedingQuery : BaseAxisQuery
    {
        private XPathNodeIterator _workIterator;
        private StackNav _ancestorStk;

        public PrecedingQuery(Query qyInput, string name, string prefix, XPathNodeType typeTest) : base(qyInput, name, prefix, typeTest)
        {
            _ancestorStk = new StackNav();
        }
        private PrecedingQuery(PrecedingQuery other) : base(other)
        {
            _workIterator = Clone(other._workIterator);
            _ancestorStk = other._ancestorStk.Clone();
        }

        public override void Reset()
        {
            _workIterator = null;
            _ancestorStk.Clear();
            base.Reset();
        }

        public override XPathNavigator Advance()
        {
            if (_workIterator == null)
            {
                XPathNavigator last;
                {
                    XPathNavigator input = qyInput.Advance();
                    if (input == null)
                    {
                        return null;
                    }
                    last = input.Clone();
                    do
                    {
                        last.MoveTo(input);
                    } while ((input = qyInput.Advance()) != null);

                    if (last.NodeType == XPathNodeType.Attribute || last.NodeType == XPathNodeType.Namespace)
                    {
                        last.MoveToParent();
                    }
                }
                // Fill ancestorStk :
                do
                {
                    _ancestorStk.Push(last.Clone());
                } while (last.MoveToParent());
                // Create workIterator :
                // last.MoveToRoot(); We are on root already
                _workIterator = last.SelectDescendants(XPathNodeType.All, true);
            }

            while (_workIterator.MoveNext())
            {
                currentNode = _workIterator.Current;
                if (currentNode.IsSamePosition(_ancestorStk.Peek()))
                {
                    _ancestorStk.Pop();
                    if (_ancestorStk.Count == 0)
                    {
                        currentNode = null;
                        _workIterator = null;
                        Debug.Assert(qyInput.Advance() == null, "we read all qyInput.Advance() already");
                        return null;
                    }
                    continue;
                }
                if (matches(currentNode))
                {
                    position++;
                    return currentNode;
                }
            }
            Debug.Fail("Algorithm error: we missed the centinel node");
            return null;
        }

        public override XPathNodeIterator Clone() { return new PrecedingQuery(this); }
        public override QueryProps Properties { get { return base.Properties | QueryProps.Reverse; } }
    }
}

