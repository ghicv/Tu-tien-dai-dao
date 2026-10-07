using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using ThienDao.World;

namespace ThienDao.Sim
{
    // Lưu / tải thế giới: a snapshot of the whole simulation object graph.
    //
    // Loading does not replay the command log (a thousand years would take minutes); instead a fresh Simulation
    // is built from the same seed — which wires every system's event subscriptions — and the saved state is
    // poured into it in place. Object identity is kept (two lists holding the same cultivator still do after a
    // load), delegates are skipped (the fresh objects already carry the right ones), and arrays of plain data are
    // copied as raw bytes. The file carries a schema table, so a save from code with different fields is refused
    // instead of loading garbage.
    public static class SaveGame
    {
        const string Magic = "TDSAVE";
        const int Version = 1;

        public sealed class Header
        {
            public string Seed;
            public long Tick;
            public ulong StateHash;
            public long SavedAtUtcTicks;
            public string Label;
            public int Year => (int)(Tick / Core.SimClock.DaysPerYear) + 1;
        }

        // ---------------------------------------------------------------- public API

        public static void Save(Simulation sim, string seed, string label, Stream output)
        {
            using var gz = new GZipStream(output, CompressionLevel.Fastest, true);
            using var w = new BinaryWriter(gz, Encoding.UTF8, true);
            w.Write(Magic);
            w.Write(Version);
            WriteHeader(w, new Header
            {
                Seed = seed, Tick = sim.Clock.Tick, StateHash = sim.ComputeStateHash(), SavedAtUtcTicks = DateTime.UtcNow.Ticks, Label = label ?? ""
            });
            var g = new GraphWriter(w);
            g.WriteObjectGraph(sim);
        }

        public static Header ReadHeader(Stream input)
        {
            using var gz = new GZipStream(input, CompressionMode.Decompress, true);
            using var r = new BinaryReader(gz, Encoding.UTF8, true);
            return ReadHeaderOnly(r);
        }

        // Builds a new simulation from the header's seed, then restores everything else from the file.
        public static Simulation Load(Stream input, out Header header)
        {
            using var gz = new GZipStream(input, CompressionMode.Decompress, true);
            using var r = new BinaryReader(gz, Encoding.UTF8, true);
            header = ReadHeaderOnly(r);
            var sim = new Simulation(MapGenerator.Generate(header.Seed));
            new GraphReader(r).ReadObjectGraph(sim);
            return sim;
        }

        public static byte[] SaveToBytes(Simulation sim, string seed, string label = null)
        {
            using var ms = new MemoryStream();
            Save(sim, seed, label, ms);
            return ms.ToArray();
        }

        public static Simulation LoadFromBytes(byte[] data, out Header header)
        {
            using var ms = new MemoryStream(data);
            return Load(ms, out header);
        }

        static void WriteHeader(BinaryWriter w, Header h)
        {
            w.Write(h.Seed);
            w.Write(h.Tick);
            w.Write(h.StateHash);
            w.Write(h.SavedAtUtcTicks);
            w.Write(h.Label);
        }

        static Header ReadHeaderOnly(BinaryReader r)
        {
            if (r.ReadString() != Magic) throw new InvalidDataException("Không phải tệp lưu Thiên Đạo.");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Tệp lưu phiên bản {version}, game cần {Version}.");
            return new Header { Seed = r.ReadString(), Tick = r.ReadInt64(), StateHash = r.ReadUInt64(), SavedAtUtcTicks = r.ReadInt64(), Label = r.ReadString() };
        }

        // ---------------------------------------------------------------- type model

        enum Kind : byte { Primitive, Enum, String, Blittable, Struct, Array, List, Dictionary, HashSet, Queue, Stack, Class, Skip }

        sealed class TypeInfo
        {
            public Type Type;
            public Kind Kind;
            public FieldInfo[] Fields;   // Struct / Class
            public Type Element, Key, Value;
        }

        static readonly Dictionary<Type, TypeInfo> Types = new Dictionary<Type, TypeInfo>();

        static TypeInfo Info(Type t)
        {
            lock (Types)
            {
                if (Types.TryGetValue(t, out var info)) return info;
                info = new TypeInfo { Type = t };
                Types[t] = info; // before recursing: types can refer to themselves
                if (typeof(Delegate).IsAssignableFrom(t)) info.Kind = Kind.Skip;
                else if (t.IsPrimitive) info.Kind = Kind.Primitive;
                else if (t.IsEnum) info.Kind = Kind.Enum;
                else if (t == typeof(string)) info.Kind = Kind.String;
                else if (t.IsArray)
                {
                    if (t.GetArrayRank() != 1) throw new NotSupportedException($"Mảng nhiều chiều chưa hỗ trợ: {t}");
                    info.Kind = Kind.Array;
                    info.Element = t.GetElementType();
                }
                else if (t.IsGenericType && Collection(t.GetGenericTypeDefinition(), out var kind))
                {
                    info.Kind = kind;
                    var args = t.GetGenericArguments();
                    info.Element = args[0];
                    info.Key = args[0];
                    if (args.Length > 1) info.Value = args[1];
                }
                else if (t.IsValueType)
                {
                    info.Fields = FieldsOf(t);
                    info.Kind = IsBlittable(t) ? Kind.Blittable : Kind.Struct;
                }
                else
                {
                    if (t.Assembly == typeof(object).Assembly)
                        throw new NotSupportedException($"Kiểu hệ thống chưa hỗ trợ khi lưu: {t.FullName}");
                    info.Kind = Kind.Class;
                    info.Fields = FieldsOf(t);
                }
                return info;
            }
        }

        static bool Collection(Type def, out Kind kind)
        {
            kind = def == typeof(List<>) ? Kind.List : def == typeof(Dictionary<,>) ? Kind.Dictionary : def == typeof(HashSet<>) ? Kind.HashSet :
                def == typeof(Queue<>) ? Kind.Queue : def == typeof(Stack<>) ? Kind.Stack : Kind.Skip;
            return kind != Kind.Skip;
        }

        // Every instance field up the hierarchy, in a fixed order; delegates and [NonSerialized] excluded.
        static FieldInfo[] FieldsOf(Type t)
        {
            var list = new List<FieldInfo>();
            for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType)
                foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized || typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    list.Add(f);
                }
            list.Sort((a, b) => a.DeclaringType == b.DeclaringType ? string.CompareOrdinal(a.Name, b.Name) : string.CompareOrdinal(a.DeclaringType.FullName, b.DeclaringType.FullName));
            return list.ToArray();
        }

        static readonly MethodInfo IsRefOrContainsRef = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences));

        static bool IsBlittable(Type t) => !(bool)IsRefOrContainsRef.MakeGenericMethod(t).Invoke(null, null) && !t.IsAutoLayout;

        // A short fingerprint of a type's fields, so a save from different code is refused instead of misread.
        static string Schema(TypeInfo info)
        {
            if (info.Fields == null) return info.Type.FullName;
            var sb = new StringBuilder(info.Type.FullName);
            foreach (var f in info.Fields) sb.Append('|').Append(f.Name).Append(':').Append(f.FieldType.Name);
            return sb.ToString();
        }

        static string TypeName(Type t) => t.AssemblyQualifiedName;

        // ---------------------------------------------------------------- raw blocks

        static readonly MethodInfo WriteRawMethod = typeof(SaveGame).GetMethod(nameof(WriteRaw), BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo ReadRawMethod = typeof(SaveGame).GetMethod(nameof(ReadRaw), BindingFlags.NonPublic | BindingFlags.Static);

        static void WriteRaw<T>(BinaryWriter w, T[] a) where T : unmanaged => w.Write(MemoryMarshal.AsBytes(a.AsSpan()));

        static void ReadRaw<T>(BinaryReader r, T[] a) where T : unmanaged
        {
            var bytes = MemoryMarshal.AsBytes(a.AsSpan());
            int read = 0;
            while (read < bytes.Length)
            {
                int n = r.Read(bytes.Slice(read));
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
        }

        // ---------------------------------------------------------------- writing

        sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }

        sealed class GraphWriter
        {
            readonly BinaryWriter _w;
            readonly Dictionary<object, int> _ids = new Dictionary<object, int>(new RefEq());
            readonly Dictionary<Type, int> _typeIds = new Dictionary<Type, int>();
            readonly MemoryStream _body = new MemoryStream();
            readonly BinaryWriter _b;

            public GraphWriter(BinaryWriter w)
            {
                _w = w;
                _b = new BinaryWriter(_body, Encoding.UTF8, true);
            }

            // The type table (with schemas) goes before the body, so the reader can check it first.
            public void WriteObjectGraph(object root)
            {
                Write(_b, root.GetType(), root);
                _b.Flush();
                var types = new Type[_typeIds.Count];
                foreach (var kv in _typeIds) types[kv.Value] = kv.Key;
                _w.Write(types.Length);
                foreach (var t in types)
                {
                    _w.Write(TypeName(t));
                    _w.Write(Schema(Info(t)));
                }
                _w.Write(_body.Length);
                _body.Position = 0;
                _body.CopyTo(_w.BaseStream);
            }

            int TypeId(Type t)
            {
                if (!_typeIds.TryGetValue(t, out int id)) _typeIds[t] = id = _typeIds.Count;
                return id;
            }

            void Write(BinaryWriter w, Type declared, object v)
            {
                var info = Info(declared);
                switch (info.Kind)
                {
                    case Kind.Skip: return;
                    case Kind.Primitive: WritePrimitive(w, declared, v); return;
                    case Kind.Enum: WritePrimitive(w, Enum.GetUnderlyingType(declared), Convert.ChangeType(v, Enum.GetUnderlyingType(declared))); return;
                    case Kind.Blittable: WriteStructFields(w, info, v); return;
                    case Kind.Struct: WriteStructFields(w, info, v); return;
                }
                // Reference types: null, back-reference, or a new object tagged with its runtime type.
                if (v == null) { w.Write((byte)0); return; }
                if (_ids.TryGetValue(v, out int existing)) { w.Write((byte)2); w.Write(existing); return; }
                var runtime = v.GetType();
                var rinfo = Info(runtime);
                if (rinfo.Kind == Kind.Skip) { w.Write((byte)0); return; }
                w.Write((byte)1);
                w.Write(TypeId(runtime));
                _ids[v] = _ids.Count;
                WriteBody(w, rinfo, v);
            }

            void WriteBody(BinaryWriter w, TypeInfo info, object v)
            {
                switch (info.Kind)
                {
                    case Kind.String: w.Write((string)v); return;
                    case Kind.Array:
                    {
                        var a = (Array)v;
                        w.Write(a.Length);
                        var e = Info(info.Element);
                        if (e.Kind == Kind.Primitive || e.Kind == Kind.Enum || e.Kind == Kind.Blittable)
                            WriteRawMethod.MakeGenericMethod(info.Element).Invoke(null, new object[] { w, a });
                        else
                            for (int i = 0; i < a.Length; i++) Write(w, info.Element, a.GetValue(i));
                        return;
                    }
                    case Kind.List:
                    {
                        var list = (IList)v;
                        w.Write(list.Count);
                        foreach (var item in list) Write(w, info.Element, item);
                        return;
                    }
                    case Kind.HashSet:
                    case Kind.Queue:
                    case Kind.Stack:
                    {
                        var items = new List<object>();
                        foreach (var item in (IEnumerable)v) items.Add(item);
                        if (info.Kind == Kind.Stack) items.Reverse(); // pushing back in this order restores the stack
                        w.Write(items.Count);
                        foreach (var item in items) Write(w, info.Element, item);
                        return;
                    }
                    case Kind.Dictionary:
                    {
                        var d = (IDictionary)v;
                        w.Write(d.Count);
                        foreach (DictionaryEntry kv in d)
                        {
                            Write(w, info.Key, kv.Key);
                            Write(w, info.Value, kv.Value);
                        }
                        return;
                    }
                    case Kind.Class:
                        foreach (var f in info.Fields) Write(w, f.FieldType, f.GetValue(v));
                        return;
                }
                throw new NotSupportedException(info.Type.FullName);
            }

            void WriteStructFields(BinaryWriter w, TypeInfo info, object v)
            {
                foreach (var f in info.Fields) Write(w, f.FieldType, f.GetValue(v));
            }
        }

        static void WritePrimitive(BinaryWriter w, Type t, object v)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Boolean: w.Write((bool)v); break;
                case TypeCode.Byte: w.Write((byte)v); break;
                case TypeCode.SByte: w.Write((sbyte)v); break;
                case TypeCode.Int16: w.Write((short)v); break;
                case TypeCode.UInt16: w.Write((ushort)v); break;
                case TypeCode.Int32: w.Write((int)v); break;
                case TypeCode.UInt32: w.Write((uint)v); break;
                case TypeCode.Int64: w.Write((long)v); break;
                case TypeCode.UInt64: w.Write((ulong)v); break;
                case TypeCode.Single: w.Write((float)v); break;
                case TypeCode.Double: w.Write((double)v); break;
                case TypeCode.Char: w.Write((char)v); break;
                default: throw new NotSupportedException(t.FullName);
            }
        }

        static object ReadPrimitive(BinaryReader r, Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Boolean: return r.ReadBoolean();
                case TypeCode.Byte: return r.ReadByte();
                case TypeCode.SByte: return r.ReadSByte();
                case TypeCode.Int16: return r.ReadInt16();
                case TypeCode.UInt16: return r.ReadUInt16();
                case TypeCode.Int32: return r.ReadInt32();
                case TypeCode.UInt32: return r.ReadUInt32();
                case TypeCode.Int64: return r.ReadInt64();
                case TypeCode.UInt64: return r.ReadUInt64();
                case TypeCode.Single: return r.ReadSingle();
                case TypeCode.Double: return r.ReadDouble();
                case TypeCode.Char: return r.ReadChar();
                default: throw new NotSupportedException(t.FullName);
            }
        }

        // ---------------------------------------------------------------- reading

        sealed class GraphReader
        {
            readonly BinaryReader _r;
            readonly List<object> _objects = new List<object>();
            readonly HashSet<object> _reused = new HashSet<object>(new RefEq());
            Type[] _types;

            public GraphReader(BinaryReader r) => _r = r;

            public void ReadObjectGraph(object root)
            {
                int n = _r.ReadInt32();
                _types = new Type[n];
                for (int i = 0; i < n; i++)
                {
                    string name = _r.ReadString(), schema = _r.ReadString();
                    var t = Type.GetType(name);
                    if (t == null) throw new InvalidDataException($"Tệp lưu dùng kiểu không còn tồn tại: {name}");
                    if (Schema(Info(t)) != schema)
                        throw new InvalidDataException($"Tệp lưu được tạo từ phiên bản code khác (kiểu {t.Name} đã thay đổi).");
                    _types[i] = t;
                }
                _r.ReadInt64(); // body length
                Read(root.GetType(), root);
            }

            // `existing` is what the fresh simulation already holds in this slot: reused in place when the
            // saved object is of the same type, so the event wiring made by constructors survives.
            object Read(Type declared, object existing)
            {
                var info = Info(declared);
                switch (info.Kind)
                {
                    case Kind.Skip: return existing;
                    case Kind.Primitive: return ReadPrimitive(_r, declared);
                    case Kind.Enum: return Enum.ToObject(declared, ReadPrimitive(_r, Enum.GetUnderlyingType(declared)));
                    case Kind.Blittable:
                    case Kind.Struct: return ReadStruct(info);
                }
                byte tag = _r.ReadByte();
                if (tag == 0) return null;
                if (tag == 2) return _objects[_r.ReadInt32()];
                var runtime = _types[_r.ReadInt32()];
                var rinfo = Info(runtime);
                object target = existing != null && existing.GetType() == runtime && !_reused.Contains(existing) && rinfo.Kind != Kind.String ? existing : null;
                if (target != null) _reused.Add(target);
                return ReadBody(rinfo, target);
            }

            int Register(object o)
            {
                _objects.Add(o);
                return _objects.Count - 1;
            }

            object ReadBody(TypeInfo info, object target)
            {
                switch (info.Kind)
                {
                    case Kind.String:
                    {
                        string s = _r.ReadString();
                        Register(s);
                        return s;
                    }
                    case Kind.Array:
                    {
                        int len = _r.ReadInt32();
                        var a = target is Array t && t.Length == len ? t : Array.CreateInstance(info.Element, len);
                        Register(a);
                        var e = Info(info.Element);
                        if (e.Kind == Kind.Primitive || e.Kind == Kind.Enum || e.Kind == Kind.Blittable)
                            ReadRawMethod.MakeGenericMethod(info.Element).Invoke(null, new object[] { _r, a });
                        else
                            for (int i = 0; i < len; i++) a.SetValue(Read(info.Element, a.GetValue(i)), i);
                        return a;
                    }
                    case Kind.List:
                    {
                        var list = (IList)(target ?? Activator.CreateInstance(info.Type));
                        Register(list);
                        var old = new List<object>();
                        foreach (var item in list) old.Add(item);
                        list.Clear();
                        int n = _r.ReadInt32();
                        for (int i = 0; i < n; i++) list.Add(Read(info.Element, i < old.Count ? old[i] : null));
                        return list;
                    }
                    case Kind.HashSet:
                    case Kind.Queue:
                    case Kind.Stack:
                    {
                        var c = target ?? Activator.CreateInstance(info.Type);
                        Register(c);
                        info.Type.GetMethod("Clear").Invoke(c, null);
                        var add = info.Type.GetMethod(info.Kind == Kind.HashSet ? "Add" : info.Kind == Kind.Queue ? "Enqueue" : "Push");
                        int n = _r.ReadInt32();
                        for (int i = 0; i < n; i++) add.Invoke(c, new[] { Read(info.Element, null) });
                        return c;
                    }
                    case Kind.Dictionary:
                    {
                        var d = (IDictionary)(target ?? Activator.CreateInstance(info.Type));
                        Register(d);
                        var old = new Dictionary<object, object>();
                        foreach (DictionaryEntry kv in d) old[kv.Key] = kv.Value;
                        d.Clear();
                        int n = _r.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            var k = Read(info.Key, null);
                            old.TryGetValue(k, out var prev);
                            d[k] = Read(info.Value, prev);
                        }
                        return d;
                    }
                    case Kind.Class:
                    {
                        var o = target ?? FormatterServices.GetUninitializedObject(info.Type);
                        Register(o);
                        foreach (var f in info.Fields) f.SetValue(o, Read(f.FieldType, f.GetValue(o)));
                        return o;
                    }
                }
                throw new NotSupportedException(info.Type.FullName);
            }

            object ReadStruct(TypeInfo info)
            {
                object boxed = FormatterServices.GetUninitializedObject(info.Type);
                foreach (var f in info.Fields) f.SetValue(boxed, Read(f.FieldType, f.GetValue(boxed)));
                return boxed;
            }
        }
    }
}
