﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace disfr.Doc
{
    public class SdltmReader : IAssetReader
    {
        private static readonly string[] _FIlterString = { "Studio Translation Memory|*.sdltm" };

        public IList<string> FilterString {  get { return _FIlterString; } }

        public string Name { get { return "SdltmReader"; } }

        public int Priority { get { return 10; } }

        public IEnumerable<IAsset> Read(string filename, int filterindex)
        {
            using (var s = File.OpenRead(filename))
            {
                if (s.ReadByte() != 'S' || 
                    s.ReadByte() != 'Q' ||
                    s.ReadByte() != 'L' || 
                    s.ReadByte() != 'i' ||
                    s.ReadByte() != 't' || 
                    s.ReadByte() != 'e' ||
                    s.ReadByte() != ' ' || 
                    s.ReadByte() != 'f' ||
                    s.ReadByte() != 'o' || 
                    s.ReadByte() != 'r' ||
                    s.ReadByte() != 'm' || 
                    s.ReadByte() != 'a' ||
                    s.ReadByte() != 't' || 
                    s.ReadByte() != ' ' ||
                    s.ReadByte() != '3' || 
                    s.ReadByte() != '\0') return null;
            }

            IDbConnection connection = null;
            IDataReader reader = null;
            try
            {
                var b = new SQLiteConnectionStringBuilder()
                {
                    DataSource = filename,
                };
                try
                {
                    connection = new SQLiteConnection(b.ConnectionString);
                    connection.Open();
                }
                catch (Exception)
                {
                    return null;
                }

                var version = ExecScalar(connection, @"SELECT value FROM parameters WHERE name = 'VERSION'") as string;
                if (version?.StartsWith("8.") != true) return null;

                var tm_min = ExecScalarValue<int>(connection, @"SELECT min(id) FROM translation_memories");
                var tm_max = ExecScalarValue<int>(connection, @"SELECT max(id) FROM translation_memories");
                var tm_count = tm_max - tm_min + 1;
                if (tm_count <= 0 || tm_count > 1000) return null;

                var assets = new SdltmAsset[tm_count];

                reader = ExecReader(connection, @"SELECT id, name, source_language, target_language FROM translation_memories");
                while (reader.Read())
                {
                    var tmid = reader.GetInt32(0);
                    assets[tmid - tm_min] = new SdltmAsset()
                    {
                        Package = filename,
                        Original = reader.GetString(1),
                        SourceLang = reader.GetString(2),
                        TargetLang = reader.GetString(3),
                    };
                }
                reader.Close();
                reader.Dispose();
                reader = null;

                var pool = new StringPool();
                var matcher = new TagMatcher();

                reader = ExecReader(connection, @"SELECT translation_memory_id, id, source_segment, target_segment, creation_date, creation_user, change_date, change_user FROM translation_units");
                while (reader.Read())
                {
                    var tmid = reader.GetInt32(0);
                    var pair = new SdltmPair()
                    {
                        Id = reader.GetInt32(1).ToString(),
                        Source = GetInlineString(reader.GetString(2)),
                        Target = GetInlineString(reader.GetString(3)),
                        SourceLang = assets[tmid - tm_min].SourceLang,
                        TargetLang = assets[tmid - tm_min].TargetLang,
                        _CreationDate = pool.Intern(reader.GetString(4)),
                        _CreationUser = pool.Intern(reader.GetString(5)),
                        _ChangeDate = pool.Intern(reader.GetString(6)),
                        _ChangeUser = pool.Intern(reader.GetString(7)),
                    };
                    matcher.MatchTags(pair.Source, pair.Target, reader.GetString(2), reader.GetString(3));
                    assets[tmid - tm_min]._TransPairs.Add(pair);
                }
                reader.Close();
                reader.Dispose();
                reader = null;

                int serial = 0;
                foreach (var asset in assets)
                {
                    foreach (var pair in asset._TransPairs)
                    {
                        pair.Serial = ++serial;
                    }
                }

                return assets;
            }
            finally
            {
                reader?.Close();
                reader?.Dispose();
                connection?.Dispose();
            }
        }

        protected static object ExecScalar(IDbConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                var result = cmd.ExecuteScalar();
                if (result is DBNull) result = null;
                return result;
            }
        }

        protected static T ExecScalarValue<T>(IDbConnection connection, string sql)
        {
            return (T)Convert.ChangeType(ExecScalar(connection, sql), typeof(T));
        }

        protected static IDataReader ExecReader(IDbConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                return cmd.ExecuteReader();
            }
        }

        protected static InlineString GetInlineString(string text)
        {
            var inline = new InlineString();
            foreach (var elem in XElement.Parse(text).Elements("Elements").Elements())
            {
                switch (elem.Name.LocalName)
                {
                    case "Text":
                        inline.Add((string)elem.Element("Value") ?? "");
                        break;
                    case "Tag":
                        inline.Add(GetInlineTag(elem));
                        break;
                    default:
                        // Just in case...
                        inline.Add(new InlineTag(Tag.S, "*", "", elem.Name.LocalName, null, null, elem.ToString()));
                        break;
                }
            }
            return inline;
        }

        protected static InlineTag GetInlineTag(XElement tag)
        {
            Tag tagtype = Tag.S;
            switch ((string)tag.Element("Type"))
            {
                case "Start": tagtype = Tag.B; break;
                case "End": tagtype = Tag.E; break;
            }

            var id = (string)tag.Element("TagID") ?? "*";
            var rid = (string)tag.Element("Anchor") ?? "";
            var name = (string)tag.Element("Type") ?? "Tag";

            if (tagtype == Tag.E)
            {
                // Find the matching Start tag and use its TagID.
                var start = tag.ElementsBeforeSelf()
                    .LastOrDefault(e => (string)e.Element("Anchor") == rid && (string)e.Element("Type") == "Start" && e.Name.LocalName == "Tag");
                if (start != null)
                {
                    if (id == "*") id = (string)start.Element("TagID") ?? "*";
                }
            }

            string display = null;
            switch (tagtype)
            {
                case Tag.B: display = id + ">"; break;
                case Tag.E: display = "<" + id; break;
                case Tag.S: display = id; break;
            }

            return new InlineTag(tagtype, id, rid, name, null, display, null);
        }

        // This one got too much complicated and mixed-up,
        // as well as it is like repeating a same thing multiple times unnecessarily.
        // Definitely needs redesigning...
        protected class TagMatcher
        {
            public static readonly XName ELEM = "Elements";
            public static readonly XName TAG = "Tag";
            public static readonly XName ID = "TagID";
            public static readonly XName TYPE = "Type";
            public static readonly XName ANCH = "Anchor";
            public static readonly XName ALIGN = "AlignmentAnchor";

            public void MatchTags(InlineString source, InlineString target, string source_xml, string target_xml)
            {
                var st = source.OfType<InlineTag>().ToList();
                var tt = target.OfType<InlineTag>().ToList();

                var sx = XElement.Parse(source_xml).Elements(ELEM).Elements(TAG).ToList();
                var tx = XElement.Parse(target_xml).Elements(ELEM).Elements(TAG).ToList();

                for (int i = 0; i < st.Count; i++)
                {
                    st[i].Number = i + 1;
                }

                for (int j = 0; j < tt.Count; j++)
                {
                    int? index;
                    if (tt[j].TagType == Tag.E)
                    {
                        var anchor_in_tx = (string)tx[j].Element(ANCH);
                        var jj = Enumerable.Range(0, j).Select(x => (int?)x).LastOrDefault(x =>
                            (string)tx[(int)x].Element(ANCH) == anchor_in_tx && (string)tx[(int)x].Element(TYPE) == "Start");
                        var alignment = (jj == null) ? null : (string)tx[(int)jj].Element(ALIGN);
                        var id = (jj == null) ? null : (string)tx[(int)jj].Element(ID);
                        var ii = Enumerable.Range(0, sx.Count).Select(x => (int?)x).FirstOrDefault(x =>
                            (string)sx[(int)x].Element(ID) == id && (string)sx[(int)x].Element(ALIGN) == alignment && (string)sx[(int)x].Element(TYPE) == "Start");
                        var anchor_in_sx = (ii == null) ? null : (string)sx[(int)ii].Element(ANCH);
                        index = Enumerable.Range(0, sx.Count).Select(x => (int?)x).FirstOrDefault(x =>
                            (string)sx[(int)x].Element(ANCH) == anchor_in_sx && (string)sx[(int)x].Element(TYPE) == "End");
                    }
                    else
                    {
                        var type = (string)tx[j].Element(TYPE);
                        var id = (string)tx[j].Element(ID);
                        var alignment = (string)tx[j].Element(ALIGN);
                        index = Enumerable.Range(0, sx.Count).Select(x => (int?)x).FirstOrDefault(x =>
                            (string)sx[(int)x].Element(ID) == id && (string)sx[(int)x].Element(ALIGN) == alignment && (string)sx[(int)x].Element(TYPE) == type);
                    }
                    if (index != null) tt[j].Number = st[(int)index].Number;
                }
            }
        }
    }

    class SdltmAsset : IAsset
    {
        public string Package { get; internal set; }

        public string Original { get; internal set; }

        public string SourceLang { get; internal set; }

        public string TargetLang { get; internal set; }

        internal readonly List<SdltmPair> _TransPairs = new List<SdltmPair>();

        public IEnumerable<ITransPair> TransPairs { get { return _TransPairs; } }

        public IEnumerable<ITransPair> AltPairs { get { return Enumerable.Empty<ITransPair>(); } }
    }

    class SdltmPair : ITransPair
    {
        public int Serial { get; internal set; }

        public string Id { get; internal set; }

        public InlineString Source { get; internal set; }

        public InlineString Target { get; internal set; }

        public string SourceLang { get; internal set; }

        public string TargetLang { get; internal set; }

        public IEnumerable<string> Notes { get { return Enumerable.Empty<string>(); } }

        internal string _CreationDate, _CreationUser, _ChangeDate, _ChangeUser;

        public string this[string key]
        {
            get
            {
                switch (key)
                {
                    case "creation_date": return _CreationDate;
                    case "creation_user": return _CreationUser;
                    case "change_date": return _ChangeDate;
                    case "change_user": return _ChangeUser;
                    default: return null;
                }
            }
        }

        private readonly string[] _PropKeys = { "creation_date", "creation_user", "change_date", "change_user" };

        public IEnumerable<string> PropKeys { get { return _PropKeys; } }
    }
}
