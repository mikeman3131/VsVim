﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Vim.Modes;
using Moq;
using Vim;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;

namespace VimCoreTest
{
    [TestFixture]
    public class Modes_CommonOperationsTest
    {

        private class OperationsImpl : CommonOperations
        {
            internal OperationsImpl(ITextView view, IEditorOperations opts, IOutliningManager outlining, IVimHost host, IJumpList jumpList, IVimLocalSettings settings) : base(view, opts, outlining, host, jumpList, settings) { }
        }

        private IWpfTextView _view;
        private ITextBuffer _buffer;
        private Mock<IEditorOperations> _editorOpts;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IOutliningManager> _outlining;
        private ICommonOperations _operations;
        private CommonOperations _operationsRaw;

        public void CreateLines(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _buffer = _view.TextBuffer;
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _editorOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _settings = new Mock<IVimLocalSettings>(MockBehavior.Strict);
            _globalSettings = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _globalSettings.SetupGet(x => x.ShiftWidth).Returns(2);
            _settings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);

            _operationsRaw = new OperationsImpl(_view, _editorOpts.Object, _outlining.Object, _host.Object, _jumpList.Object,_settings.Object);
            _operations = _operationsRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _operations = null;
            _operationsRaw = null;
        }

        [Test]
        public void Join1()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Eat spaces at the start of the next line")]
        public void Join2()
        {
            CreateLines("foo", "   bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 2));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a count")]
        public void Join3()
        {
            CreateLines("foo", "bar", "baz");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 3));
            Assert.AreEqual("foo bar baz", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(8, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Join with a single count, should be no different")]
        public void Join4()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.RemoveEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Join5()
        {
            CreateLines("foo", "bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foobar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void Join6()
        {
            CreateLines("foo", " bar");
            Assert.IsTrue(_operations.Join(_view.GetCaretPoint(), JoinKind.KeepEmptySpaces, 1));
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetText());
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void GoToDefinition1()
        {
            CreateLines("foo");
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _host.Setup(x => x.GoToDefinition()).Returns(true);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
        }

        [Test]
        public void GoToDefinition2()
        {
            CreateLines("foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.IsTrue(((Result.Failed)res).Item.Contains("foo"));
        }

        [Test, Description("Make sure we don't crash when nothing is under the cursor")]
        public void GoToDefinition3()
        {
            CreateLines("      foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
        }

        [Test]
        public void GoToDefinition4()
        {
            CreateLines("  foo");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefNoWordUnderCursor, res.AsFailed().Item);
        }

        [Test]
        public void GoToDefinition5()
        {
            CreateLines("foo bar baz");
            _host.Setup(x => x.GoToDefinition()).Returns(false);
            var res = _operations.GoToDefinition();
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_GotoDefFailed("foo"), res.AsFailed().Item);
        }

        [Test]
        public void SetMark1()
        {
            CreateLines("foo");
            var map = new MarkMap(new TrackingLineColumnService());
            var vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            vimBuffer.SetupGet(x => x.MarkMap).Returns(map);
            vimBuffer.SetupGet(x => x.TextBuffer).Returns(_buffer);
            var res = _operations.SetMark(vimBuffer.Object, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, 'a');
            Assert.IsTrue(res.IsSucceeded);
            Assert.IsTrue(map.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test, Description("Invalid mark character")]
        public void SetMark2()
        {
            CreateLines("bar"); 
            var map = new MarkMap(new TrackingLineColumnService());
            var vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            vimBuffer.SetupGet(x => x.MarkMap).Returns(map);
            var res = _operations.SetMark(vimBuffer.Object, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start, ';');
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test]
        public void JumpToMark1()
        {
            CreateLines("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetLocalMark(new SnapshotPoint(_view.TextSnapshot, 0), 'a');
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_view.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsed>>(null)
                .Verifiable();
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            var res = _operations.JumpToMark('a', map);
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
            _outlining.Verify();
        }

        [Test]
        public void JumpToMark2()
        {
            CreateLines("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            var res = _operations.JumpToMark('b', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test, Description("Jump to global mark")]
        public void JumpToMark3()
        {
            CreateLines("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetMark(new SnapshotPoint(_view.TextSnapshot, 0), 'A');
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(_view.TextSnapshot,0))).Returns(true);
            _jumpList.Setup(x => x.Add(_view.GetCaretPoint())).Verifiable();
            _outlining
                .Setup(x => x.ExpandAll(new SnapshotSpan(_view.TextSnapshot, 0, 0), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsed>>(null)
                .Verifiable();
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsSucceeded);
            _jumpList.Verify();
            _outlining.Verify();
        }

        [Test, Description("Jump to global mark and jump fails")]
        public void JumpToMark4()
        {
            CreateLines();
            var view = EditorUtil.CreateView("foo", "bar");
            var map = new MarkMap(new TrackingLineColumnService());
            map.SetMark(new SnapshotPoint(view.TextSnapshot, 0), 'A');
            _host.Setup(x => x.NavigateTo(new VirtualSnapshotPoint(view.TextSnapshot,0))).Returns(false);
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkInvalid, res.AsFailed().Item);
        }

        [Test, Description("Jump to global mark that does not exist")]
        public void JumpToMark5()
        {
            CreateLines("foo", "bar");
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            buffer.SetupGet(x => x.TextBuffer).Returns(_view.TextBuffer);
            buffer.SetupGet(x => x.Name).Returns("foo");
            var map = new MarkMap(new TrackingLineColumnService());
            var res = _operations.JumpToMark('A', map);
            Assert.IsTrue(res.IsFailed);
            Assert.AreEqual(Resources.Common_MarkNotSet, res.AsFailed().Item);
        }

        [Test]
        public void PasteAfter1()
        {
            CreateLines("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.LineWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yaybar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter2()
        {
            CreateLines("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.CharacterWise).Snapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("fyayoo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void PasteAfter3()
        {
            CreateLines("foo", "bar");
            var tss = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual(3, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("yay", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(2).GetText());
        }

        [Test]
        public void PasteAfter4()
        {
            CreateLines("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test]
        public void PasteAfter5()
        {
            CreateLines("foo", "bar");
            var span = _operations.PasteAfter(new SnapshotPoint(_view.TextSnapshot, 0), "yay", OperationKind.LineWise);
            Assert.AreEqual("yay", span.GetText());
        }

        [Test, Description("Character wise paste at the end of the line should go on that line")]
        public void PasteAfter6()
        {
            CreateLines("foo", "bar");
            var buffer = _view.TextBuffer;
            var point = buffer.CurrentSnapshot.GetLineFromLineNumber(0).End;
            _operations.PasteAfter(point, "yay", OperationKind.CharacterWise);
            Assert.AreEqual("fooyay", buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Line wise paste at the end of the file should add a new line")]
        public void PasteAfter7()
        {
            CreateLines("foo", "bar");
            var point = _buffer.GetLineSpan(1).Start;
            _operations.PasteAfter(point, "foo", OperationKind.LineWise);
            Assert.AreEqual(3, _buffer.CurrentSnapshot.LineCount);
            Assert.AreEqual("foo", _buffer.GetLineSpan(2).GetText());
        }

        [Test]
        public void PasteBefore1()
        {
            CreateLines("foo", "bar");
            var buffer = _view.TextBuffer;
            var span = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay", OperationKind.CharacterWise);
            Assert.AreEqual("yay", span.GetText());
            Assert.AreEqual("yayfoo", span.Snapshot.GetLineFromLineNumber(0).GetText());
        }


        [Test]
        public void PasteBefore2()
        {
            CreateLines("foo", "bar");
            var buffer = _view.TextBuffer;
            var snapshot = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 0), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual("yay", snapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", snapshot.GetLineFromLineNumber(1).GetText());
        }


        [Test]
        public void PasteBefore3()
        {
            CreateLines("foo", "bar");
            var buffer = _view.TextBuffer;
            var snapshot = _operations.PasteBefore(new SnapshotPoint(buffer.CurrentSnapshot, 3), "yay" + Environment.NewLine, OperationKind.LineWise).Snapshot;
            Assert.AreEqual("yay", snapshot.GetLineFromLineNumber(0).Extent.GetText());
            Assert.AreEqual("foo", snapshot.GetLineFromLineNumber(1).Extent.GetText());
        }


        [Test]
        public void MoveCaretRight1()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretRight2()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _operations.MoveCaretRight(2);
            Assert.AreEqual(2, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Don't move past the end of the line")]
        public void MoveCaretRight3()
        {
            CreateLines("foo", "bar");
            var tss = _view.TextSnapshot;
            var endPoint = tss.GetLineFromLineNumber(0).End;
            _view.Caret.MoveTo(endPoint);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(endPoint, _view.Caret.Position.BufferPosition);
        }

        [Test, Description("Don't crash going off the buffer")]
        public void MoveCaretRight4()
        {
            CreateLines("foo", "bar");
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.End);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretRight(1);
            Assert.AreEqual(last.End, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Don't go off the end of the current line")]
        public void MoveCaretRight5()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _editorOpts.Setup(x => x.ResetSelection());
            _view.Caret.MoveTo(line.End.Subtract(1));
            _operations.MoveCaretRight(1);
            Assert.AreEqual(line.End.Subtract(1), _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("If already past the line, MoveCaretRight should not move the caret at all")]
        public void MoveCaretRight6()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _view.Caret.MoveTo(_view.GetLine(0).End);
            _operations.MoveCaretRight(1);
            Assert.AreEqual(_view.GetLine(0).End, _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretLeft1()
        {
            CreateLines("foo", "bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test, Description("Move left on the start of the line should not go anywhere")]
        public void MoveCaretLeft2()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretLeft3()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start.Add(1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Left at the start of the line should not go further")]
        public void MoveCaretLeft4()
        {
            CreateLines("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretLeft(1);
            Assert.AreEqual(line.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp1()
        {
            CreateLines("foo", "bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret up past the begining of the buffer should fail if it's already at the top")]
        public void MoveCaretUp2()
        {
            CreateLines("foo", "bar", "baz");
            var first = _view.TextSnapshot.Lines.First();
            _view.Caret.MoveTo(first.End);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretUp(1);
            Assert.AreEqual(first.End, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret up should respect column positions")]
        public void MoveCaretUp3()
        {
            CreateLines("foo", "bar");
            var tss = _view.TextSnapshot;
            _view.Caret.MoveTo(tss.GetLineFromLineNumber(1).Start.Add(1));
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Verifiable();
            _operations.MoveCaretUp(1);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp4()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(1);
            Assert.AreEqual(1, count);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretUp5()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(3).Start);
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineUp(false)).Callback(() => { count++; }).Verifiable();
            _operations.MoveCaretUp(2);
            Assert.AreEqual(2, count);
            _editorOpts.Verify();
        }

        [Test, Description("At end of line should wrap to the start of the next line if there is a word")]
        public void MoveWordForward1()
        {
            CreateLines(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
            var line1 = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line1.End);
            _operations.MoveWordForward(WordKind.NormalWord,1);
            var line2 = _view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line2.Start, _view.Caret.Position.BufferPosition);
        }

        [Test]
        public void MoveWordForward2()
        {
            CreateLines(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveWordForward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void MoveWordBackword1()
        {
            CreateLines("foo bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _view.Caret.MoveTo(line.End);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("At the the start of a word move back to the start of the previous wodr")]
        public void MoveWordBackward2()
        {
            CreateLines("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 4));
            Assert.AreEqual('b', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Middle of word should move back to front")]
        public void MoveWordBackard3()
        {
            CreateLines("foo bar");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 5));
            Assert.AreEqual('a', _view.Caret.Position.BufferPosition.GetChar());
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Move backwards across lines")]
        public void MoveWordBackward4()
        {
            CreateLines("foo bar", "baz");
            var line = _view.TextSnapshot.GetLineFromLineNumber(1);
            _view.Caret.MoveTo(line.Start);
            _operations.MoveWordBackward(WordKind.NormalWord,1);
            Assert.AreEqual(4, _view.Caret.Position.BufferPosition.Position);
        }


        [Test]
        public void MoveCaretDown1()
        {
            CreateLines("foo", "bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should fail if the caret is at the end of the buffer")]
        public void MoveCaretDown2()
        {
            CreateLines("bar", "baz", "aeu");
            var last = _view.TextSnapshot.Lines.Last();
            _view.Caret.MoveTo(last.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDown(1);
            Assert.AreEqual(last.Start, _view.Caret.Position.BufferPosition);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown3()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 2);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test, Description("Move caret down should not crash if the line is the second to last line.  In other words, the last real line")]
        public void MoveCaretDown4()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var line = tss.GetLineFromLineNumber(tss.LineCount - 1);
            _view.Caret.MoveTo(line.Start);
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDown(1);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDown5()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(1);
            Assert.AreEqual(1, count);
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDown6()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var count = 0;
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _editorOpts
                .Setup(x => x.MoveLineDown(false))
                .Callback(() => { count++; })
                .Verifiable();
            _operations.MoveCaretDown(2);
            Assert.AreEqual(2, count);
            _editorOpts.Verify();
        }

        [Test]
        public void DeleteSpan1()
        {
            CreateLines("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                reg);
            var tss = _view.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
        }

        [Test]
        public void DeleteSpan2()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var reg = new Register('c');
            _operations.DeleteSpan(span, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            tss = _view.TextSnapshot;
            Assert.AreEqual(1, tss.LineCount);
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(span.GetText(), reg.StringValue);
        }

        [Test]
        public void DeleteSpan3()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteSpan(tss.GetLineFromLineNumber(1).ExtentIncludingLineBreak, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("baz", tss.GetLineFromLineNumber(1).GetText());
        }


        [Test]
        public void Yank1()
        {
            CreateLines("foo", "bar");
            var reg = new Register('c');
            _operations.Yank(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
        }

        [Test]
        public void Yank2()
        {
            CreateLines("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var reg = new Register('c');
            _operations.Yank(span, MotionKind._unique_Exclusive, OperationKind.LineWise, reg);
            Assert.AreEqual(span.GetText(), reg.StringValue);
        }

        [Test]
        public void ShiftSpanRight1()
        {
            CreateLines("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanRight(span);
            Assert.AreEqual("  foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Only shift whitespace")]
        public void ShiftSpanLeft1()
        {
            CreateLines("foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(span);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Don't puke on an empty line")]
        public void ShiftSpanLeft2()
        {
            CreateLines("");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(span);
            Assert.AreEqual("", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftSpanLeft3()
        {
            CreateLines("  foo", "  bar");
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Start,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).End);
            _operations.ShiftSpanLeft(span);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar", _buffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ShiftSpanLeft4()
        {
            CreateLines("   foo");
            var span = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).Extent;
            _operations.ShiftSpanLeft(span);
            Assert.AreEqual(" foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft1()
        {
            CreateLines("   foo");
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft2()
        {
            CreateLines(" foo");
            _operations.ShiftLinesLeft(400);
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesLeft3()
        {
            CreateLines("   foo", "    bar");
            _operations.ShiftLinesLeft(2);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("  bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesLeft4()
        {
            CreateLines(" foo", "   bar");
            _view.MoveCaretTo(_buffer.GetLineSpan(1).Start.Position);
            _operations.ShiftLinesLeft(1);
            Assert.AreEqual(" foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual(" bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesRight1()
        {
            CreateLines("foo");
            _operations.ShiftLinesRight(1);
            Assert.AreEqual("  foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ShiftLinesRight2()
        {
            CreateLines("foo", " bar");
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("  foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("   bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ShiftLinesRight3()
        {
            CreateLines("foo", " bar");
            _view.MoveCaretTo(_buffer.GetLineSpan(1).Start.Position);
            _operations.ShiftLinesRight(2);
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("   bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ScrollLines1()
        {
            CreateLines("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test, Description("Don't break at line 0")]
        public void ScrollLines2()
        {
            CreateLines("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Up, 1);
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollLines3()
        {
            CreateLines("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).End);
            _editorOpts.Setup(x => x.ResetSelection());
            _settings.SetupGet(x => x.Scroll).Returns(42).Verifiable();
            _operations.ScrollLines(ScrollDirection.Down, 1);
            Assert.AreEqual(1, _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber);
            _settings.Verify();
        }

        [Test]
        public void ScrollPages1()
        {
            CreateLines("");
            _editorOpts.Setup(x => x.ScrollPageUp()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Up, 1);
            _editorOpts.Verify();
        }

        [Test]
        public void ScrollPages2()
        {
            CreateLines("");
            var count = 0;
            _editorOpts.Setup(x => x.ScrollPageUp()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Up, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ScrollPages3()
        {
            CreateLines("");
            _editorOpts.Setup(x => x.ScrollPageDown()).Verifiable();
            _operations.ScrollPages(ScrollDirection.Down, 1);
            _editorOpts.Verify();
        }

        [Test]
        public void ScrollPages4()
        {
            CreateLines("");
            var count = 0;
            _editorOpts.Setup(x => x.ScrollPageDown()).Callback(() => { count++; });
            _operations.ScrollPages(ScrollDirection.Down, 2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void DeleteLines1()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLines(1, reg);
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(4, _view.TextSnapshot.LineCount);
        }

        [Test, Description("Caret position should not affect this operation")]
        public void DeleteLines2()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLines(1, reg);
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(4, _view.TextSnapshot.LineCount);
        }

        [Test, Description("Delete past the end of the buffer should not crash")]
        public void DeleteLines3()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLines(3000, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor1()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(1, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor2()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(2, reg);
            Assert.AreEqual(String.Empty, _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine + "bar", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesFromCursor3()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var tss = _view.TextSnapshot;
            _view.MoveCaretTo(1);
            var reg = new Register('c');
            _operations.DeleteLinesFromCursor(2, reg);
            Assert.AreEqual("f", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("oo" + Environment.NewLine + "bar", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak1()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLinesIncludingLineBreak(1, reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(3, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak2()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _operations.DeleteLinesIncludingLineBreak(2, reg);
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual(2, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor1()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _view.MoveCaretTo(1);
            _operations.DeleteLinesIncludingLineBreakFromCursor(1, reg);
            Assert.AreEqual("oo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("fbar", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("baz", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(3, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void DeleteLinesIncludingLineBreakFromCursor2()
        {
            CreateLines("foo", "bar", "baz", "jaz");
            var reg = new Register('c');
            _view.MoveCaretTo(1);
            _operations.DeleteLinesIncludingLineBreakFromCursor(2, reg);
            Assert.AreEqual("oo" + Environment.NewLine + "bar" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual("fbaz", _view.TextSnapshot.GetLineSpan(0).GetText());
            Assert.AreEqual("jaz", _view.TextSnapshot.GetLineSpan(1).GetText());
            Assert.AreEqual(2, _view.TextSnapshot.LineCount);
        }

        [Test]
        public void MoveCaretDownToFirstNonWhitespaceCharacter1()
        {
            CreateLines("foo", "bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDownToFirstNonWhitespaceCharacter(1);
            Assert.AreEqual(_view.GetLine(1).Start, _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDownToFirstNonWhitespaceCharacter2()
        {
            CreateLines("foo", " bar");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDownToFirstNonWhitespaceCharacter(1);
            Assert.AreEqual(_view.GetLine(1).Start.Add(1), _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDownToFirstNonWhitespaceCharacter3()
        {
            CreateLines("foo", "  bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDownToFirstNonWhitespaceCharacter(1);
            Assert.AreEqual(_view.GetLine(1).Start.Add(2), _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDownToFirstNonWhitespaceCharacter4()
        {
            CreateLines("foo", "  bar", " baz");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDownToFirstNonWhitespaceCharacter(2);
            Assert.AreEqual(_view.GetLine(2).Start.Add(1), _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void MoveCaretDownToFirstNonWhitespaceCharacter5()
        {
            CreateLines("foo", "  bar", "baz");
            _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
            _operations.MoveCaretDownToFirstNonWhitespaceCharacter(300);
            Assert.AreEqual(_view.GetLine(2).Start, _view.GetCaretPoint());
            _editorOpts.Verify();
        }

        [Test]
        public void ChangeLetterCase1()
        {
            CreateLines("foo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ChangeLetterCase2()
        {
            CreateLines("fOo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FoO", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void ChangeLetterCase3()
        {
            CreateLines("fOo", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0,1));
            Assert.AreEqual("FoO", _buffer.GetLineSpan(0).GetText());
            Assert.AreEqual("BAR", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void ChangeLetterCase4()
        {
            CreateLines("f12o", "bar");
            _operations.ChangeLetterCase(_buffer.GetLineSpan(0));
            Assert.AreEqual("F12O", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersLowercase1()
        {
            CreateLines("FOO", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("foo", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersLowercase2()
        {
            CreateLines("FOO", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(1));
            Assert.AreEqual("bar", _buffer.GetLineSpan(1).GetText());
        }

        [Test]
        public void MakeLettersLowercase3()
        {
            CreateLines("FoO123", "BAR");
            _operations.MakeLettersLowercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("foo123", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersUppercase1()
        {
            CreateLines("foo123", "bar");
            _operations.MakeLettersUppercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO123", _buffer.GetLineSpan(0).GetText());
        }

        [Test]
        public void MakeLettersUppercase2()
        {
            CreateLines("fOo123", "bar");
            _operations.MakeLettersUppercase(_buffer.GetLineSpan(0));
            Assert.AreEqual("FOO123", _buffer.GetLineSpan(0).GetText());
        }
    }
}
