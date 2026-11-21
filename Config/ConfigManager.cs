using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Full_Bot
{

    public class FormulaStep
    {
        [JsonPropertyName("op_symbol")]
        public string OpSymbol { get; set; } = string.Empty;

        [JsonPropertyName("op_token")]
        public string OpToken { get; set; } = string.Empty;

        [JsonPropertyName("operand")]
        public string Operand { get; set; } = string.Empty;
    }

    public class AncestorArrayInfo
    {
        [JsonPropertyName("from_path")]
        public string FromPath { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("suffix")]
        public string Suffix { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;
    }

    public class OffsetNode
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("var_name")]
        public string VarName { get; set; } = string.Empty;

        [JsonPropertyName("base_name")]
        public string BaseName { get; set; } = string.Empty;

        [JsonPropertyName("array_suffix")]
        public string ArraySuffix { get; set; } = string.Empty;

        [JsonPropertyName("array_mode")]
        public string ArrayMode { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("raw_description")]
        public string RawDescription { get; set; } = string.Empty;

        [JsonPropertyName("base_address")]
        public string BaseAddress { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("local_offsets")]
        public List<string> LocalOffsets { get; set; } = new List<string>();

        [JsonPropertyName("offsets")]
        public List<string> Offsets { get; set; } = new List<string>();

        [JsonPropertyName("vartype")]
        public string VarType { get; set; } = string.Empty;

        [JsonPropertyName("is_array")]
        public bool IsArray { get; set; }

        [JsonPropertyName("own_is_array")]
        public bool OwnIsArray { get; set; }

        [JsonPropertyName("has_ancestor_array")]
        public bool HasAncestorArray { get; set; }

        [JsonPropertyName("array_source")]
        public string ArraySource { get; set; } = string.Empty;

        [JsonPropertyName("formula_steps")]
        public List<FormulaStep> FormulaSteps { get; set; } = new List<FormulaStep>();

        [JsonPropertyName("ancestor_arrays")]
        public List<AncestorArrayInfo> AncestorArrays { get; set; } = new List<AncestorArrayInfo>();

        [JsonPropertyName("is_group_header")]
        public bool IsGroupHeader { get; set; }

        [JsonPropertyName("children")]
        public Dictionary<string, OffsetNode> Children { get; set; } = new Dictionary<string, OffsetNode>(StringComparer.OrdinalIgnoreCase);



        public uint[] GetOffsetsUInt()
        {
            return ConfigManager.ParseOffsetList(Offsets);
        }

        public uint[] GetLocalOffsetsUInt()
        {
            return ConfigManager.ParseOffsetList(LocalOffsets);
        }

        public uint EvaluateFormula(uint index)
        {
            if (FormulaSteps == null || FormulaSteps.Count == 0)
                return index;

            long acc = index;
            foreach (var step in FormulaSteps)
            {
                long operand = ConfigManager.ParseFormulaOperand(step.Operand);
                string token = (step.OpToken ?? string.Empty).Trim().ToLowerInvariant();
                string sym = (step.OpSymbol ?? string.Empty).Trim();

                if (token == "mul" || sym == "*")
                    acc *= operand;
                else if (token == "add" || sym == "+")
                    acc += operand;
                else if (token == "sub" || sym == "-")
                    acc -= operand;
                else if ((token == "div" || sym == "/") && operand != 0)
                    acc /= operand;
            }

            return acc < 0 ? 0u : (uint)acc;
        }
    }


    public class TreeConfigRoot
    {
        [JsonPropertyName("tree")]
        public Dictionary<string, OffsetNode> Tree { get; set; } = new Dictionary<string, OffsetNode>(StringComparer.OrdinalIgnoreCase);

        public OffsetNode FindByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || Tree == null)
                return null;

            return ConfigManager.FindNodeRecursive(Tree.Values, n =>
                string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n.VarName, key, StringComparison.OrdinalIgnoreCase));
        }

        public OffsetNode FindByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Tree == null)
                return null;

            string normalized = path.Trim().Trim('/');
            return ConfigManager.FindNodeRecursive(Tree.Values, n =>
                string.Equals((n.Path ?? string.Empty).Trim('/'), normalized, StringComparison.OrdinalIgnoreCase));
        }

        public string DetectProcessName(string fallback = "elementclient")
        {
            var baseNode = FindByKey("base_address") ?? FindByKey("base_ptr_raw");
            if (baseNode != null)
            {
                var (moduleName, _) = ConfigManager.SplitModuleAndRva(baseNode.BaseAddress);
                if (!string.IsNullOrWhiteSpace(moduleName))
                    return Path.GetFileNameWithoutExtension(moduleName).ToLowerInvariant();
            }
            return fallback;
        }
    }

    public static class ConfigManager
    {
        internal static uint[] SkipFirst(uint[] array)
        {
            if (array == null || array.Length <= 1)
                return Array.Empty<uint>();

            uint[] rest = new uint[array.Length - 1];
            Array.Copy(array, 1, rest, 0, rest.Length);
            return rest;
        }
        public static TreeConfigRoot Load(string path = @"Config\config.json")
        {
            string resolvedPath = ResolveConfigPath(path);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Arquivo de configuração não encontrado: '{resolvedPath}'.", resolvedPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            string json = File.ReadAllText(resolvedPath);
            var root = JsonSerializer.Deserialize<TreeConfigRoot>(json, options) ?? new TreeConfigRoot();
            PopulateKeys(root.Tree);
            return root;
        }

        private static string ResolveConfigPath(string path)
        {
            if (File.Exists(path)) return path;
            if (File.Exists(@"Config\config.json")) return @"Config\config.json";
            //if (File.Exists(@"Config\offsets.json")) return @"Config\offsets.json";
            return path;
        }

        private static void PopulateKeys(Dictionary<string, OffsetNode> map)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                if (kv.Value == null) continue;
                if (string.IsNullOrWhiteSpace(kv.Value.Key))
                    kv.Value.Key = kv.Key;
                if (string.IsNullOrWhiteSpace(kv.Value.VarName))
                    kv.Value.VarName = kv.Key;
                if (kv.Value.Children != null && kv.Value.Children.Count > 0)
                    PopulateKeys(kv.Value.Children);
            }
        }

        internal static OffsetNode FindNodeRecursive(IEnumerable<OffsetNode> nodes, Func<OffsetNode, bool> predicate)
        {
            if (nodes == null) return null;
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (predicate(node)) return node;
                if (node.Children != null && node.Children.Count > 0)
                {
                    var found = FindNodeRecursive(node.Children.Values, predicate);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public static (string ModuleName, uint Rva) SplitModuleAndRva(string rawBaseAddress)
        {
            if (string.IsNullOrWhiteSpace(rawBaseAddress))
                return (string.Empty, 0);

            string s = rawBaseAddress.Trim();
            int plusIdx = s.IndexOf('+');
            if (plusIdx < 0)
                return (string.Empty, ParseHex(s));

            string mod = s.Substring(0, plusIdx).Trim();
            string rvaPart = s.Substring(plusIdx + 1).Trim();
            return (mod, ParseHex(rvaPart));
        }

        public static uint ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;
            string cleaned = hex.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(2);

            if (uint.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val))
                return val;

            throw new FormatException($"Offset/endereço hexadecimal inválido no JSON: '{hex}'.");
        }

        public static long ParseFormulaOperand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            string s = raw.Trim();

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            // Em fórmulas vindas do config.json (ex: "1e"), tenta hex se contiver letras A-F; caso contrário lê inteiro
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long decVal))
            {
                // Se a string só tem dígitos (ex: "4", "10", "12", "20"), em fórmulas como -index-mul-10 ou -index-mul-20
                // o sufixo veio da descrição/fórmula original do Lua. Mantém compatibilidade com ParseHexOrDecNumber do Lua.
                return decVal;
            }

            if (long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexVal))
                return hexVal;

            throw new FormatException($"Operando de fórmula inválido no JSON: '{raw}'.");
        }

        public static uint[] ParseOffsetList(IEnumerable<string> offsets)
        {
            if (offsets == null) return Array.Empty<uint>();
            var list = new List<uint>();
            foreach (var item in offsets)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    list.Add(ParseHex(item));
            }
            return list.ToArray();
        }
    }
}