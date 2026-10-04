// Json.cs —— 极小的 JSON 解析（只读），不依赖任何库。
//
// 为什么自己写：模型文件在**离线工具**和**运行时 mod** 里都要解析。
// 工具用 System.Text.Json、运行时用 Newtonsoft 会变成两套解析、两个真相；
// 自己写一份（~150 行）就能两边共用，且不引入依赖（Unity 里也不用管包）。
// 只支持 JSON 标准子集：对象 / 数组 / 字符串（含转义）/ 数字 / true / false / null。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModelKit
{
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    public sealed class JsonValue
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string Str;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public bool IsObject => Kind == JsonKind.Object;
        public bool IsArray => Kind == JsonKind.Array;

        public JsonValue this[string key]
            => (Kind == JsonKind.Object && Object.TryGetValue(key, out var v)) ? v : null;

        public JsonValue this[int i]
            => (Kind == JsonKind.Array && i >= 0 && i < Array.Count) ? Array[i] : null;

        public int Count => Kind == JsonKind.Array ? (Array?.Count ?? 0) : 0;

        public float AsFloat(float fallback = 0f)
            => Kind == JsonKind.Number ? (float)Number : (Kind == JsonKind.String && float.TryParse(Str, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback);

        public int AsInt(int fallback = 0) => Kind == JsonKind.Number ? (int)Math.Round(Number) : fallback;
        public string AsString(string fallback = null) => Kind == JsonKind.String ? Str : fallback;

        public bool Has(string key) => Kind == JsonKind.Object && Object.ContainsKey(key);
    }

    /// <summary>JSON 解析失败（本文件自带，不依赖别处 ✓）</summary>
    public sealed class JsonException : System.Exception
    {
        public JsonException(string message) : base(message) { }
    }

    public static class Json
    {
        public static JsonValue Parse(string text)
        {
            int i = 0;
            var v = ParseValue(text, ref i);
            SkipWs(text, ref i);
            return v;   // 尾随内容忽略
        }

        static JsonValue ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new JsonException("JSON 意外结束");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return new JsonValue { Kind = JsonKind.String, Str = ParseString(s, ref i) };
                case 't': Expect(s, ref i, "true"); return new JsonValue { Kind = JsonKind.Bool, Bool = true };
                case 'f': Expect(s, ref i, "false"); return new JsonValue { Kind = JsonKind.Bool, Bool = false };
                case 'n': Expect(s, ref i, "null"); return new JsonValue { Kind = JsonKind.Null };
                default: return new JsonValue { Kind = JsonKind.Number, Number = ParseNumber(s, ref i) };
            }
        }

        static JsonValue ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, JsonValue>();
            i++;                                   // '{'
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return new JsonValue { Kind = JsonKind.Object, Object = obj }; }
            while (true)
            {
                SkipWs(s, ref i);
                var key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new JsonException($"JSON: 期望 ':'（位置 {i}）");
                i++;
                obj[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonException("JSON: 对象未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new JsonException($"JSON: 期望 ',' 或 '}}'（位置 {i}）");
            }
            return new JsonValue { Kind = JsonKind.Object, Object = obj };
        }

        static JsonValue ParseArray(string s, ref int i)
        {
            var arr = new List<JsonValue>();
            i++;                                   // '['
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return new JsonValue { Kind = JsonKind.Array, Array = arr }; }
            while (true)
            {
                arr.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonException("JSON: 数组未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new JsonException($"JSON: 期望 ',' 或 ']'（位置 {i}）");
            }
            return new JsonValue { Kind = JsonKind.Array, Array = arr };
        }

        static string ParseString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"') throw new JsonException($"JSON: 期望字符串（位置 {i}）");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new JsonException("JSON: 字符串未闭合");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            var slice = s.Substring(start, i - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                throw new JsonException($"JSON: 不是数字 '{slice}'（位置 {start}）");
            return d;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static void Expect(string s, ref int i, string token)
        {
            if (i + token.Length > s.Length || s.Substring(i, token.Length) != token)
                throw new JsonException($"JSON: 期望 '{token}'（位置 {i}）");
            i += token.Length;
        }
    }
}
