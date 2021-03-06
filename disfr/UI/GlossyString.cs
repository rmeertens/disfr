﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace disfr.UI
{
    [Flags]
    public enum Gloss
    {
        None = 0,

        COM = 0,
        INS = 1,
        DEL = 2,

        NOR = 0,
        TAG = 4,
        SYM = 8,

        HIT = 16,
    }

    public class GlossyString : System.Collections.IEnumerable
    {
        public struct Pair
        {
            public string Text;
            public Gloss Gloss;

            public Pair(string text, Gloss gloss) { Text = text; Gloss = gloss; }
        }

        /// <summary>
        /// Create an empty unfrozen instance.
        /// </summary>
        public GlossyString()
        {
            Pairs = new List<Pair>();
            _Frozen = false;
        }

        /// <summary>
        /// Create a frozen instance equivalent to a single string.
        /// </summary>
        /// <param name="text"></param>
        public GlossyString(string text) : this(text, Gloss.None) {}

        /// <summary>
        /// Create a frozen instance of a single string in a uniform gloss.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="gloss"></param>
        public GlossyString(string text, Gloss gloss)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (text.Length == 0)
            {
                Pairs = new Pair[0];
            }
            else
            {
                Pairs = new Pair[] { new Pair(text, gloss) };
            }
            _Frozen = true;
        }

        /// <summary>
        /// Create a frozen copy of a GlossyString.
        /// </summary>
        /// <param name="original"></param>
        public GlossyString(GlossyString original)
        {
            Pairs = original.Pairs.ToArray();
            _Frozen = true;
        }

        public static readonly GlossyString Empty = new GlossyString("");

        private readonly IList<Pair> Pairs;

        private bool _Frozen;

        public bool Frozen
        {
            get { return _Frozen; }
            set
            {
                if (value == _Frozen) return;
                if (value == false) throw new InvalidOperationException("Unfreezing a frozen GlossyString.");
                _Frozen = true;
            }
        }

        private int LastAdded = -1;

        public GlossyString Append(string text) { return Append(text, Gloss.None); }

        public GlossyString Append(string text, Gloss gloss)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (_Frozen) throw new InvalidOperationException("Appending to a frozen GlossyString.");

            LastAdded = text.Length;
            if (text.Length > 0)
            {
                if (Pairs.Count > 0 && Pairs[Pairs.Count - 1].Gloss == gloss)
                {
                    Pairs[Pairs.Count - 1] = new Pair(Pairs[Pairs.Count - 1].Text + text, gloss);
                }
                else
                {
                    Pairs.Add(new Pair(text, gloss));
                }
            }
            return this;
        }

        /// <summary>
        /// <i>Add</i> a bool value to <see cref="Frozen"/> property.
        /// This method is intended for use with Collection Initializers.
        /// </summary>
        /// <param name="frozen"></param>
        public void Add(bool frozen)
        {
            if (_Frozen) throw new InvalidOperationException("Adding Frozen flag to a frozen GlossyString.");
            Frozen = frozen;
        }

        /// <summary>
        /// Add a string segment without any <see cref="Gloss"/>.
        /// </summary>
        /// <param name="text"></param>
        public void Add(string text) { Append(text, Gloss.None); }

        /// <summary>
        /// Change the <see cref="Gloss"/> for the last added segment.
        /// This method is intended for use with Collection Initializers. 
        /// </summary>
        /// <param name="gloss"></param>
        public void Add(Gloss gloss)
        {
            if (_Frozen) throw new InvalidOperationException("Modifying gloss of a frozen GlossyString.");
            if (LastAdded < 0) throw new InvalidOperationException("Modifying gloss of a GlossyString without a valid segment.");

            var last = LastAdded;
            LastAdded = -1;
            if (last == 0) return;

            var p = Pairs[Pairs.Count - 1];
            if (p.Text.Length == last)
            {
                Pairs[Pairs.Count - 1] = new Pair(p.Text, gloss);
            }
            else
            {
                Pairs[Pairs.Count - 1] = new Pair(p.Text.Substring(0, p.Text.Length - last), p.Gloss);
                Pairs.Add(new Pair(p.Text.Substring(p.Text.Length - last), gloss));
            }
        }

        /// <summary>
        /// Get String (Text) portion of this GlossyString.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Pairs.Count == 0) return "";
            if (Pairs.Count == 1) return Pairs[0].Text;

            var sb = new StringBuilder();
            foreach (var p in Pairs)
            {
                sb.Append(p.Text);
            }
            return sb.ToString();
        }

        public IEnumerator GetEnumerator()
        {
            return Pairs.GetEnumerator();
        }

        /// <summary>
        /// Enumerate over all Text-Gloss pairs.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Currently this method returns GlossyString's internal object directly,
        /// so it could be dangerous if the return value is casted and abused.
        /// Consider wrapping if provided for general public.
        /// </remarks>
        public ICollection<Pair> AsCollection()
        {
            return Pairs;
        }

        /// <summary>
        /// Get a frozen version of this GlossyString.
        /// </summary>
        /// <returns>This object if it is already frozen, or a new frozen instance otherwise.</returns>
        public GlossyString ToFrozen()
        {
            return this.Frozen ? this : new GlossyString(this);
        }

        public static bool IsNullOrEmpty(GlossyString glossy)
        {
            return glossy == null || glossy.Pairs.Count == 0;
        }
    }
}
