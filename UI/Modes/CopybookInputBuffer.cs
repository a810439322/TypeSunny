using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.UI.Modes
{
    internal enum CopybookInputState
    {
        NoType,
        Right,
        Wrong
    }

    internal sealed class CopybookInputBuffer
    {
        private readonly List<string> _textElements = new List<string>();

        public int CaretIndex { get; private set; }

        public int Length
        {
            get { return _textElements.Count; }
        }

        public string Text
        {
            get { return string.Concat(_textElements); }
        }

        public string GetElement(int index)
        {
            return index >= 0 && index < _textElements.Count ? _textElements[index] : "";
        }

        public void Clear()
        {
            _textElements.Clear();
            CaretIndex = 0;
        }

        public void SetText(string text, int caretIndex)
        {
            _textElements.Clear();
            foreach (string element in EnumerateTextElements(text))
                _textElements.Add(element);
            MoveCaret(caretIndex);
        }

        public void MoveCaret(int caretIndex)
        {
            if (caretIndex < 0)
                CaretIndex = 0;
            else if (caretIndex > _textElements.Count)
                CaretIndex = _textElements.Count;
            else
                CaretIndex = caretIndex;
        }

        public int Insert(string text)
        {
            int inserted = 0;
            foreach (string element in EnumerateTextElements(text))
            {
                _textElements.Insert(CaretIndex, element);
                CaretIndex++;
                inserted++;
            }

            return inserted;
        }

        public bool Backspace()
        {
            if (CaretIndex <= 0)
                return false;

            _textElements.RemoveAt(CaretIndex - 1);
            CaretIndex--;
            return true;
        }

        public bool Delete()
        {
            if (CaretIndex < 0 || CaretIndex >= _textElements.Count)
                return false;

            _textElements.RemoveAt(CaretIndex);
            return true;
        }

        public CopybookInputState[] BuildStates(IReadOnlyList<string> targetWords, bool lookingType)
        {
            int count = targetWords == null ? 0 : targetWords.Count;
            var states = new CopybookInputState[count];

            for (int i = 0; i < count; i++)
            {
                if (i >= _textElements.Count)
                    states[i] = CopybookInputState.NoType;
                else if (lookingType || _textElements[i] == targetWords[i])
                    states[i] = CopybookInputState.Right;
                else
                    states[i] = CopybookInputState.Wrong;
            }

            return states;
        }

        private static IEnumerable<string> EnumerateTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
                yield return enumerator.GetTextElement();
        }
    }
}
