﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using disfr.Doc;

namespace disfr.UI
{
    public interface IRowData
    {
        bool Hidden { get; }

        int Seq { get; }

        int Serial { get; }

        string Asset { get; }

        string Id { get; }

        GlossyString Source { get; }

        GlossyString Target { get; }

        int Serial2 { get; }

        string Asset2 { get; }

        string Id2 { get; }

        GlossyString Target2 { get; }

        string Notes { get; }

        string this[string key] { get; }

        IEnumerable<string> Keys { get; }

        InlineString RawSource { get; }

        InlineString RawTarget { get; }

        InlineString RawTarget2 { get; }

        string SourceLang { get; }

        string TargetLang { get; }
    }
}
