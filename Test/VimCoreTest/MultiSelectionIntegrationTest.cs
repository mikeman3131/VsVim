﻿using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Vim.Extensions;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.FSharp.Core;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Vim.UnitTest
{
    /// <summary>
    /// Class for testing the full integration story of multiple carets
    /// </summary>
    public abstract class MultiSelectionIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected IVimBufferData _vimBufferData;
        protected IVimTextBuffer _vimTextBuffer;
        protected IWpfTextView _textView;
        protected ITextBuffer _textBuffer;
        protected IVimGlobalSettings _globalSettings;
        protected IVimLocalSettings _localSettings;
        protected IVimWindowSettings _windowSettings;
        protected IJumpList _jumpList;
        protected IKeyMap _keyMap;
        protected IVimData _vimData;
        protected INormalMode _normalMode;
        protected IVimHost _vimHost;
        protected MockVimHost _mockVimHost;
        protected TestableClipboardDevice _clipboardDevice;
        protected TestableMouseDevice _testableMouseDevice;

        protected virtual void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBufferData = _vimBuffer.VimBufferData;
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _normalMode = _vimBuffer.NormalMode;
            _keyMap = _vimBuffer.Vim.KeyMap;
            _localSettings = _vimBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _windowSettings = _vimBuffer.WindowSettings;
            _jumpList = _vimBuffer.JumpList;
            _vimHost = _vimBuffer.Vim.VimHost;
            _mockVimHost = (MockVimHost)_vimHost;
            _mockVimHost.BeepCount = 0;
            _mockVimHost.IsMultiSelectionSupported = true;
            _mockVimHost.TryCustomProcessFunc = TryCustomProcess;
            _vimData = Vim.VimData;
            _clipboardDevice = (TestableClipboardDevice)CompositionContainer.GetExportedValue<IClipboardDevice>();

            _testableMouseDevice = (TestableMouseDevice)MouseDevice;
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            _testableMouseDevice.YOffset = 0;
        }

        public override void Dispose()
        {
            _testableMouseDevice.IsLeftButtonPressed = false;
            _testableMouseDevice.Point = null;
            base.Dispose();
        }

        private bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            if (command.IsInsert)
            {
                // Simulate native simultaneouos insertion at all carets at the
                // same time.
                var text = command.AsInsert().Text;
                var oldCarets = CaretVirtualPoints;
                using (var textEdit = _textBuffer.CreateEdit())
                {
                    foreach (var caret in oldCarets)
                    {
                        textEdit.Insert(caret.Position.Position, text);
                    }
                    textEdit.Apply();
                }
                var snapshot = _textBuffer.CurrentSnapshot;
                var newCarets =
                    oldCarets
                    .Select(x => x.MapToSnapshot(snapshot).Add(text.Length))
                    .ToArray();
                _vimHost.SetSelectedSpans(_textView, newCarets.Select(x => new SelectedSpan(x)));
                return true;
            }
            return false;
        }

        private SnapshotPoint[] CaretPoints =>
            _vimHost.GetSelectedSpans(_textView).Select(x => x.CaretPoint.Position).ToArray();

        private VirtualSnapshotPoint[] CaretVirtualPoints =>
            _vimHost.GetSelectedSpans(_textView).Select(x => x.CaretPoint).ToArray();

        private SelectedSpan[] SelectedSpans =>
            _vimHost.GetSelectedSpans(_textView).ToArray();

        private VirtualSnapshotPoint GetPoint(int lineNumber, int column)
        {
            return _textView.GetVirtualPointInLine(lineNumber, column);
        }

        private void SetCaretPoints(params VirtualSnapshotPoint[] caretPoints)
        {
            _vimHost.SetSelectedSpans(_textView, caretPoints.Select(x => new SelectedSpan(x)));
        }

        private void AssertCarets(params VirtualSnapshotPoint[] expectedCarets)
        {
            var actualSpans = SelectedSpans;
            Assert.Equal(expectedCarets.Length, actualSpans.Length);
            for (var i = 0; i < expectedCarets.Length; i++)
            {
                Assert.Equal(expectedCarets[i].GetSelectedSpan(), actualSpans[i]);
            }
        }

        private void AssertSelections(params SelectedSpan[] expectedSpans)
        {
            var actualSpans = SelectedSpans;
            Assert.Equal(expectedSpans.Length, actualSpans.Length);
            for (var i = 0; i < expectedSpans.Length; i++)
            {
                Assert.Equal(expectedSpans[i], actualSpans[i]);
            }
        }

        private void AssertLines(params string[] lines)
        {
            Assert.Equal(lines, _textBuffer.GetLines());
        }

        public sealed class MockTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Mock inftrastructure should use the real text view for the
            /// primary selection and the internal data structure for the
            /// secondary selection
            /// </summary>
            [WpfFact]
            public void Basic()
            {
                Create("cat", "bat", "");
                SetCaretPoints(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetVirtualPointInLine(1, 1));
                var spans = SelectedSpans;

                // Verify real caret and real selection.
                Assert.Equal(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetCaretVirtualPoint());
                Assert.Equal(
                    new VirtualSnapshotSpan(new SnapshotSpan(_textView.GetPointInLine(0, 1), 0)),
                    _textView.GetVirtualSelectionSpan());

                // Verify secondary selection agrees with mock vim host.
                Assert.Single(_mockVimHost.SecondarySelectedSpans);
                Assert.Equal(
                    new SelectedSpan(_textView.GetVirtualPointInLine(1, 1)),
                    _mockVimHost.SecondarySelectedSpans[0]);
                Assert.Equal(
                    _textView.GetVirtualPointInLine(0, 1),
                    _textView.GetCaretVirtualPoint());
                Assert.Equal(
                    new SelectedSpan(_textView.GetVirtualPointInLine(1, 1)),
                    spans[1]);
            }
        }

        public sealed class AddCaretTest : MultiSelectionIntegrationTest
        {
            /// <summary>
            /// Test adding a caret with ctrl-alt-arrow
            /// </summary>
            [WpfFact]
            public void AddCaret()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                _vimBuffer.ProcessNotation("<C-A-Down>");
                var spans = SelectedSpans;
                Assert.Equal(2, spans.Length);
                Assert.Equal(GetPoint(0, 0).GetSelectedSpan(), spans[0]);
                Assert.Equal(GetPoint(1, 0).GetSelectedSpan(), spans[1]);
            }
        }

        public sealed class SelectModeTest : MultiSelectionIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.SelectModeOptions = SelectModeOptions.Mouse | SelectModeOptions.Keyboard;
                _globalSettings.KeyModelOptions = KeyModelOptions.StartSelection;
                _globalSettings.Selection = "exclusive";
            }

            /// <summary>
            /// Test entering select mode
            /// </summary>
            [WpfFact]
            public void Enter()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 0), GetPoint(1, 0));
                _vimBuffer.ProcessNotation("<S-Right>");
                AssertSelections(
                    GetPoint(0, 1).GetSelectedSpan(-1, 0),
                    GetPoint(1, 0).GetSelectedSpan(-1, 0));
            }

            /// <summary>
            /// Test extending the selection forward
            /// </summary>
            [WpfFact]
            public void ReplaceSelection()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                _vimBuffer.ProcessNotation("gh<C-S-Right>xxx ");
                AssertCarets(GetPoint(0, 8), GetPoint(1, 8));
                AssertLines("abc xxx ghi", "jkl xxx pqr", "");
            }

            /// <summary>
            /// Test extending the selection forward
            /// </summary>
            [WpfFact]
            public void ExtendForward()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 4), GetPoint(1, 4));
                _vimBuffer.ProcessNotation("gh<C-S-Right>");
                AssertSelections(
                    GetPoint(0, 8).GetSelectedSpan(-4, 0),
                    GetPoint(1, 8).GetSelectedSpan(-4, 0));
            }

            /// <summary>
            /// Test extending the selection backward
            /// </summary>
            [WpfFact]
            public void ExtendBackward()
            {
                Create("abc def ghi", "jkl mno pqr", "");
                SetCaretPoints(GetPoint(0, 8), GetPoint(1, 8));
                _vimBuffer.ProcessNotation("gh<C-S-Left>");
                AssertSelections(
                    GetPoint(0, 4).GetSelectedSpan(4, 0),
                    GetPoint(1, 4).GetSelectedSpan(4, 0));
            }
        }
    }
}
