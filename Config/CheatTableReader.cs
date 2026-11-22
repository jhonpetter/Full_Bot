using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Full_Bot
{
    public class CtFormulaStep
    {
        public string Op { get; set; } = string.Empty; // mul, add, sub, div
        public long Value { get; set; }
    }

    public class CtEntryNode
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string BaseName { get; set; } = string.Empty;
        public string VariableType { get; set; } = string.Empty;
        public string RawAddress { get; set; } = string.Empty;

        /// <summary>
        /// Deslocamento próprio do <Address> quando começa com + ou - (ex: "+0" => 0, "+D" => 0xD).
        /// </summary>
        public uint OwnAddressDisplacement { get; set; }
        public bool HasOwnAddressDisplacement { get; set; }

        /// <summary>
        /// Endereço base explícito (ex: "ELEMENTCLIENT.exe+A39F5C") ou herdado do ancestral.
        /// </summary>
        public string BaseAddress { get; set; } = string.Empty;

        /// <summary>
        /// Offsets do <Offsets> já ordenados da leitura do CE (ex: +6E0 -> +0).
        /// </summary>
        public uint[] LocalOffsets { get; set; } = Array.Empty<uint>();

        /// <summary>
        /// Cadeia completa de offsets acumulada desde o nó raiz até este nó.
        /// </summary>
        public uint[] FullOffsets { get; set; } = Array.Empty<uint>();

        public List<CtFormulaStep> FormulaSteps { get; set; } = new List<CtFormulaStep>();
        public bool HasFormula => FormulaSteps.Count > 0;

        public CtEntryNode Parent { get; set; }
        public List<CtEntryNode> Children { get; set; } = new List<CtEntryNode>();

        // Dentro de CtEntryNode (adicione estes métodos junto com EvalFormula):
        public uint[] GetLocalOffsetsUInt() => LocalOffsets ?? Array.Empty<uint>();

        public uint[] GetOffsetsUInt() => FullOffsets ?? Array.Empty<uint>();

        public uint EvaluateFormula(uint index) => EvalFormula(index);

        public uint EvalFormula(uint index)
        {
            if (FormulaSteps.Count == 0)
                return index;

            long acc = index;
            foreach (var step in FormulaSteps)
            {
                switch (step.Op)
                {
                    case "mul":
                        acc *= step.Value;
                        break;
                    case "add":
                        acc += step.Value;
                        break;
                    case "sub":
                        acc -= step.Value;
                        break;
                    case "div":
                        if (step.Value != 0)
                            acc /= step.Value;
                        break;
                }
            }

            return acc < 0 ? 0u : (uint)acc;
        }
    }
    public class CheatTableDocument
    {
        public List<CtEntryNode> Roots { get; set; } = new List<CtEntryNode>();
        public List<CtEntryNode> Entries { get; set; } = new List<CtEntryNode>();

        public CtEntryNode Find(string keyOrDescription)
        {
            if (string.IsNullOrWhiteSpace(keyOrDescription))
                return null;

            string needle = keyOrDescription.Trim();
            return Entries.FirstOrDefault(e =>
                string.Equals(e.Key, needle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.BaseName, needle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Description, needle, StringComparison.OrdinalIgnoreCase));
        }

        // Dentro de CheatTableDocument (adicione este método junto com Find):
        public string DetectProcessName(string fallback = "elementclient")
        {
            var baseNode = Find("base_address") ?? Find("base_ptr_raw");
            if (baseNode != null && !string.IsNullOrWhiteSpace(baseNode.BaseAddress))
            {
                string raw = baseNode.BaseAddress.Trim();
                int plusIdx = raw.IndexOf('+');
                string modPart = plusIdx >= 0 ? raw.Substring(0, plusIdx).Trim() : raw;
                if (!string.IsNullOrWhiteSpace(modPart))
                {
                    string withoutExt = Path.GetFileNameWithoutExtension(modPart);
                    if (!string.IsNullOrWhiteSpace(withoutExt))
                        return withoutExt.ToLowerInvariant();
                }
            }
            return fallback;
        }

        /// <summary>
        /// Resolve o endereço de memória seguindo os saltos do CE.
        /// Mantém o deslocamento de <Address> (+0, +D, etc.) e aplica fórmula quando existir.
        /// </summary>
        public uint ResolveAddress(
            CtEntryNode node,
            Func<string, uint> getModuleBase,
            Func<uint, uint> readUInt32,
            uint[] indices = null,
            bool dereferenceLastOffset = true)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.BaseAddress))
                return 0u;

            var chainNodes = new List<CtEntryNode>();
            var cur = node;
            while (cur != null)
            {
                chainNodes.Add(cur);
                cur = cur.Parent;
            }
            chainNodes.Reverse();

            // Se algum nó intermediário tiver endereço de módulo próprio diferente do pai, inicia dele
            int startIdx = 0;
            for (int i = chainNodes.Count - 1; i > 0; i--)
            {
                string prevBase = chainNodes[i - 1].BaseAddress ?? string.Empty;
                string currBase = chainNodes[i].BaseAddress ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(currBase) &&
                    !string.Equals(currBase, prevBase, StringComparison.OrdinalIgnoreCase))
                {
                    startIdx = i;
                    break;
                }
            }

            uint currentPtr = ParseBaseToAddress(chainNodes[startIdx].BaseAddress, getModuleBase);
            if (currentPtr == 0u)
                return 0u;

            int indexCursor = 0;

            for (int i = startIdx; i < chainNodes.Count; i++)
            {
                var n = chainNodes[i];
                bool isTarget = (i == chainNodes.Count - 1);

                uint idxVal = 0u;
                if (n.HasFormula && indices != null && indexCursor < indices.Length)
                    idxVal = indices[indexCursor++];

                // Deslocamento próprio do <Address> (ex: +0, +D, +0*2+13)
                if (n.HasOwnAddressDisplacement)
                {
                    uint addrDisp = n.OwnAddressDisplacement;
                    if (n.HasFormula && n.LocalOffsets.Length == 0)
                        addrDisp = unchecked(addrDisp + n.EvalFormula(idxVal));

                    if (n.LocalOffsets.Length == 0)
                    {
                        uint directAddr = unchecked(currentPtr + addrDisp);
                        if (isTarget && !dereferenceLastOffset)
                            return directAddr;

                        currentPtr = readUInt32(directAddr);
                        if (currentPtr == 0u)
                            return 0u;
                        continue;
                    }
                    else
                    {
                        currentPtr = unchecked(currentPtr + addrDisp);
                    }
                }

                // Cadeia de <Offsets> do nó
                for (int o = 0; o < n.LocalOffsets.Length; o++)
                {
                    uint off = n.LocalOffsets[o];
                    bool isLastOffset = isTarget && (o == n.LocalOffsets.Length - 1);

                    if (n.HasFormula && o == n.LocalOffsets.Length - 1)
                        off = unchecked(off + n.EvalFormula(idxVal));

                    uint target = unchecked(currentPtr + off);
                    if (isLastOffset && !dereferenceLastOffset)
                        return target;

                    currentPtr = readUInt32(target);
                    if (currentPtr == 0u)
                        return 0u;
                }
            }

            return currentPtr;
        }

        private static uint ParseBaseToAddress(string rawBase, Func<string, uint> getModuleBase)
        {
            string s = rawBase.Trim();
            int plusIdx = s.IndexOf('+');
            if (plusIdx < 0)
                return ParseHexUInt(s);

            string mod = s.Substring(0, plusIdx).Trim();
            string rva = s.Substring(plusIdx + 1).Trim();
            uint modBase = getModuleBase != null ? getModuleBase(mod) : 0u;
            if (modBase == 0u) return 0u;

            return unchecked(modBase + ParseHexUInt(rva));
        }

        private static uint ParseHexUInt(string raw)
        {
            string s = raw.Trim();
            if (s.StartsWith("+") || s.StartsWith("-"))
                s = s.Substring(1).Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    public static class CheatTableReader
    {
        public static CheatTableDocument Load(string ctPath)
        {
            if (!File.Exists(ctPath))
                throw new FileNotFoundException($"Tabela do Cheat Engine não encontrada: '{ctPath}'.", ctPath);

            var xdoc = XDocument.Load(ctPath);
            var rootEntriesEl = xdoc.Root?.Element("CheatEntries");
            if (rootEntriesEl == null)
                throw new FormatException("Elemento <CheatEntries> não encontrado na tabela .CT.");

            var doc = new CheatTableDocument();
            foreach (var entryEl in rootEntriesEl.Elements("CheatEntry"))
            {
                var node = ParseCheatEntry(entryEl, parent: null, inheritedBase: string.Empty, doc: doc);
                if (node != null)
                    doc.Roots.Add(node);
            }

            return doc;
        }

        private static CtEntryNode ParseCheatEntry(XElement el, CtEntryNode parent, string inheritedBase, CheatTableDocument doc)
        {
            int id = int.TryParse(el.Element("ID")?.Value?.Trim(), out int parsedId) ? parsedId : 0;
            string rawDesc = Unquote(el.Element("Description")?.Value ?? string.Empty);
            string varType = el.Element("VariableType")?.Value?.Trim() ?? string.Empty;
            string rawAddr = el.Element("Address")?.Value?.Trim() ?? string.Empty;

            var (baseName, descSteps) = ParseDescriptionFormula(rawDesc);

            var node = new CtEntryNode
            {
                Id = id,
                Description = rawDesc,
                Key = rawDesc,
                BaseName = baseName,
                VariableType = varType,
                RawAddress = rawAddr,
                Parent = parent
            };

            node.FormulaSteps.AddRange(descSteps);

            // Trata <Address>
            string currentBase = inheritedBase;
            if (!string.IsNullOrEmpty(rawAddr))
            {
                if (rawAddr.StartsWith("+") || rawAddr.StartsWith("-"))
                {
                    node.HasOwnAddressDisplacement = true;
                    if (rawAddr.Contains("*"))
                    {
                        var (disp, addrSteps) = ParseExpressionDisplacementAndFormula(rawAddr);
                        node.OwnAddressDisplacement = disp;
                        if (node.FormulaSteps.Count == 0)
                            node.FormulaSteps.AddRange(addrSteps);
                    }
                    else
                    {
                        node.OwnAddressDisplacement = ParseHexOffset(rawAddr);
                    }
                }
                else
                {
                    // Endereço estático / módulo (ex: ELEMENTCLIENT.exe+A39F5C)
                    currentBase = rawAddr;
                }
            }

            node.BaseAddress = currentBase;

            // Trata <Offsets> (no XML vêm em ordem reversa ao fluxo da cadeia no CE)
            var offsetsEl = el.Element("Offsets");
            if (offsetsEl != null)
            {
                var rawOffsetList = offsetsEl.Elements("Offset")
                    .Select(o => o.Value?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Reverse()
                    .ToList();

                var parsedOffsets = new List<uint>();
                foreach (var offStr in rawOffsetList)
                {
                    if (offStr.Contains("*"))
                    {
                        var (_, offSteps) = ParseExpressionDisplacementAndFormula(offStr);
                        if (node.FormulaSteps.Count == 0)
                            node.FormulaSteps.AddRange(offSteps);
                        parsedOffsets.Add(0u);
                    }
                    else
                    {
                        parsedOffsets.Add(ParseHexOffset(offStr));
                    }
                }

                node.LocalOffsets = parsedOffsets.ToArray();
            }

            // Monta FullOffsets acumulado
            var fullList = new List<uint>();
            if (parent != null)
                fullList.AddRange(parent.FullOffsets);
            if (node.HasOwnAddressDisplacement && node.LocalOffsets.Length == 0)
                fullList.Add(node.OwnAddressDisplacement);
            fullList.AddRange(node.LocalOffsets);
            node.FullOffsets = fullList.ToArray();

            doc.Entries.Add(node);
            parent?.Children.Add(node);

            // Filhos <CheatEntries>
            var childContainer = el.Element("CheatEntries");
            if (childContainer != null)
            {
                foreach (var childEl in childContainer.Elements("CheatEntry"))
                {
                    ParseCheatEntry(childEl, node, currentBase, doc);
                }
            }

            return node;
        }

        private static string Unquote(string s)
        {
            string t = s.Trim();
            if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
                return t.Substring(1, t.Length - 2);
            return t;
        }

        private static (string BaseName, List<CtFormulaStep> Steps) ParseDescriptionFormula(string desc)
        {
            var steps = new List<CtFormulaStep>();
            int idxPos = desc.IndexOf("-index", StringComparison.OrdinalIgnoreCase);
            if (idxPos < 0)
                return (desc, steps);

            string baseName = desc.Substring(0, idxPos);
            string suffix = desc.Substring(idxPos);
            string rest = suffix.Substring("-index".Length).Trim('-');

            if (!string.IsNullOrEmpty(rest))
            {
                string[] parts = rest.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    string op = parts[i].ToLowerInvariant();
                    long val = long.Parse(parts[i + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    steps.Add(new CtFormulaStep { Op = op, Value = val });
                }
            }

            return (baseName, steps);
        }

        private static (uint BaseDisplacement, List<CtFormulaStep> Steps) ParseExpressionDisplacementAndFormula(string exprRaw)
        {
            var steps = new List<CtFormulaStep>();
            string clean = exprRaw.Trim().TrimStart('+').Replace(" ", string.Empty);

            // Ex: "0*10+10" ou "2*4" ou "0*2+13"
            var match = Regex.Match(clean, @"^(?<base>[0-9A-Fa-fxX]+)(?<ops>.*)$");
            if (!match.Success)
                return (0u, steps);

            uint baseDisp = ParseHexOffset(match.Groups["base"].Value);
            string ops = match.Groups["ops"].Value;

            foreach (Match opMatch in Regex.Matches(ops, @"(?<op>[\+\-\*/])(?<val>[0-9A-Fa-fxX]+)"))
            {
                string sym = opMatch.Groups["op"].Value;
                string opName = sym == "*" ? "mul" : sym == "+" ? "add" : sym == "-" ? "sub" : "div";
                long val = long.Parse(opMatch.Groups["val"].Value.Replace("0x", "").Replace("0X", ""),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                steps.Add(new CtFormulaStep { Op = opName, Value = val });
            }

            return (baseDisp, steps);
        }

        private static uint ParseHexOffset(string raw)
        {
            string s = raw.Trim();
            if (s.StartsWith("+") || s.StartsWith("-"))
                s = s.Substring(1).Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            if (string.IsNullOrEmpty(s))
                return 0u;

            return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}

